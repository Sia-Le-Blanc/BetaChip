#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MosaicCensorSystem.Capture;
using MosaicCensorSystem.Detection;
using MosaicCensorSystem.Overlay;
using MosaicCensorSystem.UI;
using OpenCvSharp;

namespace MosaicCensorSystem
{
    public class CensorService : IDisposable
    {
        private class StickerInfo
        {
            public Mat Sticker { get; set; }
            public DateTime AssignedTime { get; set; }
        }

        private readonly GuiController ui;
        private readonly ScreenCapture capturer;
        public readonly MosaicProcessor processor;
        private readonly FullscreenOverlay overlay;
        private readonly Random random = new Random();

        private Thread processThread;
        private volatile bool isRunning = false;
        private int targetFPS = 15;
        private bool enableDetection = true;
        private bool enableCensoring = true;
        private bool enableStickers = false;

        private readonly List<Mat> squareStickers = new();
        private readonly List<Mat> wideStickers = new();
        private readonly Dictionary<int, StickerInfo> trackedStickers = new();

        public CensorService(GuiController uiController)
        {
            ui = uiController;
            capturer = new ScreenCapture();
            processor = new MosaicProcessor(Program.ONNX_MODEL_PATH);
            overlay = new FullscreenOverlay();
            LoadStickers();
        }

        private void LoadStickers()
        {
            string stickerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Stickers");
            if (!Directory.Exists(stickerPath)) { ui.LogMessage($"⚠️ 스티커 폴더 없음: {stickerPath}"); return; }
            
            var files = Directory.GetFiles(stickerPath, "*.png");
            foreach (var file in files)
            {
                using var sticker = Cv2.ImRead(file, ImreadModes.Unchanged);
                if (sticker.Empty()) continue;
                
                // 비율 기반 자동 분류
                float ratio = (float)sticker.Width / sticker.Height;
                if (ratio > 1.2f) wideStickers.Add(sticker.Clone());
                else squareStickers.Add(sticker.Clone());
            }
            ui.LogMessage($"✅ 스티커 로드: Square({squareStickers.Count}), Wide({wideStickers.Count})");
        }

