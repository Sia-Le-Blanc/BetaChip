#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MosaicCensorSystem.Capture;
using MosaicCensorSystem.Detection;
using MosaicCensorSystem.UI;
using OpenCvSharp;

// ★★★ 후원자 전용: 멀티 모니터 기능 ★★★
#if PATREON_VERSION
using MosaicCensorSystem.Monitor;
#else
using MosaicCensorSystem.Overlay;
#endif

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
        private readonly Random random = new Random();

        // ★★★ 조건부 컴파일: 후원자는 멀티모니터, 무료는 단일 오버레이 ★★★
#if PATREON_VERSION
        private readonly MultiMonitorManager multiMonitorManager;
#else
        private readonly FullscreenOverlay singleOverlay;
#endif

        private Thread processThread;
        private volatile bool isRunning = false;
        private int targetFPS = 15;
        private bool enableDetection = true;
        private bool enableCensoring = true;
        private bool enableStickers = false;

        private readonly List<Mat> squareStickers = new();
        private readonly List<Mat> wideStickers = new();
        private readonly Dictionary<int, StickerInfo> trackedStickers = new();

        // ★★★ 스크린샷 저장 관련 ★★★
        private static readonly string SCREENSHOTS_FOLDER = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
            "BetaChip Screenshots");
        
        private static readonly string DESKTOP_SHORTCUT = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), 
            "BetaChip 스크린샷.lnk");

        public CensorService(GuiController uiController)
        {
            ui = uiController;
            capturer = new ScreenCapture();
            processor = new MosaicProcessor(Program.ONNX_MODEL_PATH);
            
            // ★★★ 조건부 초기화 ★★★
#if PATREON_VERSION
            multiMonitorManager = new MultiMonitorManager();
            ui.LogMessage($"🖥️ 후원자 기능: 멀티 모니터 지원 활성화!");
            ui.LogMessage($"🖥️ 감지된 모니터 수: {multiMonitorManager.Monitors.Count}");
            for (int i = 0; i < multiMonitorManager.Monitors.Count; i++)
            {
                var monitor = multiMonitorManager.Monitors[i];
                ui.LogMessage($"   모니터 {i + 1}: {monitor.Bounds.Width}x{monitor.Bounds.Height}");
            }
#else
            singleOverlay = new FullscreenOverlay();
            ui.LogMessage($"🖥️ 무료 버전: 메인 모니터만 지원");
#endif

            // ★★★ 스크린샷 폴더 및 바로가기 설정 ★★★
            SetupScreenshotFolder();

            LoadStickers();

#if PATREON_VERSION
            // ★★★ 멀티모니터 매니저에 프로세서와 스티커 전달 ★★★
            multiMonitorManager.Initialize(processor, squareStickers, wideStickers);
#endif

            // 모델 워밍업
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

        // ★★★ 스크린샷 폴더 및 바탕화면 바로가기 설정 ★★★
        private void SetupScreenshotFolder()
        {
            try
            {
                // 스크린샷 폴더 생성
                if (!Directory.Exists(SCREENSHOTS_FOLDER))
                {
                    Directory.CreateDirectory(SCREENSHOTS_FOLDER);
                    ui.LogMessage($"📁 스크린샷 폴더 생성: {SCREENSHOTS_FOLDER}");
                }

                // 바탕화면 바로가기 생성 (없을 때만)
                if (!File.Exists(DESKTOP_SHORTCUT))
                {
                    CreateDesktopShortcut();
                }
            }
            catch (Exception ex)
            {
                ui.LogMessage($"⚠️ 스크린샷 폴더 설정 실패: {ex.Message}");
            }
        }

        // ★★★ 바탕화면 바로가기 생성 ★★★
        private void CreateDesktopShortcut()
        {
            try
            {
                // 방법 1: 간단한 텍스트 파일 바로가기 (항상 작동)
                string simpleShortcut = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "BetaChip 스크린샷 폴더.txt");

                if (!File.Exists(simpleShortcut))
                {
                    File.WriteAllText(simpleShortcut, 
                        $"BetaChip 스크린샷 저장 폴더:\n{SCREENSHOTS_FOLDER}\n\n" +
                        "위 경로를 복사해서 탐색기 주소창에 붙여넣으세요.\n\n" +
                        "또는 이 파일과 같은 폴더에 있는 'BetaChip 스크린샷.lnk' 파일을 더블클릭하세요.");
                    ui.LogMessage($"📝 바탕화면에 폴더 경로 파일 생성: {simpleShortcut}");
                }

                // 방법 2: PowerShell을 사용한 바로가기 생성
                TryCreateWindowsShortcut();
            }
            catch (Exception ex)
            {
                ui.LogMessage($"⚠️ 바탕화면 바로가기 생성 실패: {ex.Message}");
            }
        }

        private void TryCreateWindowsShortcut()
        {
            try
            {
                // PowerShell을 사용한 바로가기 생성 (Windows 10/11에서 안정적)
                string psScript = $@"
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut('{DESKTOP_SHORTCUT}')
$Shortcut.TargetPath = '{SCREENSHOTS_FOLDER}'
$Shortcut.Description = 'BetaChip 검열된 스크린샷 모음'
$Shortcut.Save()
";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit(5000); // 5초 타임아웃

                    if (process.ExitCode == 0 && File.Exists(DESKTOP_SHORTCUT))
                    {
                        ui.LogMessage($"🔗 Windows 바로가기 생성 완료: {DESKTOP_SHORTCUT}");
                    }
                    else
                    {
                        ui.LogMessage("⚠️ Windows 바로가기 생성 실패, 텍스트 파일로 대체됨");
                    }
                }
            }
            catch (Exception ex)
            {
                ui.LogMessage($"⚠️ PowerShell 바로가기 생성 실패: {ex.Message}");
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
            
            // ★★★ 조건부 오버레이 표시 ★★★
#if PATREON_VERSION
            // ★★★ 후원자 버전: 각 모니터별 개별 처리 시작 ★★★
            multiMonitorManager.UpdateSettings(enableDetection, enableCensoring, enableStickers, targetFPS);
            multiMonitorManager.ShowOverlays();
            ui.LogMessage("🖥️ 멀티모니터 개별 처리 시작됨");
#else
            // ★★★ 무료 버전: 기존 방식 유지 ★★★
            singleOverlay.Show();
            processThread = new Thread(ProcessingLoop) { IsBackground = true, Name = "CensorProcessingThread" };
            processThread.Start();
#endif
        }

        public void Stop()
        {
            if (!isRunning) return;
            isRunning = false;
            
            // ★★★ 조건부 오버레이 숨김 ★★★
#if PATREON_VERSION
            multiMonitorManager.HideOverlays();
#else
            processThread?.Join(1000);
            singleOverlay.Hide();
#endif

            ui.SetRunningState(false);
            ui.UpdateStatus("⭕ 시스템 대기 중", Color.Red);
        }

