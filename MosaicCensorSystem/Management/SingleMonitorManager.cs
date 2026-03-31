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
        private IGuiController ui;
        private readonly ScreenCapture capturer;
        private readonly FullscreenOverlay overlay;
        private readonly bool _ownsCapture;
        private readonly int _monitorIndex;

        private Channel<ScreenCapture.CapturedFrame> _frameChannel;
        private Thread _captureThread;
        private Thread _inferenceThread;

        private volatile bool isRunning = false;
        private CensorSettings settings = new(true, true, false, false);
        private Func<Mat, Mat> processFrame;
        private readonly object disposeLock = new object();
        private bool isDisposed = false;

        private readonly Stopwatch _swCapture    = new Stopwatch();
        private readonly Stopwatch _swInference  = new Stopwatch();
        private readonly Stopwatch _swRender     = new Stopwatch();
        private readonly Stopwatch _profilerClock = Stopwatch.StartNew();
        private readonly Stopwatch _captureLogClock = Stopwatch.StartNew();
        private bool _firstCaptureLogged = false;

        private int _frameCount = 0;
        private double _totalCaptureMs = 0;
        private double _totalInferenceMs = 0;
        private double _totalRenderMs = 0;

        public SingleMonitorManager(ScreenCapture screenCapturer)
        {
            _monitorIndex = 0;
            _ownsCapture = false;
            capturer = screenCapturer;
            overlay = new FullscreenOverlay(Screen.PrimaryScreen.Bounds);
        }

        public SingleMonitorManager(System.Drawing.Rectangle bounds, int monitorIndex)
        {
            _monitorIndex = monitorIndex;
            _ownsCapture = true;
            capturer = new ScreenCapture(bounds);
            overlay = new FullscreenOverlay(bounds);
        }

        public int MonitorIndex => _monitorIndex;
        public void Initialize(IGuiController uiController) => ui = uiController;

        public void Start(Func<Mat, Mat> frameProcessor)
        {
            lock (disposeLock) {
                if (isRunning || isDisposed) return;
                isRunning = true;
                processFrame = frameProcessor;
                try {
                    overlay.Show();
                    StartPipeline();
                } catch { isRunning = false; }
            }
        }

        public void Stop()
        {
            lock (disposeLock) {
                if (!isRunning) return;
                isRunning = false;
                try { _frameChannel?.Writer.TryComplete(); } catch { }
                _captureThread?.Join(1500);
                _inferenceThread?.Join(1500);
                try { overlay?.Hide(); } catch { }
            }
        }

        public void UpdateSettings(CensorSettings newSettings) => settings = newSettings;

        private void StartPipeline()
        {
            _frameChannel = Channel.CreateBounded<ScreenCapture.CapturedFrame>(new BoundedChannelOptions(1) {
                SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = false
            });
            _frameCount = 0; _totalCaptureMs = _totalInferenceMs = _totalRenderMs = 0;
            _profilerClock.Restart();

            _captureThread = new Thread(CaptureLoop) { IsBackground = true, Priority = ThreadPriority.AboveNormal };
            _inferenceThread = new Thread(InferenceLoop) { IsBackground = true };
            _captureThread.Start();
            _inferenceThread.Start();
            ui?.LogMessage($"🚀 [System] M{_monitorIndex + 1} 캡처 및 추론 루프 기동 완료.");
        }

        private void CaptureLoop()
        {
            var writer = _frameChannel.Writer;
            while (isRunning && !isDisposed) {
                try {
                    if (capturer == null) break;
                    
                    _swCapture.Restart();
                    var captured = capturer.CaptureFrame();
                    _totalCaptureMs += _swCapture.Elapsed.TotalMilliseconds;
                    if (captured == null || captured.Frame == null || captured.Frame.IsDisposed) {
                        ui?.LogMessage("⚠️ [Debug] CaptureFrame returned NULL (Checking handle or buffer...)");
                        captured?.Dispose(); Thread.Sleep(100); continue;
                    }

                    // [DEBUG] 단 1회만 성공 로그 남김
                    if (!_firstCaptureLogged) { 
                        ui?.LogMessage($"📸 [Debug] M{_monitorIndex+1} 첫 캡처 성공! ({captured.Frame.Width}x{captured.Frame.Height})"); 
                        _firstCaptureLogged = true; 
                    }
                    if (!writer.TryWrite(captured)) captured.Dispose();
                } catch (Exception ex) { 
                    ui?.LogMessage($"❌ [Error] CaptureLoop {MonitorIndex+1}: {ex.Message}");
                    break; 
                }
            }
            writer.TryComplete();
            ui?.LogMessage($"⏹ [System] CaptureLoop {MonitorIndex+1} 종료됨.");
        }

        private void InferenceLoop()
        {
            var reader = _frameChannel.Reader;
            while (isRunning && !isDisposed) {
                ScreenCapture.CapturedFrame captured = null;
                try {
                    if (!reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult()) break;
                    if (!reader.TryRead(out captured)) continue;

                    _swInference.Restart();
                    var processed = processFrame?.Invoke(captured.Frame);
                    _totalInferenceMs += _swInference.Elapsed.TotalMilliseconds;

                    _swRender.Restart();
                    if (processed != null && overlay != null) overlay.UpdateFrame(processed);
                    _totalRenderMs += _swRender.Elapsed.TotalMilliseconds;

                    _frameCount++;
                    if (_profilerClock.Elapsed.TotalSeconds >= 1.0) {
                        int n = Math.Max(1, _frameCount);
                        ui?.LogMessage($"⏱ [M{_monitorIndex + 1}] FPS:{_frameCount} | C:{_totalCaptureMs/n:F1}ms | I:{_totalInferenceMs/n:F1}ms | R:{_totalRenderMs/n:F1}ms");
                        _frameCount = 0; _totalCaptureMs = _totalInferenceMs = _totalRenderMs = 0; _profilerClock.Restart();
                    }
                } catch (Exception ex) { 
                    ui?.LogMessage($"❌ [Error] InferenceLoop {MonitorIndex+1}: {ex.Message}");
                    break; 
                }
                finally { captured?.Dispose(); }
            }
            ui?.LogMessage($"⏹ [System] InferenceLoop {MonitorIndex+1} 종료됨.");
        }

        public void Dispose()
        {
            lock (disposeLock) {
                if (isDisposed) return;
                isDisposed = true;
                Stop();
                if (_ownsCapture) capturer?.Dispose();
                overlay?.Dispose();
            }
        }
    }
}
