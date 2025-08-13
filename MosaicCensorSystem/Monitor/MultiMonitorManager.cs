using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MosaicCensorSystem.Overlay;
using OpenCvSharp;

namespace MosaicCensorSystem.Monitor
{
    public class MonitorInfo
    {
        public int Index { get; set; }
        public System.Drawing.Rectangle Bounds { get; set; }
        public bool IsEnabled { get; set; } = true;
        public FullscreenOverlay Overlay { get; set; }
        public string DeviceName { get; set; }
    }

    public class MultiMonitorManager : IDisposable
    {
        private readonly List<MonitorInfo> monitors = new();
        private readonly System.Drawing.Rectangle virtualScreenBounds;

        public IReadOnlyList<MonitorInfo> Monitors => monitors.AsReadOnly();

        public MultiMonitorManager()
        {
            DetectMonitors();
            virtualScreenBounds = SystemInformation.VirtualScreen;
            Console.WriteLine($"가상 데스크톱 크기: {virtualScreenBounds.Width}x{virtualScreenBounds.Height}");
        }

        private void DetectMonitors()
        {
            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                var screen = Screen.AllScreens[i];
                var monitor = new MonitorInfo
                {
                    Index = i,
                    Bounds = screen.Bounds,
                    DeviceName = screen.DeviceName,
                    Overlay = new FullscreenOverlay()
                };
                
                // 각 모니터별 오버레이 설정
                monitor.Overlay.SetMonitorBounds(screen.Bounds.X, screen.Bounds.Y, 
                                        screen.Bounds.Width, screen.Bounds.Height);
                monitors.Add(monitor);
                
                Console.WriteLine($"모니터 {i}: {screen.Bounds.Width}x{screen.Bounds.Height} at ({screen.Bounds.X}, {screen.Bounds.Y})");
            }
        }

        public void ShowOverlays()
        {
            foreach (var monitor in monitors.Where(m => m.IsEnabled))
            {
                monitor.Overlay.Show();
            }
        }

        public void HideOverlays()
        {
            foreach (var monitor in monitors)
            {
                monitor.Overlay.Hide();
            }
        }

        public void UpdateFrames(Mat fullFrame)
        {
            if (fullFrame == null || fullFrame.Empty()) return;

            foreach (var monitor in monitors.Where(m => m.IsEnabled))
            {
                // 각 모니터 영역에 해당하는 프레임 부분 추출
                var roi = new Rect(
                    monitor.Bounds.X - virtualScreenBounds.X,
                    monitor.Bounds.Y - virtualScreenBounds.Y,
                    monitor.Bounds.Width,
                    monitor.Bounds.Height
                );

                // ROI가 프레임 범위 내에 있는지 확인
                if (roi.X >= 0 && roi.Y >= 0 && 
                    roi.X + roi.Width <= fullFrame.Width && 
                    roi.Y + roi.Height <= fullFrame.Height)
                {
                    using var monitorFrame = new Mat(fullFrame, roi);
                    monitor.Overlay.UpdateFrame(monitorFrame);
                }
            }
        }

        public void SetMonitorEnabled(int index, bool enabled)
        {
            if (index >= 0 && index < monitors.Count)
            {
                monitors[index].IsEnabled = enabled;
                if (!enabled)
                    monitors[index].Overlay.Hide();
                else if (enabled)
                    monitors[index].Overlay.Show();
            }
        }

        // 감지된 객체의 좌표를 모니터별 상대 좌표로 변환
        public (int monitorIndex, int x, int y) ConvertToMonitorCoords(int globalX, int globalY)
        {
            for (int i = 0; i < monitors.Count; i++)
            {
                var bounds = monitors[i].Bounds;
                if (globalX >= bounds.Left && globalX < bounds.Right &&
                    globalY >= bounds.Top && globalY < bounds.Bottom)
                {
                    return (i, globalX - bounds.Left, globalY - bounds.Top);
                }
            }
            return (-1, globalX, globalY); // 주 모니터 기본값
        }

        public void Dispose()
        {
            foreach (var monitor in monitors)
            {
                monitor.Overlay?.Dispose();
            }
            monitors.Clear();
        }
    }
}