// MultiMonitorManager.cs
#nullable disable
using MosaicCensorSystem.UI;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace MosaicCensorSystem.Management
{
    /// <summary>
    /// 감지된 각 Screen마다 독립적인 SingleMonitorManager 인스턴스를 생성하여
    /// 완전 병렬로 구동하는 다중 모니터 관리자입니다.
    ///
    /// ▸ 각 모니터: 독립 CaptureLoop / InferenceLoop / OverlayWindow / RenderTimer
    /// ▸ 종횡비 왜곡 원천 차단: 각 모니터가 자신의 bounds로만 캡처
    /// ▸ 자원 적응형 추론 게이트:
    ///    - GPU 활성: 모든 모니터 InferenceLoop가 동시에 model.Run() 호출 (병렬)
    ///    - CPU 전용: SemaphoreSlim(1,1)로 직렬화 → CPU 과부하 방지
    /// ▸ 성능 로그: [모니터 1] FPS:N | 캡처:Xms | 추론:Yms | 렌더:Zms (모니터별 독립)
    /// </summary>
    public class MultiMonitorManager : IOverlayManager
    {
        private IGuiController _ui;
        private readonly List<SingleMonitorManager> _monitors = new();
        private volatile bool _isRunning = false;
        private CensorSettings _settings = new(true, true, false, false);
        private readonly object _disposeLock = new object();
        private bool _isDisposed = false;

        // ── 자원 적응형 추론 게이트 ──────────────────────────────────────────
        // processFrame(= DetectObjects + ApplyCensor)을 래핑하여 CPU/GPU에 따라 직렬/병렬 제어
        // GPU: SemaphoreSlim(N, N) → 모든 모니터 동시 통과 (병렬 추론)
        // CPU: SemaphoreSlim(1, 1) → 한 번에 하나만 통과 (직렬 추론, CPU 과부하 방지)
        private readonly SemaphoreSlim _inferenceGate;
        private readonly bool _isGpuActive;

        /// <param name="isGpuActive">
        /// true(CUDA/DirectML): 병렬 추론. false(CPU): 직렬 추론.
        /// </param>
        public MultiMonitorManager(bool isGpuActive = false)
        {
            _isGpuActive = isGpuActive;

            var screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
                _monitors.Add(new SingleMonitorManager(screens[i].Bounds, i));

            int parallelism = isGpuActive ? Math.Max(1, screens.Length) : 1;
            _inferenceGate = new SemaphoreSlim(parallelism, parallelism);
        }

        public void Initialize(IGuiController uiController)
        {
            _ui = uiController;
            string mode = _isGpuActive ? "GPU 병렬 모드" : "CPU 직렬 모드";
            _ui.LogMessage($"🖥️ 감지된 모니터: {_monitors.Count}개 | 추론: {mode}");

            var screens = Screen.AllScreens;
            for (int i = 0; i < _monitors.Count && i < screens.Length; i++)
            {
                var b = screens[i].Bounds;
                _ui.LogMessage($"   모니터 {i + 1}: {b.Width}×{b.Height} @ ({b.X},{b.Y})");
            }

            foreach (var m in _monitors)
                m.Initialize(uiController);
        }

        public void Start(Func<Mat, Mat> frameProcessor)
        {
            lock (_disposeLock)
            {
                if (_isRunning || _isDisposed) return;
                _isRunning = true;

                // 추론 게이트로 래핑: CPU 직렬 / GPU 병렬
                Func<Mat, Mat> gated = (frame) =>
                {
                    _inferenceGate.Wait();
                    try   { return frameProcessor(frame); }
                    finally { _inferenceGate.Release(); }
                };

                foreach (var m in _monitors)
                {
                    try { m.Start(gated); }
                    catch (Exception ex)
                    {
                        _ui?.LogMessage($"🚨 [모니터 {m.MonitorIndex + 1}] 시작 실패: {ex.Message}");
                    }
                }
            }
        }

        public void Stop()
        {
            lock (_disposeLock)
            {
                if (!_isRunning) return;
                _isRunning = false;
                foreach (var m in _monitors) { try { m.Stop(); } catch { } }
            }
        }

        public void UpdateSettings(CensorSettings newSettings)
        {
            _settings = newSettings;
            foreach (var m in _monitors) m.UpdateSettings(newSettings);
        }

        public void Dispose()
        {
            lock (_disposeLock)
            {
                if (_isDisposed) return;
                _isDisposed = true;

                Stop();

                foreach (var m in _monitors) { try { m.Dispose(); } catch { } }
                _monitors.Clear();

                _inferenceGate?.Dispose();
            }
        }
    }
}
