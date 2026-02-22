#nullable disable
using MosaicCensorSystem.Capture;
using MosaicCensorSystem.Detection;
using MosaicCensorSystem.Management;
using MosaicCensorSystem.Overlay;
using MosaicCensorSystem.UI;
using MosaicCensorSystem.Models; // 추가됨
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

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
        private readonly MosaicProcessor processor;
        private readonly Random random = new Random();
        private readonly IOverlayManager overlayManager;
        private readonly OverlayTextManager overlayTextManager; // 항상 선언
        private readonly SubscriptionInfo _subInfo; // 유저 등급 정보 저장

        public MosaicProcessor Processor => processor;
        
        private CensorSettings currentSettings = new(true, true, false, true, 15);

        private readonly List<Mat> squareStickers = new();
        private readonly List<Mat> wideStickers = new();
        private readonly Dictionary<int, StickerInfo> trackedStickers = new();
        private const int STICKER_CLEANUP_INTERVAL_SECONDS = 30;
        private DateTime lastStickerCleanup = DateTime.Now;

        private static readonly string SCREENSHOTS_FOLDER = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BetaChip Screenshots");
        private static readonly string DESKTOP_SHORTCUT = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "BetaChip 스크린샷.lnk");

        private bool disposed = false;

        public CensorService(GuiController uiController, SubscriptionInfo subInfo)
        {
            ui = uiController;
            _subInfo = subInfo; // 등급 정보 주입받음
            capturer = new ScreenCapture();
            processor = new MosaicProcessor(Program.STANDARD_MODEL_PATH);
            processor.LogCallback = ui.LogMessage;

            // 등급에 따라 매니저 결정 (Patreon 이상이면 멀티모니터)
            if (_subInfo.Tier == "plus" || _subInfo.Tier == "patreon")
            {
                overlayManager = new MultiMonitorManager(capturer);
                ui.LogMessage($"🖥️ [{_subInfo.Tier.ToUpper()}] 등급 확인: 멀티 모니터 관리자 활성화!");
            }
            else
            {
                overlayManager = new SingleMonitorManager(capturer);
                ui.LogMessage("🖥️ [FREE] 등급 확인: 단일 모니터 관리자 활성화");
            }

            overlayManager.Initialize(ui);
            overlayManager.UpdateSettings(currentSettings);

            // Plus 등급이면 캡션 기능 활성화
            if (_subInfo.Tier == "plus")
            {
                overlayTextManager = new OverlayTextManager((msg) => ui.LogMessage(msg));
                ui.LogMessage("✨ [PLUS] 등급 확인: 캡션 기능 활성화!");
            }

            SetupScreenshotFolder();
            LoadStickers();
            WarmupModelAsync();
        }

        private void WarmupModelAsync()
        {
            if (processor.IsModelLoaded())
            {
                ui.LogMessage("🔥 모델 워밍업을 시작합니다... (백그라운드)");
                Task.Run(() =>
                {
                    processor.WarmUpModel();
                    ui.LogMessage("✅ 모델 워밍업 완료.");
                });
            }
        }

        public void Start()
        {
            if (!processor.IsModelLoaded())
            {
                ui.LogMessage("❌ 모델 파일 로드 실패.");
                MessageBox.Show("ONNX 모델 파일(best.onnx)을 찾을 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ui.SetRunningState(true);
            ui.UpdateStatus("🚀 시스템 실행 중...", Color.Green);
            overlayManager.Start(ProcessFrame);
        }

        public void Stop()
        {
            overlayManager.Stop();
            ui.SetRunningState(false);
            ui.UpdateStatus("⭕ 시스템 대기 중", Color.Red);
        }

        private Mat ProcessFrame(Mat rawFrame)
        {
            if (rawFrame == null || rawFrame.IsDisposed || rawFrame.Empty())
            {
                overlayTextManager?.Update(false);
                return null;
            }

            Mat processedFrame = null;
            try
            {
                processedFrame = rawFrame.Clone();

                if (!currentSettings.EnableDetection)
                {
                    overlayTextManager?.Update(false);
                    return processedFrame;
                }

                List<Detection.Detection> detections = processor.DetectObjects(rawFrame);
                bool detectionActive = detections != null && detections.Count > 0;
                
                // 캡션 기능 등급 체크
                if (currentSettings.EnableCaptions && _subInfo.Tier == "plus")
                {
                    overlayTextManager?.Update(detectionActive);
                }
                else
                {
                    overlayTextManager?.Update(false);
                }

                foreach (var detection in detections)
                {
                    if (currentSettings.EnableCensoring)
                    {
                        processor.ApplySingleCensorOptimized(processedFrame, detection);
                    }

                    // 스티커 기능 등급 체크 (Patreon 이상)
                    bool canUseStickers = _subInfo.Tier == "patreon" || _subInfo.Tier == "plus";
                    if (canUseStickers && currentSettings.EnableStickers && (squareStickers.Count > 0 || wideStickers.Count > 0))
                    {
                        if (!trackedStickers.TryGetValue(detection.TrackId, out var stickerInfo) || 
                            (DateTime.Now - stickerInfo.AssignedTime).TotalSeconds > 30)
                        {
                            var stickerList = (float)detection.Width / detection.Height > 1.2f ? wideStickers : squareStickers;
                            if (stickerList.Count > 0)
                            {
                                stickerInfo = new StickerInfo { 
                                    Sticker = stickerList[random.Next(stickerList.Count)], 
                                    AssignedTime = DateTime.Now 
                                };
                                trackedStickers[detection.TrackId] = stickerInfo;
                            }
                        }
                        
                        if (stickerInfo?.Sticker != null && !stickerInfo.Sticker.IsDisposed)
                        {
                            BlendStickerOnMosaic(processedFrame, detection, stickerInfo.Sticker);
                        }
                    }
                }

                if ((DateTime.Now - lastStickerCleanup).TotalSeconds > STICKER_CLEANUP_INTERVAL_SECONDS)
                {
                    CleanupExpiredStickerTracking();
                    lastStickerCleanup = DateTime.Now;
                }

                if (detectionActive && currentSettings.EnableCaptions && _subInfo.Tier == "plus" &&
                    processedFrame != null && !processedFrame.IsDisposed)
                {
                    overlayTextManager?.DrawOverlayOnFrame(processedFrame);
                }

                return processedFrame;
            }
            catch (Exception ex)
            {
                ui.LogMessage($"❌ ProcessFrame 오류: {ex.Message}");
                processedFrame?.Dispose();
                return null;
            }
        }

        private void CleanupExpiredStickerTracking()
        {
            try
            {
                var expiredIds = trackedStickers
                    .Where(kvp => (DateTime.Now - kvp.Value.AssignedTime).TotalSeconds > 30)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var id in expiredIds)
                {
                    trackedStickers.Remove(id);
                }
            }
            catch (Exception ex)
            {
                ui.LogMessage($"⚠️ 스티커 추적 정리 중 오류: {ex.Message}");
            }
        }

        public void CaptureAndSave()
        {
            try
            {
                ui.LogMessage("📸 검열된 화면 캡처 시작...");
                using Mat rawFrame = capturer.GetFrame();
                using Mat processedFrame = ProcessFrame(rawFrame);

                if (processedFrame == null)
                {
                    ui.LogMessage("❌ 화면 캡처 실패: 빈 프레임이 반환되었습니다.");
                    return;
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"BetaChip_{timestamp}.jpg";
                string filePath = Path.Combine(SCREENSHOTS_FOLDER, fileName);

                processedFrame.SaveImage(filePath);
                
                ui.LogMessage($"✅ 캡처 저장 완료! 파일: {fileName}");
                MessageBox.Show($"검열된 스크린샷이 저장되었습니다!\n\n파일명: {fileName}", "캡처 저장 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ui.LogMessage($"❌ 캡처 저장 중 오류: {ex.Message}");
            }
        }
        
        public void UpdateSetting(string key, object value)
        {
            bool settingsChanged = false;
            switch (key)
            {
                case nameof(CensorSettings.TargetFPS): 
                    currentSettings = currentSettings with { TargetFPS = (int)value }; 
                    settingsChanged = true; 
                    break;
                case nameof(CensorSettings.EnableDetection): 
                    currentSettings = currentSettings with { EnableDetection = (bool)value }; 
                    settingsChanged = true; 
                    break;
                case nameof(CensorSettings.EnableCensoring): 
                    currentSettings = currentSettings with { EnableCensoring = (bool)value }; 
                    settingsChanged = true; 
                    break;
                case nameof(CensorSettings.EnableStickers):
                    currentSettings = currentSettings with { EnableStickers = (bool)value };
                    settingsChanged = true;
                    break;
                case nameof(CensorSettings.EnableCaptions):
                    currentSettings = currentSettings with { EnableCaptions = (bool)value };
                    settingsChanged = true;
                    break;
                case "CensorType": 
                    processor.SetCensorType((CensorType)value); 
                    break;
                case "Strength": 
                    processor.SetStrength((int)value); 
                    break;
                case "Confidence": 
                    processor.ConfThreshold = (float)value; 
                    break;
                case "Targets": 
                    processor.SetTargets((List<string>)value); 
                    break;
            }

            if (settingsChanged)
            {
                overlayManager.UpdateSettings(currentSettings);
            }
        }
        
        private void BlendStickerOnMosaic(Mat frame, Detection.Detection detection, Mat sticker)
        {
            try
            {
                int x = Math.Max(0, detection.BBox[0]);
                int y = Math.Max(0, detection.BBox[1]);
                int w = Math.Min(detection.Width, frame.Width - x);
                int h = Math.Min(detection.Height, frame.Height - y);
                if (w <= 10 || h <= 10) return;

                using var resized = new Mat();
                Cv2.Resize(sticker, resized, new OpenCvSharp.Size(w, h), interpolation: InterpolationFlags.Area);
                using var frameRoi = new Mat(frame, new Rect(x, y, w, h));

                if (resized.Channels() == 4)
                {
                    Mat[] channels = Cv2.Split(resized);
                    try
                    {
                        var alpha = channels[3];
                        if (frameRoi.Channels() == 4) resized.CopyTo(frameRoi, alpha);
                        else
                        {
                            using var stickerBgr = new Mat();
                            Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, stickerBgr);
                            stickerBgr.CopyTo(frameRoi, alpha);
                        }
                    }
                    finally { foreach (var c in channels) c?.Dispose(); }
                }
                else resized.CopyTo(frameRoi);
            }
            catch { }
        }
        
        private void LoadStickers()
        {
            string stickerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Stickers");
            if (!Directory.Exists(stickerPath)) return;
            var files = Directory.GetFiles(stickerPath, "*.png");
            foreach (var file in files)
            {
                using var sticker = Cv2.ImRead(file, ImreadModes.Unchanged);
                if (sticker.Empty()) continue;
                float ratio = (float)sticker.Width / sticker.Height;
                if (ratio > 1.2f) wideStickers.Add(sticker.Clone());
                else squareStickers.Add(sticker.Clone());
            }
        }

        private void SetupScreenshotFolder()
        {
            if (!Directory.Exists(SCREENSHOTS_FOLDER)) Directory.CreateDirectory(SCREENSHOTS_FOLDER);
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            capturer?.Dispose();
            processor?.Dispose();
            overlayManager?.Dispose();
            overlayTextManager?.Dispose();
            foreach (var s in squareStickers) s?.Dispose();
            foreach (var s in wideStickers) s?.Dispose();
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}