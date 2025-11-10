// SingleMonitorManager.cs
using MosaicCensorSystem.Capture;
using MosaicCensorSystem.Overlay;
using MosaicCensorSystem.UI;
using OpenCvSharp;
using System;
using System.Threading;
using System.Windows.Forms;

namespace MosaicCensorSystem.Management
{
    public class SingleMonitorManager : IOverlayManager
    {
        private GuiController ui;
        private readonly ScreenCapture capturer;
        private readonly FullscreenOverlay overlay;
        private Thread processThread;
        private volatile bool isRunning = false;
        private CensorSettings settings = new(true, true, false, false, 15); // ★ 기본값으로 초기화
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
                    ui?.LogMessage($"🚨 치명적 오류 발생 (백그라운드 스레드): {ex.Message}");
                    MessageBox.Show($"백그라운드 처리 중 심각한 오류가 발생했습니다. 프로그램을 중지합니다.\n\n오류: {ex.ToString()}", "치명적 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
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