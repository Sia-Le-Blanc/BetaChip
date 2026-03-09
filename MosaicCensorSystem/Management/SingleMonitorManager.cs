// SingleMonitorManager.cs
#nullable disable
using MosaicCensorSystem.Capture;
using MosaicCensorSystem.Overlay;
using MosaicCensorSystem.UI;
using OpenCvSharp;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Windows.Forms;

namespace MosaicCensorSystem.Management
{
    public class SingleMonitorManager : IOverlayManager
    {
        private GuiController ui;
        private readonly ScreenCapture capturer;
        private readonly FullscreenOverlay overlay;
        private readonly bool _ownsCapture;    // true이면 Dispose 시 capturer를 직접 해제
        private readonly int  _monitorIndex;   // 0-based, 프로파일러 로그 식별자

        // ── 제로-지연 파이프라인 (Capacity=1, DropOldest) ──────────────────────
        private Channel<Mat> _frameChannel;
        private Thread _captureThread;
        private Thread _inferenceThread;

        // ── 오버레이 렌더 타이머 (33ms ≈ 30fps, 추론과 완전히 독립) ───────────
        private System.Threading.Timer _renderTimer;

        private volatile bool isRunning = false;
        private CensorSettings settings = new(true, true, false, false);
        private Func<Mat, Mat> processFrame;
        private readonly object disposeLock = new object();
        private bool isDisposed = false;

        // ── 단계별 성능 프로파일러 ────────────────────────────────────────────
        private readonly Stopwatch _swCapture    = new Stopwatch();
        private readonly Stopwatch _swInference  = new Stopwatch();
        private readonly Stopwatch _swRender     = new Stopwatch();
        private readonly Stopwatch _profilerClock = Stopwatch.StartNew();

        private int    _frameCount       = 0;
        private double _totalCaptureMs   = 0;
        private double _totalInferenceMs = 0;
        private double _totalRenderMs    = 0;

        /// <summary>FREE 등급(단일 모니터): 외부에서 생성된 ScreenCapture를 받습니다.</summary>
        public SingleMonitorManager(ScreenCapture screenCapturer)
        {
            _monitorIndex = 0;
            _ownsCapture  = false;
            capturer = screenCapturer;
            overlay  = new FullscreenOverlay(Screen.PrimaryScreen.Bounds);
        }

        /// <summary>
        /// PLUS/PATREON 등급(다중 모니터): 주어진 bounds에 전용 ScreenCapture를 생성합니다.
        /// 각 모니터를 독립 캡처하므로 종횡비 왜곡이 원천 차단됩니다.
        /// </summary>
        public SingleMonitorManager(System.Drawing.Rectangle bounds, int monitorIndex)
        {
            _monitorIndex = monitorIndex;
            _ownsCapture  = true;
            capturer = new ScreenCapture(bounds);
            overlay  = new FullscreenOverlay(bounds);
        }

        public int MonitorIndex => _monitorIndex;

        public void Initialize(GuiController uiController)
        {
            ui = uiController;
        }

        public void Start(Func<Mat, Mat> frameProcessor)
        {
            lock (disposeLock)
            {
                if (isRunning || isDisposed) return;
                isRunning    = true;
                processFrame = frameProcessor;

                try
                {
                    overlay.Show();
                    StartPipeline();
                }
                catch (Exception ex)
                {
                    ui?.LogMessage($"🚨 [모니터 {_monitorIndex + 1}] 시작 실패: {ex.Message}");
                    isRunning = false;
                }
            }
        }

        public void Stop()
        {
            lock (disposeLock)
            {
                if (!isRunning) return;
                isRunning = false;

                _renderTimer?.Dispose();
                _renderTimer = null;

                try { _frameChannel?.Writer.TryComplete(); } catch { }

                _captureThread?.Join(1500);
                _inferenceThread?.Join(1500);

                try { overlay?.Hide(); } catch { }
            }
        }

        public void UpdateSettings(CensorSettings newSettings)
        {
            settings = newSettings;
        }

