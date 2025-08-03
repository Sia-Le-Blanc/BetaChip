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
                if (Path.GetFileName(file).StartsWith("square")) squareStickers.Add(sticker.Clone());
                else if (Path.GetFileName(file).StartsWith("wide")) wideStickers.Add(sticker.Clone());
            }
            ui.LogMessage($"✅ 스티커 로드 완료: Square({squareStickers.Count}개), Wide({wideStickers.Count}개)");
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
                        if (enableCensoring) processor.ApplySingleCensorOptimized(displayFrame, detection);

                        if (enableStickers)
                        {
                            if (!trackedStickers.ContainsKey(detection.TrackId) || (DateTime.Now - trackedStickers[detection.TrackId].AssignedTime).TotalSeconds > 30)
                            {
                                float aspectRatio = (float)detection.Width / detection.Height;
                                var stickerList = aspectRatio > 1.2f ? wideStickers : squareStickers;
                                if (stickerList.Count > 0)
                                {
                                    trackedStickers[detection.TrackId] = new StickerInfo
                                    {
                                        Sticker = stickerList[random.Next(stickerList.Count)],
                                        AssignedTime = DateTime.Now
                                    };
                                }
                            }

                            if (trackedStickers.ContainsKey(detection.TrackId))
                            {
                                DrawSticker(displayFrame, detection, trackedStickers[detection.TrackId].Sticker);
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

        private void DrawSticker(Mat frame, Detection.Detection detection, Mat sticker)
        {
            if (sticker == null || sticker.IsDisposed) return;

            using Mat resizedSticker = new Mat();
            Cv2.Resize(sticker, resizedSticker, new OpenCvSharp.Size(detection.Width, detection.Height));

            var roi = new Rect(detection.BBox[0], detection.BBox[1], detection.Width, detection.Height);
            using Mat frameRoi = new Mat(frame, roi);

            var channels = Cv2.Split(resizedSticker);
            if (channels.Length < 4)
            {
                foreach (var c in channels) c.Dispose();
                return;
            }

            var (stickerBgr, mask) = (new Mat(), channels[3]);
            Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, stickerBgr);
            stickerBgr.CopyTo(frameRoi, mask);

            stickerBgr.Dispose(); mask.Dispose();
            foreach (var c in channels) c.Dispose();
        }

        public void UpdateSetting(string key, object value)
        {
            switch (key)
            {
                case "TargetFPS": targetFPS = (int)value; break;
                case "EnableDetection": enableDetection = (bool)value; break;
                case "EnableCensoring": enableCensoring = (bool)value; break;
                case "EnableStickers": enableStickers = (bool)value; break;
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