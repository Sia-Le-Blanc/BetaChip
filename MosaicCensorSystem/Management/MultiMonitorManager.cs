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

namespace MosaicCensorSystem.Management // 네임스페이스 변경
{
    /// <summary>
    /// 후원자 버전을 위한 다중 모니터 오버레이 관리자
    /// </summary>
    public class MultiMonitorManager : IOverlayManager
    {
        private class MonitorTask
        {
            public ScreenCapture Capturer { get; }
            public FullscreenOverlay Overlay { get; }
            public Thread ProcessThread { get; set; }
            public bool IsEnabled { get; set; } = true;

            public MonitorTask(Rectangle bounds)
            {
                Capturer = new ScreenCapture(bounds);
                Overlay = new FullscreenOverlay(bounds);
            }
        }
        
        private GuiController ui;
        private readonly List<MonitorTask> monitorTasks = new List<MonitorTask>();
        private volatile bool isRunning = false;
        private CensorSettings settings;
        private Func<Mat, Mat> processFrame;

        public IReadOnlyList<Screen> Monitors { get; }

        public MultiMonitorManager(ScreenCapture mainCapturerToDetectMonitors)
        {
             // Screen.AllScreens를 사용하여 모든 모니터 정보를 가져옵니다.
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
            if (isRunning) return;
            isRunning = true;
            processFrame = frameProcessor;
            
            foreach (var task in monitorTasks.Where(t => t.IsEnabled))
            {
                task.Overlay.Show();
                task.ProcessThread = new Thread(() => ProcessingLoop(task)) 
                    { IsBackground = true, Name = $"Monitor_{monitorTasks.IndexOf(task)}_Thread" };
                task.ProcessThread.Start();
            }
        }

        public void Stop()
        {
            if (!isRunning) return;
            isRunning = false;
            
            foreach (var task in monitorTasks)
            {
                task.ProcessThread?.Join(500);
                task.Overlay.Hide();
            }
        }
        
        public void SetMonitorEnabled(int index, bool enabled)
        {
            if (index < 0 || index >= monitorTasks.Count) return;
            monitorTasks[index].IsEnabled = enabled;
            
            // 이미 실행 중이라면 해당 모니터의 스레드를 즉시 중지/시작할 수도 있습니다.
            // (여기서는 단순화를 위해 다음 Start/Stop 시에만 적용)
            ui.LogMessage($"모니터 {index + 1} {(enabled ? "활성화" : "비활성화")}");
        }

        public void UpdateSettings(CensorSettings newSettings)
        {
            settings = newSettings;
        }

        private void ProcessingLoop(MonitorTask task)
        {
            while (isRunning && task.IsEnabled)
            {
                var frameStart = DateTime.Now;

                using Mat rawFrame = task.Capturer.GetFrame();
                using Mat processedFrame = processFrame(rawFrame);

                if (processedFrame != null)
                {
                    task.Overlay.UpdateFrame(processedFrame);
                }

                var elapsedMs = (DateTime.Now - frameStart).TotalMilliseconds;
                int delay = (1000 / settings.TargetFPS) - (int)elapsedMs;
                if (delay > 0) Thread.Sleep(delay);
            }
            // 루프가 끝나면 오버레이를 숨겨줍니다.
            task.Overlay.Hide();
        }

        public void Dispose()
        {
            Stop();
            foreach (var task in monitorTasks)
            {
                task.Capturer?.Dispose();
                task.Overlay?.Dispose();
            }
        }
    }
}