        // ── 파이프라인 초기화 ─────────────────────────────────────────────────
        private void StartPipeline()
        {
            _frameChannel = Channel.CreateBounded<Mat>(new BoundedChannelOptions(1)
            {
                SingleReader = false,   // CaptureLoop(drain) + InferenceLoop(consume) 양쪽에서 읽음
                SingleWriter = true,
                AllowSynchronousContinuations = false,
            });

            _frameCount = 0;
            _totalCaptureMs = _totalInferenceMs = _totalRenderMs = 0;
            _profilerClock.Restart();

            // 렌더 타이머: 추론 속도와 무관하게 30fps로 오버레이를 항상 최신 상태로 유지
            _renderTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    if (overlay != null && !overlay.IsDisposed)
                        overlay.BeginInvoke(new Action(() =>
                        {
                            if (!overlay.IsDisposed) overlay.Invalidate();
                        }));
                }
                catch { }
            }, null, 0, 33);

            _captureThread = new Thread(CaptureLoop)
            {
                IsBackground = true,
                Name         = $"BetaChip_Capture_M{_monitorIndex + 1}",
                Priority     = ThreadPriority.AboveNormal
            };
            _inferenceThread = new Thread(InferenceLoop)
            {
                IsBackground = true,
                Name         = $"BetaChip_Inference_M{_monitorIndex + 1}"
            };

            _captureThread.Start();
            _inferenceThread.Start();
        }

        // ── 생산자 스레드: 독립 캡처 + DropOldest ──────────────────────────────
        // 이 모니터의 bounds만 캡처하므로 다른 모니터의 해상도/종횡비에 영향을 받지 않음
        private void CaptureLoop()
        {
            var writer = _frameChannel.Writer;

            while (isRunning && !isDisposed)
            {
                try
                {
                    if (capturer == null) break;

                    _swCapture.Restart();
                    Mat rawFrame = capturer.GetFrame();
                    _totalCaptureMs += _swCapture.Elapsed.TotalMilliseconds;

                    if (rawFrame == null || rawFrame.IsDisposed || rawFrame.Empty())
                    {
                        rawFrame?.Dispose();
                        Thread.Sleep(1);
                        continue;
                    }

                    // DropOldest: 낡은 대기 프레임 명시 폐기 → Mat 메모리 누수 방지
                    while (_frameChannel.Reader.TryRead(out Mat stale))
                        stale?.Dispose();

                    if (!writer.TryWrite(rawFrame))
                        rawFrame.Dispose();
                }
                catch (ChannelClosedException)  { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex) when (isRunning)
                {
                    ui?.LogMessage($"🚨 [모니터 {_monitorIndex + 1}] 캡처 오류: {ex.Message}");
                    Thread.Sleep(10);
                }
            }

            writer.TryComplete();
        }

        // ── 소비자 스레드: 오포르투니스틱 추론 ────────────────────────────────
        // 이전 추론 완료 즉시 + 새 프레임이 있을 때만 동작 (고정 FPS 슬립 없음)
        private void InferenceLoop()
        {
            var reader = _frameChannel.Reader;

            while (isRunning && !isDisposed)
            {
                Mat rawFrame       = null;
                Mat processedFrame = null;

                try
                {
                    if (!reader.TryRead(out rawFrame))
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    _swInference.Restart();
                    processedFrame = processFrame?.Invoke(rawFrame);
                    _totalInferenceMs += _swInference.Elapsed.TotalMilliseconds;

                    _swRender.Restart();
                    if (processedFrame != null && !processedFrame.IsDisposed && overlay != null)
                        overlay.UpdateFrame(processedFrame);
                    _totalRenderMs += _swRender.Elapsed.TotalMilliseconds;

                    // 1초마다 모니터별 성능 로그
                    _frameCount++;
                    if (_profilerClock.Elapsed.TotalSeconds >= 1.0)
                    {
                        int n = Math.Max(1, _frameCount);
                        ui?.LogMessage(
                            $"⏱ [모니터 {_monitorIndex + 1}] FPS:{_frameCount} | " +
                            $"캡처:{_totalCaptureMs / n:F1}ms | " +
                            $"추론:{_totalInferenceMs / n:F1}ms | " +
                            $"렌더:{_totalRenderMs / n:F1}ms");

                        _frameCount = 0;
                        _totalCaptureMs = _totalInferenceMs = _totalRenderMs = 0;
                        _profilerClock.Restart();
                    }
                }
                catch (ChannelClosedException)  { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex) when (isRunning)
                {
                    ui?.LogMessage($"🚨 [모니터 {_monitorIndex + 1}] 추론 오류: {ex.Message}");
                    if (!isRunning || isDisposed) break;
                    Thread.Sleep(50);
                }
                finally
                {
                    rawFrame?.Dispose();
                    processedFrame?.Dispose();
                }
            }
        }

        public void Dispose()
        {
            lock (disposeLock)
            {
                if (isDisposed) return;
                isDisposed = true;

                Stop();

                // 다중 모니터 모드에서만 capturer를 직접 소유하므로 여기서 해제
                if (_ownsCapture) try { capturer?.Dispose(); } catch { }
                try { overlay?.Dispose(); } catch { }
            }
        }
    }
}
