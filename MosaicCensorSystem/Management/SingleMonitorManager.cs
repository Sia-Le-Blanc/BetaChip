// SingleMonitorManager.cs
using MosaicCensorSystem.Capture;
using MosaicCensorSystem.Overlay;
using MosaicCensorSystem.UI;
using OpenCvSharp;
using System;
using System.Threading;
using System.Windows.Forms; // MessageBox를 위해 추가

namespace MosaicCensorSystem.Management
{
    /// <summary>
    /// 무료 버전을 위한 단일 모니터 오버레이 관리자
    /// </summary>
    public class SingleMonitorManager : IOverlayManager
    {
        private GuiController ui;
        private readonly ScreenCapture capturer;
        private readonly FullscreenOverlay overlay;
        private Thread processThread;
        private volatile bool isRunning = false;
        private CensorSettings settings;
        private Func<Mat, Mat> processFrame;

        public SingleMonitorManager(ScreenCapture screenCapturer)
        {
            capturer = screenCapturer;
            overlay = new FullscreenOverlay();
        }

        public void Initialize(GuiController uiController)
        {
            ui = uiController;
        }

        public void Start(Func<Mat, Mat> frameProcessor)
        {
            if (isRunning) return;
            isRunning = true;
            processFrame = frameProcessor;
            overlay.Show();
            processThread = new Thread(ProcessingLoop) { IsBackground = true, Name = "CensorProcessingThread" };
            processThread.Start();
        }



        public void Stop()
        {
            if (!isRunning) return;
            isRunning = false;
            processThread?.Join(1000);
            overlay.Hide();
        }

        public void UpdateSettings(CensorSettings newSettings)
        {
            settings = newSettings;
        }

        // ★★★ [수정] 스레드 충돌 원인 파악을 위해 try-catch 로깅 추가 ★★★
        private void ProcessingLoop()
        {
            while (isRunning)
            {
                try
                {
                    var frameStart = DateTime.Now;

                    using Mat rawFrame = capturer.GetFrame();
                    using Mat processedFrame = processFrame(rawFrame);

                    if (processedFrame != null)
                    {
                        overlay.UpdateFrame(processedFrame);
                    }

                    var elapsedMs = (DateTime.Now - frameStart).TotalMilliseconds;
                    int delay = (1000 / settings.TargetFPS) - (int)elapsedMs;
                    if (delay > 0) Thread.Sleep(delay);
                }
                catch (Exception ex)
                {
                    // 오류를 UI 로그와 메시지 박스로 표시
                    ui.LogMessage($"🚨 치명적 오류 발생 (백그라운드 스레드): {ex.Message}");
                    MessageBox.Show($"백그라운드 처리 중 심각한 오류가 발생했습니다. 프로그램을 중지합니다.\n\n오류: {ex.ToString()}", "치명적 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    
                    // isRunning을 false로 설정하여 스레드를 안전하게 종료
                    isRunning = false;
                }
            }
        }

        public void Dispose()
        {
            Stop();
            overlay?.Dispose();
        }
    }
}