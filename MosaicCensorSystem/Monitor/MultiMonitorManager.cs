using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MosaicCensorSystem.Capture;
using MosaicCensorSystem.Detection;
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
        
        // ★★★ 새로 추가: 개별 모니터 캡처 및 처리 ★★★
        public ScreenCapture IndividualCapturer { get; set; }
        public Thread ProcessingThread { get; set; }
        public volatile bool IsProcessing = false;
        public MosaicProcessor Processor { get; set; }
    }

    public class MultiMonitorManager : IDisposable
    {
        private readonly List<MonitorInfo> monitors = new();
        private readonly System.Drawing.Rectangle virtualScreenBounds;
        private readonly Random random = new Random();

        // ★★★ 스티커 관련 (CensorService에서 전달받을 예정) ★★★
        private List<Mat> squareStickers = new List<Mat>();
        private List<Mat> wideStickers = new List<Mat>();
        private readonly Dictionary<int, StickerInfo> trackedStickers = new();

        // ★★★ 설정값들 ★★★
        private bool enableDetection = true;
        private bool enableCensoring = true;
        private bool enableStickers = false;
        private int targetFPS = 15;

        public IReadOnlyList<MonitorInfo> Monitors => monitors.AsReadOnly();

        // ★★★ 스티커 정보 클래스 ★★★
        private class StickerInfo
        {
            public Mat Sticker { get; set; }
            public DateTime AssignedTime { get; set; }
        }

        public MultiMonitorManager()
        {
            DetectMonitors();
            virtualScreenBounds = SystemInformation.VirtualScreen;
            Console.WriteLine($"진정한 멀티모니터 매니저 초기화 - 가상 데스크톱 크기: {virtualScreenBounds.Width}x{virtualScreenBounds.Height}");
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
                    Overlay = new FullscreenOverlay(),
                    // ★★★ 핵심: 각 모니터별로 개별 캡처러 생성 ★★★
                    IndividualCapturer = new ScreenCapture(screen.Bounds),
                    // ★★★ 각 모니터별 프로세서는 나중에 설정 ★★★
                    Processor = null
                };
                
                // 각 모니터별 오버레이 설정
                monitor.Overlay.SetMonitorBounds(screen.Bounds.X, screen.Bounds.Y, 
                                        screen.Bounds.Width, screen.Bounds.Height);
                monitors.Add(monitor);
                
                Console.WriteLine($"모니터 {i}: {screen.Bounds.Width}x{screen.Bounds.Height} at ({screen.Bounds.X}, {screen.Bounds.Y}) - 개별 캡처 준비됨");
            }
        }

        // ★★★ CensorService에서 프로세서와 스티커를 설정하는 메서드 ★★★
        public void Initialize(MosaicProcessor sharedProcessor, List<Mat> squares, List<Mat> wides)
        {
            squareStickers = squares ?? new List<Mat>();
            wideStickers = wides ?? new List<Mat>();

            // 각 모니터에 프로세서 할당 (공유 프로세서 사용)
            foreach (var monitor in monitors)
            {
                monitor.Processor = sharedProcessor;
            }
            
            Console.WriteLine($"멀티모니터 초기화 완료 - 스티커: Square({squareStickers.Count}), Wide({wideStickers.Count})");
        }

        public void ShowOverlays()
        {
            foreach (var monitor in monitors.Where(m => m.IsEnabled))
            {
                monitor.Overlay.Show();
                StartMonitorProcessing(monitor);
            }
        }

        public void HideOverlays()
        {
            foreach (var monitor in monitors)
            {
                StopMonitorProcessing(monitor);
                monitor.Overlay.Hide();
            }
        }

        // ★★★ 각 모니터별 개별 처리 시작 ★★★
        private void StartMonitorProcessing(MonitorInfo monitor)
        {
            if (monitor.IsProcessing) return;
            
            monitor.IsProcessing = true;
            monitor.ProcessingThread = new Thread(() => MonitorProcessingLoop(monitor))
            {
                IsBackground = true,
                Name = $"Monitor{monitor.Index}ProcessingThread"
            };
            monitor.ProcessingThread.Start();
            Console.WriteLine($"모니터 {monitor.Index} 개별 처리 시작");
        }

        private void StopMonitorProcessing(MonitorInfo monitor)
        {
            if (!monitor.IsProcessing) return;

            monitor.IsProcessing = false;
            monitor.ProcessingThread?.Join(1000);
            monitor.ProcessingThread = null;
            Console.WriteLine($"모니터 {monitor.Index} 개별 처리 중지");
        }

        // ★★★ 각 모니터별 독립적인 처리 루프 ★★★
        private void MonitorProcessingLoop(MonitorInfo monitor)
        {
            while (monitor.IsProcessing)
            {
                try
                {
                    var frameStart = DateTime.Now;
                    
                    // ★★★ 해당 모니터만 개별 캡처 ★★★
                    using Mat individualFrame = monitor.IndividualCapturer.GetFrame();
                    if (individualFrame == null || individualFrame.Empty()) 
                    {
                        Thread.Sleep(30); 
                        continue; 
                    }

                    using Mat processedFrame = individualFrame.Clone();

                    if (enableDetection && monitor.Processor != null)
                    {
                        // ★★★ 해당 모니터 화면에서만 검출 ★★★
                        List<Detection.Detection> detections = monitor.Processor.DetectObjects(individualFrame);
                        
                        foreach (var detection in detections)
                        {
                            // 1단계: 검열 적용
                            if (enableCensoring) 
                            {
                                monitor.Processor.ApplySingleCensorOptimized(processedFrame, detection);
                            }

                            // 2단계: 스티커 적용 (후원자 기능)
                            if (enableStickers && (squareStickers.Count > 0 || wideStickers.Count > 0))
                            {
                                ApplyStickerToDetection(processedFrame, detection, monitor.Index);
                            }
                        }
                    }

                    // ★★★ 해당 모니터의 오버레이에만 표시 ★★★
                    monitor.Overlay.UpdateFrame(processedFrame);
                    
                    // FPS 제어
                    var elapsedMs = (DateTime.Now - frameStart).TotalMilliseconds;
                    int delay = (1000 / targetFPS) - (int)elapsedMs;
                    if (delay > 0) Thread.Sleep(delay);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"모니터 {monitor.Index} 처리 오류: {ex.Message}");
                    Thread.Sleep(100); // 오류시 잠시 대기
                }
            }
        }

        private void ApplyStickerToDetection(Mat frame, Detection.Detection detection, int monitorIndex)
        {
            try
            {
                // ★★★ 트래킹 ID에 모니터 정보 포함 ★★★
                int uniqueTrackId = (monitorIndex * 10000) + detection.TrackId;

                // 스티커 할당/업데이트
                if (!trackedStickers.TryGetValue(uniqueTrackId, out var stickerInfo) || 
                    (DateTime.Now - stickerInfo.AssignedTime).TotalSeconds > 30)
                {
                    var stickerList = (float)detection.Width / detection.Height > 1.2f ? wideStickers : squareStickers;
                    if (stickerList.Count > 0)
                    {
                        trackedStickers[uniqueTrackId] = new StickerInfo
                        {
                            Sticker = stickerList[random.Next(stickerList.Count)],
                            AssignedTime = DateTime.Now
                        };
                    }
                }

                // 스티커 블렌딩 (모자이크 위에)
                if (trackedStickers.TryGetValue(uniqueTrackId, out stickerInfo) && 
                    stickerInfo.Sticker != null && !stickerInfo.Sticker.IsDisposed)
                {
                    BlendStickerOnMosaic(frame, detection, stickerInfo.Sticker);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"모니터 {monitorIndex} 스티커 적용 오류: {ex.Message}");
            }
        }

        private void BlendStickerOnMosaic(Mat frame, Detection.Detection detection, Mat sticker)
        {
            try
            {
                // 안전한 범위 체크
                int x = Math.Max(0, detection.BBox[0]);
                int y = Math.Max(0, detection.BBox[1]);
                int w = Math.Min(detection.Width, frame.Width - x);
                int h = Math.Min(detection.Height, frame.Height - y);
                
                if (w <= 10 || h <= 10) return;

                // 스티커 크기 조정
                using var resized = new Mat();
                Cv2.Resize(sticker, resized, new OpenCvSharp.Size(w, h), interpolation: InterpolationFlags.Area);
                
                // ROI 설정 (모자이크된 영역)
                var roi = new Rect(x, y, w, h);
                using var frameRoi = new Mat(frame, roi);
                
                if (resized.Channels() == 4) // BGRA - 알파 채널 있음
                {
                    using var frameRoiBgr = new Mat();
                    Cv2.CvtColor(frameRoi, frameRoiBgr, ColorConversionCodes.BGRA2BGR);
                    Mat[]? channels = null;
                    try
                    {
                        channels = Cv2.Split(resized);
                        using var stickerBgr = new Mat();
                        using var alpha = new Mat();
                        
                        Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, stickerBgr);
                        channels[3].ConvertTo(alpha, MatType.CV_32F, 1.0/255.0);
                        
                        using var alphaBgr = new Mat();
                        using var invAlpha = new Mat();
                        using var mosaicFloat = new Mat();
                        using var stickerFloat = new Mat();
                        using var result = new Mat();
                        
                        Cv2.CvtColor(alpha, alphaBgr, ColorConversionCodes.GRAY2BGR);
                        Cv2.Subtract(Scalar.All(1.0), alphaBgr, invAlpha);
                        frameRoiBgr.ConvertTo(mosaicFloat, MatType.CV_32F);
                        stickerBgr.ConvertTo(stickerFloat, MatType.CV_32F);
                        
                        using var mosaicWeighted = new Mat();
                        using var stickerWeighted = new Mat();
                        
                        Cv2.Multiply(mosaicFloat, invAlpha, mosaicWeighted);
                        Cv2.Multiply(stickerFloat, alphaBgr, stickerWeighted);
                        Cv2.Add(mosaicWeighted, stickerWeighted, result);
                        
                        using var result8u = new Mat();
                        result.ConvertTo(result8u, MatType.CV_8U);
                        Cv2.CvtColor(result8u, frameRoi, ColorConversionCodes.BGR2BGRA);
                    }
                    finally
                    {
                        if (channels != null)
                        {
                            foreach (var c in channels) 
                            {
                                c?.Dispose();
                            }
                        }
                    }
                }
                else if (resized.Channels() == 3)
                {
                    Cv2.AddWeighted(frameRoi, 0.7, resized, 0.3, 0, frameRoi);
                }
                else 
                {
                    using var colorSticker = new Mat();
                    Cv2.CvtColor(resized, colorSticker, ColorConversionCodes.GRAY2BGR);
                    Cv2.AddWeighted(frameRoi, 0.7, colorSticker, 0.3, 0, frameRoi);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"스티커 블렌딩 오류: {ex.Message}");
            }
        }

        // ★★★ 기존 UpdateFrames 메서드는 이제 사용하지 않음 ★★★
        public void UpdateFrames(Mat fullFrame)
        {
            // 개별 처리 방식에서는 이 메서드를 사용하지 않음
            // 각 모니터가 독립적으로 캡처하고 처리함
        }

        // ★★★ 설정 업데이트 메서드들 ★★★
        public void UpdateSettings(bool detection, bool censoring, bool stickers, int fps)
        {
            enableDetection = detection;
            enableCensoring = censoring;
            enableStickers = stickers;
            targetFPS = Math.Max(5, Math.Min(60, fps));
        }

        public void SetMonitorEnabled(int index, bool enabled)
        {
            if (index >= 0 && index < monitors.Count)
            {
                var monitor = monitors[index];
                monitor.IsEnabled = enabled;
                
                if (!enabled)
                {
                    StopMonitorProcessing(monitor);
                    monitor.Overlay.Hide();
                }
                else if (enabled)
                {
                    monitor.Overlay.Show();
                    StartMonitorProcessing(monitor);
                }
            }
        }

        public void Dispose()
        {
            foreach (var monitor in monitors)
            {
                StopMonitorProcessing(monitor);
                monitor.Overlay?.Dispose();
                monitor.IndividualCapturer?.Dispose();
            }
            monitors.Clear();
        }
    }
}