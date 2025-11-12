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
        private CensorSettings settings = new(true, true, false, false, 15);
        private Func<Mat, Mat> processFrame;
        private readonly object disposeLock = new object();
        private bool isDisposed = false;

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
            lock (disposeLock)
            {
                if (isRunning || isDisposed) return;
                isRunning = true;
                processFrame = frameProcessor;
                
                try
                {
                    overlay.Show();
                    processThread = new Thread(ProcessingLoop) 
                        { IsBackground = true, Name = "CensorProcessingThread" };
                    processThread.Start();
                }
                catch (Exception ex)
                {
                    ui?.LogMessage($"🚨 시작 실패: {ex.Message}");
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
                
                if (processThread != null && processThread.IsAlive)
                {
                    processThread.Join(1000);
                }
                
                try { overlay?.Hide(); } catch { }
            }
        }

        public void UpdateSettings(CensorSettings newSettings)
        {
            settings = newSettings;
        }

        private void ProcessingLoop()
        {
            while (isRunning && !isDisposed)
            {
                try
                {
                    if (capturer == null || overlay == null)
                    {
                        ui?.LogMessage("⚠️ 캡처 또는 오버레이가 유효하지 않음 - 루프 종료");
                        break;
                    }

                    var frameStart = DateTime.Now;

                    Mat? rawFrame = null;
                    Mat? processedFrame = null;

                    try
                    {
                        rawFrame = capturer.GetFrame();

                        if (rawFrame != null && !rawFrame.IsDisposed && !rawFrame.Empty())
                        {
                            if (processFrame != null)
                            {
                                processedFrame = processFrame(rawFrame);

                                if (processedFrame != null && !processedFrame.IsDisposed)
                                {
                                    overlay.UpdateFrame(processedFrame);
                                }
                            }
                        }
                    }
                    finally
                    {
                        rawFrame?.Dispose();
                        processedFrame?.Dispose();
                    }

                    if (!isRunning || isDisposed) break;

                    var elapsedMs = (DateTime.Now - frameStart).TotalMilliseconds;
                    int delay = (1000 / settings.TargetFPS) - (int)elapsedMs;
                    if (delay > 0) Thread.Sleep(delay);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ui?.LogMessage($"🚨 치명적 오류 발생 (백그라운드 스레드): {ex.Message}");
                    if (!isRunning || isDisposed) break;
                    Thread.Sleep(100);
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
                
                try { capturer?.Dispose(); } catch { }
                try { overlay?.Dispose(); } catch { }
            }
        }
    }
}