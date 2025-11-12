// MultiMonitorManager.cs
using MosaicCensorSystem.Capture;
using MosaicCensorSystem.UI;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MosaicCensorSystem.Overlay;

namespace MosaicCensorSystem.Management
{
    public class MultiMonitorManager : IOverlayManager
    {
        private class MonitorTask
        {
            public ScreenCapture Capturer { get; }
            public FullscreenOverlay Overlay { get; }
            public Thread ProcessThread { get; set; }
            public bool IsEnabled { get; set; } = true;
            private volatile bool isDisposed = false;

            public MonitorTask(Rectangle bounds)
            {
                Capturer = new ScreenCapture(bounds);
                Overlay = new FullscreenOverlay(bounds);
            }

            public bool IsValid()
            {
                return !isDisposed && Capturer != null && Overlay != null;
            }

            public void Dispose()
            {
                if (isDisposed) return;
                isDisposed = true;
                
                try { Capturer?.Dispose(); } catch { }
                try { Overlay?.Dispose(); } catch { }
            }
        }
        
        private GuiController ui;
        private readonly List<MonitorTask> monitorTasks = new List<MonitorTask>();
        private volatile bool isRunning = false;
        private CensorSettings settings = new(true, true, false, true, 15);
        private Func<Mat, Mat> processFrame;
        private readonly object disposeLock = new object();
        private bool isDisposed = false;

        public IReadOnlyList<Screen> Monitors { get; }

        public MultiMonitorManager(ScreenCapture mainCapturerToDetectMonitors)
        {
            Monitors = Screen.AllScreens.ToList().AsReadOnly();
            foreach (var screen in Monitors)
            {
                monitorTasks.Add(new MonitorTask(screen.Bounds));
            }
        }

        public void Initialize(GuiController uiController)
        {
            ui = uiController;
            ui.LogMessage($"감지된 모니터 수: {Monitors.Count}");
            for (int i = 0; i < Monitors.Count; i++)
            {
                ui.LogMessage($"   모니터 {i + 1}: {Monitors[i].Bounds.Width}x{Monitors[i].Bounds.Height} at {Monitors[i].Bounds.Location}");
            }
        }

        public void Start(Func<Mat, Mat> frameProcessor)
        {
            lock (disposeLock)
            {
                if (isRunning || isDisposed) return;
                isRunning = true;
                processFrame = frameProcessor;
                
                foreach (var task in monitorTasks.Where(t => t.IsEnabled))
                {
                    if (!task.IsValid()) continue;
                    
                    try
                    {
                        task.Overlay.Show();
                        task.ProcessThread = new Thread(() => ProcessingLoop(task)) 
                            { IsBackground = true, Name = $"Monitor_{monitorTasks.IndexOf(task)}_Thread" };
                        task.ProcessThread.Start();
                    }
                    catch (Exception ex)
                    {
                        ui?.LogMessage($"🚨 모니터 {monitorTasks.IndexOf(task)} 시작 실패: {ex.Message}");
                    }
                }
            }
        }

        public void Stop()
        {
            lock (disposeLock)
            {
                if (!isRunning) return;
                isRunning = false;
                
                foreach (var task in monitorTasks)
                {
                    if (task.ProcessThread != null && task.ProcessThread.IsAlive)
                    {
                        task.ProcessThread.Join(1000);
                    }
                    
                    if (task.IsValid())
                    {
                        try { task.Overlay.Hide(); } catch { }
                    }
                }
            }
        }
        
        public void SetMonitorEnabled(int index, bool enabled)
        {
            if (index < 0 || index >= monitorTasks.Count) return;
            monitorTasks[index].IsEnabled = enabled;
            ui?.LogMessage($"모니터 {index + 1} {(enabled ? "활성화" : "비활성화")}");
        }

        public void UpdateSettings(CensorSettings newSettings)
        {
            settings = newSettings;
        }

        private void ProcessingLoop(MonitorTask task)
        {
            if (task == null) return;

            while (isRunning && task.IsEnabled && !isDisposed)
            {
                try
                {
                    if (!task.IsValid())
                    {
                        ui?.LogMessage("⚠️ 모니터 작업이 유효하지 않음 - 루프 종료");
                        break;
                    }

                    var frameStart = DateTime.Now;

                    Mat? rawFrame = null;
                    Mat? processedFrame = null;

                    try
                    {
                        rawFrame = task.Capturer?.GetFrame();
                        
                        if (rawFrame != null && !rawFrame.IsDisposed && !rawFrame.Empty())
                        {
                            if (processFrame != null)
                            {
                                processedFrame = processFrame(rawFrame);

                                if (processedFrame != null && !processedFrame.IsDisposed && task.IsValid())
                                {
                                    task.Overlay?.UpdateFrame(processedFrame);
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
                    int targetFps = Math.Max(1, settings?.TargetFPS ?? 15);
                    int delay = (1000 / targetFps) - (int)elapsedMs;
                    if (delay > 0) Thread.Sleep(delay);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    ui?.LogMessage($"🚨 모니터 처리 루프 오류: {ex.Message}");
                    if (!isRunning || isDisposed) break;
                    Thread.Sleep(100);
                }
            }

            if (task.IsValid())
            {
                try { task.Overlay?.Hide(); } catch { }
            }
        }

        public void Dispose()
        {
            lock (disposeLock)
            {
                if (isDisposed) return;
                isDisposed = true;
                
                Stop();

                foreach (var task in monitorTasks)
                {
                    task?.Dispose();
                }
                
                monitorTasks.Clear();
            }
        }
    }
}