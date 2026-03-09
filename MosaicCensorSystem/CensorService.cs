#nullable disable
using MosaicCensorSystem.Capture;
using MosaicCensorSystem.Detection;
using MosaicCensorSystem.Management;
using MosaicCensorSystem.Overlay;
using MosaicCensorSystem.UI;
using MosaicCensorSystem.Models;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
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

        // 스레드별 독립 추론 컨텍스트 풀
        // InferenceThread(단일/다중 모니터)와 CaptureAndSave(UI 스레드) 각각에게
        // 전용 버퍼를 할당하여 inputBuffer·Mat 충돌을 완전히 제거합니다.
        private readonly ThreadLocal<InferenceContext> _perThreadCtx;

        private CensorSettings currentSettings = new(true, true, false, true);

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
            _subInfo = subInfo;
            capturer = new ScreenCapture();
            processor = new MosaicProcessor(Program.STANDARD_MODEL_PATH);
            processor.LogCallback = ui.LogMessage;

            // 스레드별 컨텍스트 풀: 각 InferenceThread와 UI 스레드가 호출하는 첫 순간에 생성됨
            // trackAllValues: true → Dispose() 시 모든 컨텍스트를 순회하여 해제 가능
            _perThreadCtx = new ThreadLocal<InferenceContext>(
                () => processor.CreateContext(), trackAllValues: true);

            // 등급에 따라 매니저 결정 (Patreon 이상이면 멀티모니터)
            if (_subInfo.Tier == "plus" || _subInfo.Tier == "patreon")
            {
                // GPU 여부를 전달하여 병렬/직렬 추론 모드를 결정
                bool isGpuActive = !processor.CurrentExecutionProvider.Contains("CPU", StringComparison.OrdinalIgnoreCase);
                overlayManager = new MultiMonitorManager(isGpuActive);
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

                // 호출 스레드(InferenceThread 또는 UI 스레드)별 전용 컨텍스트를 사용하여
                // inputBuffer·Mat 충돌 없이 InferenceSession.Run()을 병렬 호출합니다.
                List<Detection.Detection> detections = processor.DetectObjects(rawFrame, _perThreadCtx.Value);
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
                // TargetFPS 케이스 제거: FPS는 하드웨어가 자동으로 결정
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
            // OBB 모드이고 유효한 각도가 있으면 회전 렌더링 경로로 분기
            if (processor.isObbMode && detection.ObbWidth > 0 && detection.ObbHeight > 0)
            {
                BlendRotatedStickerObb(frame, detection, sticker);
                return;
            }

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
        
        // OBB 전용: 스티커를 detection 각도(Degree)로 회전하여 OBB 중심에 합성
        private void BlendRotatedStickerObb(Mat frame, Detection.Detection detection, Mat sticker)
        {
            try
            {
                int ow = Math.Max(1, (int)detection.ObbWidth);
                int oh = Math.Max(1, (int)detection.ObbHeight);

                // 스티커를 OBB 고유 크기로 리사이즈
                using var resized = new Mat();
                Cv2.Resize(sticker, resized, new OpenCvSharp.Size(ow, oh), interpolation: InterpolationFlags.Area);

                // 스티커 중심 기준 회전 행렬 (Detection.Angle은 이미 Degree 단위)
                var stickerCenter = new Point2f(ow / 2f, oh / 2f);
                using var rotMat = Cv2.GetRotationMatrix2D(stickerCenter, detection.Angle, 1.0);

                // 회전 후 바운딩 박스 크기 계산
                var rotRect = new RotatedRect(stickerCenter, new Size2f(ow, oh), (float)detection.Angle);
                var bb = rotRect.BoundingRect();
                int dstW = Math.Max(1, bb.Width);
                int dstH = Math.Max(1, bb.Height);

                // 회전 후 이미지 중심이 (dstW/2, dstH/2)에 오도록 평행이동 보정
                rotMat.At<double>(0, 2) += (dstW - ow) / 2.0;
                rotMat.At<double>(1, 2) += (dstH - oh) / 2.0;

                using var rotated = new Mat();
                Cv2.WarpAffine(resized, rotated, rotMat, new OpenCvSharp.Size(dstW, dstH),
                    flags: InterpolationFlags.Linear, borderMode: BorderTypes.Constant, borderValue: new Scalar(0));

                // OBB 중심 좌표를 기준으로 프레임 상의 붙여넣기 위치 계산
                int pasteX = (int)(detection.CenterX - dstW / 2f);
                int pasteY = (int)(detection.CenterY - dstH / 2f);

                // 프레임 경계를 벗어나는 영역 클리핑
                int srcX = Math.Max(0, -pasteX);
                int srcY = Math.Max(0, -pasteY);
                int dstX = Math.Max(0, pasteX);
                int dstY = Math.Max(0, pasteY);
                int copyW = Math.Min(dstW - srcX, frame.Width - dstX);
                int copyH = Math.Min(dstH - srcY, frame.Height - dstY);
                if (copyW <= 0 || copyH <= 0) return;

                using var srcRoi = new Mat(rotated, new Rect(srcX, srcY, copyW, copyH));
                using var dstRoi = new Mat(frame, new Rect(dstX, dstY, copyW, copyH));

                if (srcRoi.Channels() == 4)
                {
                    Mat[] ch = Cv2.Split(srcRoi);
                    try
                    {
                        using var bgr = new Mat();
                        Cv2.Merge(new[] { ch[0], ch[1], ch[2] }, bgr);
                        bgr.CopyTo(dstRoi, ch[3]); // alpha 채널을 마스크로 사용
                    }
                    finally { foreach (var c in ch) c?.Dispose(); }
                }
                else
                {
                    srcRoi.CopyTo(dstRoi);
                }
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

            // 모든 스레드의 InferenceContext 해제 (trackAllValues: true로 생성했으므로 순회 가능)
            if (_perThreadCtx != null)
            {
                foreach (var ctx in _perThreadCtx.Values)
                    try { ctx?.Dispose(); } catch { }
                _perThreadCtx.Dispose();
            }

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