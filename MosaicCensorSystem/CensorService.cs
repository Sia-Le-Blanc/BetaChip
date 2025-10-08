#nullable disable
using MosaicCensorSystem.Capture;
using MosaicCensorSystem.Detection;
using MosaicCensorSystem.Management;
using MosaicCensorSystem.UI;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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

        // ★★★ [수정] MosaicApp에서 processor에 접근할 수 있도록 public getter 추가 ★★★
        public MosaicProcessor Processor => processor;
        
        private CensorSettings currentSettings = new(true, true, false, 15);

        private readonly List<Mat> squareStickers = new();
        private readonly List<Mat> wideStickers = new();
        private readonly Dictionary<int, StickerInfo> trackedStickers = new();

        private static readonly string SCREENSHOTS_FOLDER = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BetaChip Screenshots");
        private static readonly string DESKTOP_SHORTCUT = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "BetaChip 스크린샷.lnk");

        public CensorService(GuiController uiController)
        {
            ui = uiController;
            capturer = new ScreenCapture();
            processor = new MosaicProcessor(Program.ONNX_MODEL_PATH);

#if PATREON_VERSION
            overlayManager = new MultiMonitorManager(capturer);
            ui.LogMessage("🖥️ 후원자 버전: 멀티 모니터 관리자 활성화!");
#else
            overlayManager = new SingleMonitorManager(capturer);
            ui.LogMessage("🖥️ 무료 버전: 단일 모니터 관리자 활성화");
#endif
            overlayManager.Initialize(ui);

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
            if (rawFrame == null || rawFrame.Empty()) return null;

            Mat processedFrame = rawFrame.Clone();
            if (!currentSettings.EnableDetection) return processedFrame;

            List<Detection.Detection> detections = processor.DetectObjects(rawFrame);
            foreach (var detection in detections)
            {
                if (currentSettings.EnableCensoring)
                {
                    processor.ApplySingleCensorOptimized(processedFrame, detection);
                }

                if (currentSettings.EnableStickers && (squareStickers.Count > 0 || wideStickers.Count > 0))
                {
                    if (!trackedStickers.TryGetValue(detection.TrackId, out var stickerInfo) || (DateTime.Now - stickerInfo.AssignedTime).TotalSeconds > 30)
                    {
                        var stickerList = (float)detection.Width / detection.Height > 1.2f ? wideStickers : squareStickers;
                        if (stickerList.Count > 0)
                        {
                            stickerInfo = new StickerInfo { Sticker = stickerList[random.Next(stickerList.Count)], AssignedTime = DateTime.Now };
                            trackedStickers[detection.TrackId] = stickerInfo;
                        }
                    }
                    
                    if (stickerInfo?.Sticker != null && !stickerInfo.Sticker.IsDisposed)
                    {
                        BlendStickerOnMosaic(processedFrame, detection, stickerInfo.Sticker);
                    }
                }
            }
            return processedFrame;
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
                MessageBox.Show($"캡처 저장 중 오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        public void UpdateSetting(string key, object value)
        {
            bool settingsChanged = false;
            switch (key)
            {
                case nameof(CensorSettings.TargetFPS): currentSettings = currentSettings with { TargetFPS = (int)value }; settingsChanged = true; break;
                case nameof(CensorSettings.EnableDetection): currentSettings = currentSettings with { EnableDetection = (bool)value }; settingsChanged = true; break;
                case nameof(CensorSettings.EnableCensoring): currentSettings = currentSettings with { EnableCensoring = (bool)value }; settingsChanged = true; break;
                case nameof(CensorSettings.EnableStickers):
                    currentSettings = currentSettings with { EnableStickers = (bool)value };
                    settingsChanged = true;
                    ui.LogMessage($"🎯 스티커 기능 {(currentSettings.EnableStickers ? "활성화" : "비활성화")}");
                    break;
                case "CensorType": processor.SetCensorType((CensorType)value); break;
                case "Strength": processor.SetStrength((int)value); break;
                case "Confidence": processor.ConfThreshold = (float)value; break;
                case "Targets": processor.SetTargets((List<string>)value); break;
            }

            if (settingsChanged)
            {
                overlayManager.UpdateSettings(currentSettings);
            }
        }
        
        #region Helper Methods 
        
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
                    using var stickerBgr = new Mat();
                    Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, stickerBgr);
                    using var alpha = channels[3];
                    stickerBgr.CopyTo(frameRoi, alpha);
                    foreach (var c in channels) c.Dispose();
                }
                else
                {
                    resized.CopyTo(frameRoi);
                }
            }
            catch (Exception ex)
            {
                ui.LogMessage($"🚨 스티커 블렌딩 오류: {ex.Message}");
            }
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
                
                float ratio = (float)sticker.Width / sticker.Height;
                if (ratio > 1.2f) wideStickers.Add(sticker.Clone());
                else squareStickers.Add(sticker.Clone());
            }
            ui.LogMessage($"✅ 스티커 로드: Square({squareStickers.Count}), Wide({wideStickers.Count})");
        }

        private void SetupScreenshotFolder()
        {
            try
            {
                if (!Directory.Exists(SCREENSHOTS_FOLDER)) Directory.CreateDirectory(SCREENSHOTS_FOLDER);
                if (!File.Exists(DESKTOP_SHORTCUT)) TryCreateWindowsShortcut();
            }
            catch (Exception ex) { ui.LogMessage($"⚠️ 스크린샷 폴더 설정 실패: {ex.Message}"); }
        }

        private void TryCreateWindowsShortcut()
        {
             try
            {
                string psScript = $@"
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut('{DESKTOP_SHORTCUT}')
$Shortcut.TargetPath = '{SCREENSHOTS_FOLDER}'
$Shortcut.Description = 'BetaChip 검열된 스크린샷 모음'
$Shortcut.Save()";
                var psi = new ProcessStartInfo { FileName = "powershell.exe", Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript}\"", UseShellExecute = false, CreateNoWindow = true };
                Process.Start(psi)?.WaitForExit(5000);
            }
            catch (Exception ex) { ui.LogMessage($"⚠️ PowerShell 바로가기 생성 실패: {ex.Message}"); }
        }

        #endregion

        public void Dispose()
        {
            Stop();
            capturer?.Dispose();
            processor?.Dispose();
            overlayManager?.Dispose();
            foreach (var s in squareStickers) s.Dispose();
            foreach (var s in wideStickers) s.Dispose();
        }
    }
}