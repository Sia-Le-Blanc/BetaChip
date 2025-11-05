using System;
using System.Collections.Generic;
using System.IO;
using OpenCvSharp;

namespace MosaicCensorSystem.Overlay
{
    public class OverlayTextManager : IDisposable
    {
        private const double MIN_INTERVAL_SECONDS = 3.0;   // ★ 10초 → 3초로 단축
        private const double MAX_INTERVAL_SECONDS = 8.0;   // ★ 30초 → 8초로 단축
        private const float MAX_SCREEN_COVERAGE = 0.27f;   // ★ 0.4(40%) → 0.27(27%)로 감소 (10% 줄임: 40% * 0.9 = 36% → 더 작게 조정)

        private readonly Random random = new Random();
        private readonly List<Mat> overlayImages = new();
        private readonly Action<string> logCallback;

        private Mat? currentOverlay;
        private DateTime lastChangeTime = DateTime.MinValue;
        private double currentInterval = 0;
        private OpenCvSharp.Point currentPosition = new OpenCvSharp.Point(0, 0);
        private bool positionSet = false;
        private bool isActive = false;
        private bool disposed = false;

        public OverlayTextManager(Action<string> logger = null)
        {
            logCallback = logger;
            LoadOverlayImages();
        }

        private void LoadOverlayImages()
        {
            try
            {
                string overlayPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "OverlayText");
                
                logCallback?.Invoke($"📂 OverlayText 경로 확인 중: {overlayPath}");

                if (!Directory.Exists(overlayPath))
                {
                    logCallback?.Invoke($"⚠️ OverlayText 폴더 없음: {overlayPath}");
                    return;
                }

                var files = Directory.GetFiles(overlayPath, "*.png");
                
                if (files.Length == 0)
                {
                    logCallback?.Invoke($"⚠️ OverlayText 폴더에 PNG 파일이 없습니다: {overlayPath}");
                    return;
                }

                logCallback?.Invoke($"🔍 발견된 PNG 파일 수: {files.Length}");

                int loadedCount = 0;
                foreach (var file in files)
                {
                    using var img = Cv2.ImRead(file, ImreadModes.Unchanged);
                    if (!img.Empty())
                    {
                        overlayImages.Add(img.Clone());
                        loadedCount++;
                        logCallback?.Invoke($"✅ 이미지 로드 성공: {Path.GetFileName(file)} (원본: {img.Width}x{img.Height})");
                    }
                    else
                    {
                        logCallback?.Invoke($"⚠️ 이미지 로드 실패: {file}");
                    }
                }

                if (loadedCount > 0)
                {
                    logCallback?.Invoke($"✅ OverlayText 이미지 {loadedCount}개 로드 완료");
                }
                else
                {
                    logCallback?.Invoke($"❌ OverlayText 이미지 로드 실패 (0개)");
                }
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"❌ OverlayText 로드 중 오류: {ex.Message}");
            }
        }

        public void Update(bool detectionActive)
        {
            if (disposed) return;

            isActive = detectionActive;

            if (!isActive || overlayImages.Count == 0)
            {
                ClearCurrentOverlay();
                return;
            }

            if (currentOverlay == null || (DateTime.Now - lastChangeTime).TotalSeconds >= currentInterval)
            {
                ChangeOverlay();
            }
        }

        private void ClearCurrentOverlay()
        {
            if (currentOverlay != null)
            {
                if (!currentOverlay.IsDisposed)
                {
                    currentOverlay.Dispose();
                }
                currentOverlay = null;
            }
            positionSet = false;
        }

        private void ChangeOverlay()
        {
            ClearCurrentOverlay();

            int index = random.Next(overlayImages.Count);
            currentOverlay = overlayImages[index].Clone();

            currentInterval = MIN_INTERVAL_SECONDS + random.NextDouble() * (MAX_INTERVAL_SECONDS - MIN_INTERVAL_SECONDS);
            lastChangeTime = DateTime.Now;
            positionSet = false;  // ★ 새 오버레이마다 위치를 새로 선택하도록 리셋

            logCallback?.Invoke($"🎨 새 오버레이 선택됨 (다음 변경: {currentInterval:F1}초 후)");
        }

        public void DrawOverlayOnFrame(Mat frame)
        {
            if (disposed || !isActive || currentOverlay == null || currentOverlay.IsDisposed)
            {
                return;
            }

            if (frame == null || frame.Empty())
            {
                return;
            }

            Mat resizedOverlay = ResizeOverlayToFit(currentOverlay, frame.Width, frame.Height);
            
            if (resizedOverlay == null || resizedOverlay.Empty())
            {
                logCallback?.Invoke("⚠️ 오버레이 리사이징 실패");
                return;
            }

            int overlayWidth = resizedOverlay.Width;
            int overlayHeight = resizedOverlay.Height;

            // ★ positionSet이 false일 때마다 새 위치 계산 (더 자주 위치 변경)
            if (!positionSet)
            {
                int maxX = Math.Max(0, frame.Width - overlayWidth);
                int maxY = Math.Max(0, frame.Height - overlayHeight);
                
                // ★ 화면 전체에서 랜덤하게 위치 선택
                int x = maxX == 0 ? 0 : random.Next(0, maxX + 1);
                int y = maxY == 0 ? 0 : random.Next(0, maxY + 1);
                
                currentPosition = new OpenCvSharp.Point(x, y);
                positionSet = true;
                logCallback?.Invoke($"📍 오버레이 표시: 크기({overlayWidth}x{overlayHeight}), 위치({x}, {y})");
            }

            BlendMatOnFrame(frame, resizedOverlay, currentPosition.X, currentPosition.Y);
            
            resizedOverlay.Dispose();
        }

        /// <summary>
        /// 오버레이 이미지를 화면 크기에 맞게 리사이징합니다.
        /// 화면의 일정 비율(MAX_SCREEN_COVERAGE)을 넘지 않도록 조정합니다.
        /// </summary>
        private Mat ResizeOverlayToFit(Mat original, int frameWidth, int frameHeight)
        {
            if (original == null || original.Empty()) return null;

            int origWidth = original.Width;
            int origHeight = original.Height;

            // 오버레이가 화면보다 작으면 그대로 사용
            if (origWidth <= frameWidth * MAX_SCREEN_COVERAGE && 
                origHeight <= frameHeight * MAX_SCREEN_COVERAGE)
            {
                return original.Clone();
            }

            // 화면의 27%를 최대 크기로 설정
            int maxWidth = (int)(frameWidth * MAX_SCREEN_COVERAGE);
            int maxHeight = (int)(frameHeight * MAX_SCREEN_COVERAGE);

            // 비율을 유지하면서 크기 조정
            float scaleWidth = (float)maxWidth / origWidth;
            float scaleHeight = (float)maxHeight / origHeight;
            float scale = Math.Min(scaleWidth, scaleHeight);

            int newWidth = (int)(origWidth * scale);
            int newHeight = (int)(origHeight * scale);

            // 최소 크기 보장 (너무 작아지지 않도록)
            newWidth = Math.Max(150, newWidth);
            newHeight = Math.Max(80, newHeight);

            // 리사이징
            Mat resized = new Mat();
            Cv2.Resize(original, resized, new OpenCvSharp.Size(newWidth, newHeight), interpolation: InterpolationFlags.Area);

            logCallback?.Invoke($"🔧 오버레이 리사이징: {origWidth}x{origHeight} → {newWidth}x{newHeight}");

            return resized;
        }

        private void BlendMatOnFrame(Mat frame, Mat overlay, int x, int y)
        {
            if (disposed || overlay == null || overlay.IsDisposed || frame.IsDisposed) return;

            int w = overlay.Width;
            int h = overlay.Height;

            if (w <= 0 || h <= 0) return;
            if (x < 0 || y < 0 || x + w > frame.Width || y + h > frame.Height) return;

            using var frameRoi = new Mat(frame, new Rect(x, y, w, h));

            if (overlay.Channels() == 4)
            {
                Mat[] channels = Cv2.Split(overlay);
                try
                {
                    var alpha = channels[3];
                    if (frameRoi.Channels() == 4)
                    {
                        overlay.CopyTo(frameRoi, alpha);
                    }
                    else
                    {
                        using var overlayBgr = new Mat();
                        Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, overlayBgr);
                        overlayBgr.CopyTo(frameRoi, alpha);
                    }
                }
                finally
                {
                    foreach (var c in channels)
                    {
                        c?.Dispose();
                    }
                }
            }
            else if (frameRoi.Channels() == 4)
            {
                using var overlayBgra = new Mat();
                Cv2.CvtColor(overlay, overlayBgra, ColorConversionCodes.BGR2BGRA);
                overlayBgra.CopyTo(frameRoi);
            }
            else
            {
                overlay.CopyTo(frameRoi);
            }
        }

        public void Dispose()
        {
            if (disposed) return;

            ClearCurrentOverlay();

            foreach (var img in overlayImages)
            {
                if (img != null && !img.IsDisposed)
                {
                    img.Dispose();
                }
            }
            overlayImages.Clear();

            disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}