        public void Start()
        {
            if (isRunning) return;
            if (!processor.IsModelLoaded())
            {
                ui.LogMessage("❌ 모델 파일 로드 실패.");
                MessageBox.Show("ONNX 모델 파일(best.onnx)을 찾을 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            isRunning = true;
            ui.SetRunningState(true);
            ui.UpdateStatus("🚀 시스템 실행 중...", Color.Green);
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
            ui.SetRunningState(false);
            ui.UpdateStatus("⭕ 시스템 대기 중", Color.Red);
        }

        private void ProcessingLoop()
        {
            while (isRunning)
            {
                var frameStart = DateTime.Now;
                using Mat frame = capturer.GetFrame();
                if (frame == null || frame.Empty()) { Thread.Sleep(30); continue; }
                using Mat displayFrame = frame.Clone();

                if (enableDetection)
                {
                    List<Detection.Detection> detections = processor.DetectObjects(frame);
                    foreach (var detection in detections)
                    {
                        // 1단계: 모자이크 적용
                        if (enableCensoring) processor.ApplySingleCensorOptimized(displayFrame, detection);

                        // 2단계: 스티커를 모자이크 위에 블렌딩
                        if (enableStickers && (squareStickers.Count > 0 || wideStickers.Count > 0))
                        {
                            // 스티커 할당/업데이트
                            if (!trackedStickers.TryGetValue(detection.TrackId, out var stickerInfo) || 
                                (DateTime.Now - stickerInfo.AssignedTime).TotalSeconds > 30)
                            {
                                var stickerList = (float)detection.Width / detection.Height > 1.2f ? wideStickers : squareStickers;
                                if (stickerList.Count > 0)
                                {
                                    trackedStickers[detection.TrackId] = new StickerInfo
                                    {
                                        Sticker = stickerList[random.Next(stickerList.Count)],
                                        AssignedTime = DateTime.Now
                                    };
                                }
                            }

                            // 스티커 블렌딩 (모자이크 위에)
                            if (trackedStickers.TryGetValue(detection.TrackId, out stickerInfo) && 
                                stickerInfo.Sticker != null && !stickerInfo.Sticker.IsDisposed)
                            {
                                BlendStickerOnMosaic(displayFrame, detection, stickerInfo.Sticker);
                            }
                        }
                    }
                }

                overlay.UpdateFrame(displayFrame);
                var elapsedMs = (DateTime.Now - frameStart).TotalMilliseconds;
                int delay = (1000 / targetFPS) - (int)elapsedMs;
                if (delay > 0) Thread.Sleep(delay);
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
                    // 알파 블렌딩으로 모자이크 위에 스티커 겹치기
                    Mat[] channels = null;
                    try
                    {
                        channels = Cv2.Split(resized);
                        using var stickerBgr = new Mat();
                        using var alpha = new Mat();
                        
                        // BGR 채널 병합
                        Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, stickerBgr);
                        
                        // 알파 채널을 0~1 범위로 정규화
                        channels[3].ConvertTo(alpha, MatType.CV_32F, 1.0/255.0);
                        
                        // 픽셀별 알파 블렌딩: result = mosaic * (1-alpha) + sticker * alpha
                        using var alphaBgr = new Mat();
                        using var invAlpha = new Mat();
                        using var mosaicFloat = new Mat();
                        using var stickerFloat = new Mat();
                        using var result = new Mat();
                        
                        Cv2.CvtColor(alpha, alphaBgr, ColorConversionCodes.GRAY2BGR);
                        Cv2.Subtract(Scalar.All(1.0), alphaBgr, invAlpha);
                        
                        frameRoi.ConvertTo(mosaicFloat, MatType.CV_32F);
                        stickerBgr.ConvertTo(stickerFloat, MatType.CV_32F);
                        
                        using var mosaicWeighted = new Mat();
                        using var stickerWeighted = new Mat();
                        
                        Cv2.Multiply(mosaicFloat, invAlpha, mosaicWeighted);
                        Cv2.Multiply(stickerFloat, alphaBgr, stickerWeighted);
                        Cv2.Add(mosaicWeighted, stickerWeighted, result);
                        
                        result.ConvertTo(frameRoi, MatType.CV_8U);
                    }
                    finally
                    {
                        if (channels != null)
                        {
                            foreach (var c in channels) c?.Dispose();
                        }
                    }
                }
                else if (resized.Channels() == 3) // BGR - 반투명 블렌딩
                {
                    // 모자이크 70% + 스티커 30%로 블렌딩 (모자이크가 더 강하게)
                    Cv2.AddWeighted(frameRoi, 0.7, resized, 0.3, 0, frameRoi);
                }
                else // 그레이스케일
                {
                    using var colorSticker = new Mat();
                    Cv2.CvtColor(resized, colorSticker, ColorConversionCodes.GRAY2BGR);
                    Cv2.AddWeighted(frameRoi, 0.7, colorSticker, 0.3, 0, frameRoi);
                }
            }
            catch
            {
                // 실시간 처리에서 오류 발생 시 조용히 무시
            }
        }

        public void UpdateSetting(string key, object value)
        {
            switch (key)
            {
                case "TargetFPS": targetFPS = (int)value; break;
                case "EnableDetection": enableDetection = (bool)value; break;
                case "EnableCensoring": enableCensoring = (bool)value; break;
                case "EnableStickers": 
                    enableStickers = (bool)value;
                    ui.LogMessage($"🎯 스티커 기능 {(enableStickers ? "활성화" : "비활성화")}");
                    break;
                case "CensorType": processor.SetCensorType((CensorType)value); break;
                case "Strength": processor.SetStrength((int)value); break;
                case "Confidence": processor.ConfThreshold = (float)value; break;
                case "Targets": processor.SetTargets((List<string>)value); break;
            }
        }

        public void TestCapture()
        {
            try
            {
                using Mat testFrame = capturer.GetFrame();
                if (testFrame != null && !testFrame.Empty())
                {
                    string testPath = Path.Combine(Environment.CurrentDirectory, "capture_test.jpg");
                    testFrame.SaveImage(testPath);
                    ui.LogMessage($"✅ 캡처 테스트 성공! 크기: {testFrame.Width}x{testFrame.Height}");
                    MessageBox.Show($"캡처 테스트 성공! 이미지가 {testPath}에 저장되었습니다.", "성공", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    ui.LogMessage($"❌ 캡처 테스트 실패: 빈 프레임이 반환되었습니다.");
                }
            }
            catch (Exception ex)
            {
                ui.LogMessage($"❌ 캡처 테스트 오류: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
            capturer?.Dispose();
            processor?.Dispose();
            overlay?.Dispose();
            foreach (var s in squareStickers) s.Dispose();
            foreach (var s in wideStickers) s.Dispose();
        }
    }
}