#if !PATREON_VERSION
        // ★★★ 무료 버전용 기존 처리 루프 (단일 모니터) ★★★
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

                        // 2단계: 스티커를 모자이크 위에 블렌딩 (무료버전에서는 스티커 없음)
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

                singleOverlay.UpdateFrame(displayFrame);
                
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
                    using var frameRoiBgr = new Mat();
                    Cv2.CvtColor(frameRoi, frameRoiBgr, ColorConversionCodes.BGRA2BGR);
                    Mat[] channels = null;
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
                            foreach (var c in channels) c?.Dispose();
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
                ui.LogMessage($"🚨 스티커 블렌딩 오류: {ex.Message}");
            }
        }
#endif

        // ★★★ 캡처 저장 기능 (기존 TestCapture 대체) ★★★
        public void CaptureAndSave()
        {
            if (!isRunning)
            {
                ui.LogMessage("❌ 시스템이 실행 중이 아닙니다. 먼저 시작 버튼을 눌러주세요.");
                MessageBox.Show("검열 시스템이 실행 중일 때만 캡처 저장이 가능합니다.", 
                              "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                ui.LogMessage("📸 검열된 화면 캡처 시작...");
                
                // 현재 화면 캡처
                using Mat rawFrame = capturer.GetFrame();
                if (rawFrame == null || rawFrame.Empty())
                {
                    ui.LogMessage("❌ 화면 캡처 실패: 빈 프레임이 반환되었습니다.");
                    return;
                }

                // 검열 처리 적용
                using Mat processedFrame = rawFrame.Clone();
                
                if (enableDetection)
                {
                    List<Detection.Detection> detections = processor.DetectObjects(rawFrame);
                    foreach (var detection in detections)
                    {
                        // 검열 효과 적용
                        if (enableCensoring) 
                        {
                            processor.ApplySingleCensorOptimized(processedFrame, detection);
                        }

                        // 스티커 적용 (후원자 기능)
                        if (enableStickers && (squareStickers.Count > 0 || wideStickers.Count > 0))
                        {
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

#if !PATREON_VERSION
                            if (trackedStickers.TryGetValue(detection.TrackId, out stickerInfo) && 
                                stickerInfo.Sticker != null && !stickerInfo.Sticker.IsDisposed)
                            {
                                BlendStickerOnMosaic(processedFrame, detection, stickerInfo.Sticker);
                            }
#endif
                        }
                    }
                }

                // 파일명 생성 (타임스탬프 포함)
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"BetaChip_{timestamp}.jpg";
                string filePath = Path.Combine(SCREENSHOTS_FOLDER, fileName);

                // 이미지 저장
                processedFrame.SaveImage(filePath);
                
                // 성공 메시지
                ui.LogMessage($"✅ 캡처 저장 완료! 파일: {fileName}");
                ui.LogMessage($"📁 저장 위치: {SCREENSHOTS_FOLDER}");
                
                MessageBox.Show(
                    $"검열된 스크린샷이 저장되었습니다!\n\n" +
                    $"파일명: {fileName}\n" +
                    $"크기: {processedFrame.Width}x{processedFrame.Height}\n\n" +
                    $"바탕화면의 'BetaChip 스크린샷' 바로가기로 폴더에 접근할 수 있습니다.", 
                    "캡처 저장 완료", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ui.LogMessage($"❌ 캡처 저장 중 오류: {ex.Message}");
                MessageBox.Show($"캡처 저장 중 오류가 발생했습니다:\n{ex.Message}", 
                              "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void UpdateSetting(string key, object value)
        {
            switch (key)
            {
                case "TargetFPS": 
                    targetFPS = (int)value;
#if PATREON_VERSION
                    multiMonitorManager?.UpdateSettings(enableDetection, enableCensoring, enableStickers, targetFPS);
#endif
                    break;
                case "EnableDetection": 
                    enableDetection = (bool)value;
#if PATREON_VERSION
                    multiMonitorManager?.UpdateSettings(enableDetection, enableCensoring, enableStickers, targetFPS);
#endif
                    break;
                case "EnableCensoring": 
                    enableCensoring = (bool)value;
#if PATREON_VERSION
                    multiMonitorManager?.UpdateSettings(enableDetection, enableCensoring, enableStickers, targetFPS);
#endif
                    break;
                case "EnableStickers": 
                    enableStickers = (bool)value;
                    ui.LogMessage($"🎯 스티커 기능 {(enableStickers ? "활성화" : "비활성화")}");
#if PATREON_VERSION
                    multiMonitorManager?.UpdateSettings(enableDetection, enableCensoring, enableStickers, targetFPS);
#endif
                    break;
                case "CensorType": processor.SetCensorType((CensorType)value); break;
                case "Strength": processor.SetStrength((int)value); break;
                case "Confidence": processor.ConfThreshold = (float)value; break;
                case "Targets": processor.SetTargets((List<string>)value); break;
            }
        }

        // ★★★ 후원자 전용: 모니터 설정 ★★★
#if PATREON_VERSION
        public void SetMonitorEnabled(int index, bool enabled)
        {
            multiMonitorManager.SetMonitorEnabled(index, enabled);
            ui.LogMessage($"🖥️ 모니터 {index + 1} {(enabled ? "활성화" : "비활성화")}");
        }
#endif

        public void Dispose()
        {
            Stop();
            capturer?.Dispose();
            processor?.Dispose();
            
            // ★★★ 조건부 리소스 해제 ★★★
#if PATREON_VERSION
            multiMonitorManager?.Dispose();
#else
            singleOverlay?.Dispose();
#endif

            foreach (var s in squareStickers) s.Dispose();
            foreach (var s in wideStickers) s.Dispose();
        }
    }
}