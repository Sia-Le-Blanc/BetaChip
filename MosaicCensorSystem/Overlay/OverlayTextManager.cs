using System;
using System.Collections.Generic;
using System.IO;
using OpenCvSharp;

namespace MosaicCensorSystem.Overlay
{
    public class OverlayTextManager : IDisposable
    {
        private const double MIN_INTERVAL_SECONDS = 3.0;
        private const double MAX_INTERVAL_SECONDS = 8.0;
        private const float MAX_SCREEN_COVERAGE = 0.27f;

        private readonly Random random = new Random();
        private readonly List<Mat> overlayImages = new();
        private readonly Action<string>? logCallback;

        private Mat? currentOverlay;
        private DateTime lastChangeTime = DateTime.MinValue;
        private double currentInterval = 0;
        private OpenCvSharp.Point currentPosition = new OpenCvSharp.Point(0, 0);
        private bool positionSet = false;
        private bool isActive = false;
        private bool disposed = false;

        public OverlayTextManager(Action<string>? logger = null)
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
                    try
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
                    catch (Exception ex)
                    {
                        logCallback?.Invoke($"⚠️ 이미지 파일 처리 중 오류 ({Path.GetFileName(file)}): {ex.Message}");
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
            if (currentOverlay != null && !currentOverlay.IsDisposed)
            {
                try
                {
                    currentOverlay.Dispose();
                }
                catch (Exception ex)
                {
                    logCallback?.Invoke($"⚠️ 오버레이 Dispose 중 오류: {ex.Message}");
                }
            }
            currentOverlay = null;
            positionSet = false;
        }

        private void ChangeOverlay()
        {
            ClearCurrentOverlay();

            if (overlayImages.Count == 0) return;

            try
            {
                int index = random.Next(overlayImages.Count);
                Mat sourceOverlay = overlayImages[index];
                
                if (sourceOverlay != null && !sourceOverlay.IsDisposed && !sourceOverlay.Empty())
                {
                    currentOverlay = sourceOverlay.Clone();
                    currentInterval = MIN_INTERVAL_SECONDS + random.NextDouble() * (MAX_INTERVAL_SECONDS - MIN_INTERVAL_SECONDS);
                    lastChangeTime = DateTime.Now;
                    positionSet = false;

                    logCallback?.Invoke($"🎨 새 오버레이 선택됨 (다음 변경: {currentInterval:F1}초 후)");
                }
                else
                {
                    logCallback?.Invoke($"⚠️ 선택된 오버레이가 유효하지 않음");
                }
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"❌ 오버레이 변경 중 오류: {ex.Message}");
            }
        }

        public void DrawOverlayOnFrame(Mat frame)
        {
            if (disposed || !isActive)
            {
                return;
            }

            if (frame == null || frame.IsDisposed || frame.Empty())
            {
                return;
            }

            if (currentOverlay == null || currentOverlay.IsDisposed || currentOverlay.Empty())
            {
                return;
            }

            Mat? resizedOverlay = null;
            try
            {
                resizedOverlay = ResizeOverlayToFit(currentOverlay, frame.Width, frame.Height);
                
                if (resizedOverlay == null || resizedOverlay.IsDisposed || resizedOverlay.Empty())
                {
                    return;
                }

                int overlayWidth = resizedOverlay.Width;
                int overlayHeight = resizedOverlay.Height;

                if (!positionSet)
                {
                    int maxX = Math.Max(0, frame.Width - overlayWidth);
                    int maxY = Math.Max(0, frame.Height - overlayHeight);
                    
                    int x = maxX == 0 ? 0 : random.Next(0, maxX + 1);
                    int y = maxY == 0 ? 0 : random.Next(0, maxY + 1);
                    
                    currentPosition = new OpenCvSharp.Point(x, y);
                    positionSet = true;
                    logCallback?.Invoke($"📍 오버레이 표시: 크기({overlayWidth}x{overlayHeight}), 위치({x}, {y})");
                }

                if (!frame.IsDisposed && !frame.Empty())
                {
                    BlendMatOnFrame(frame, resizedOverlay, currentPosition.X, currentPosition.Y);
                }
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"❌ DrawOverlayOnFrame 오류: {ex.Message}");
            }
            finally
            {
                if (resizedOverlay != null && !resizedOverlay.IsDisposed)
                {
                    try
                    {
                        resizedOverlay.Dispose();
                    }
                    catch { }
                }
            }
        }

        private Mat? ResizeOverlayToFit(Mat original, int frameWidth, int frameHeight)
        {
            if (original == null || original.IsDisposed || original.Empty()) 
                return null;

            if (frameWidth <= 0 || frameHeight <= 0)
                return null;

            try
            {
                int origWidth = original.Width;
                int origHeight = original.Height;

                if (origWidth <= 0 || origHeight <= 0)
                    return null;

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

                int newWidth = Math.Max(150, (int)(origWidth * scale));
                int newHeight = Math.Max(80, (int)(origHeight * scale));

                // 리사이징
                Mat resized = new Mat();
                Cv2.Resize(original, resized, new OpenCvSharp.Size(newWidth, newHeight), 
                          interpolation: InterpolationFlags.Area);

                // 리사이징 실패 시 명시적으로 null 반환
                if (resized == null || resized.IsDisposed || resized.Empty())
                {
                    try
                    {
                        resized?.Dispose();
                    }
                    catch { }
                    
                    logCallback?.Invoke("❌ 리사이징 결과가 비어있음");
                    return null;
                }

                logCallback?.Invoke($"🔧 오버레이 리사이징: {origWidth}x{origHeight} → {newWidth}x{newHeight}");
                return resized;
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"❌ ResizeOverlayToFit 오류: {ex.Message}");
                return null;
            }
        }

        private void BlendMatOnFrame(Mat frame, Mat overlay, int x, int y)
        {
            if (frame == null || frame.IsDisposed || frame.Empty())
                return;
                
            if (overlay == null || overlay.IsDisposed || overlay.Empty())
                return;

            try
            {
                int w = overlay.Width;
                int h = overlay.Height;

                if (w <= 0 || h <= 0) return;
                if (x < 0 || y < 0 || x + w > frame.Width || y + h > frame.Height) return;

                using var frameRoi = new Mat(frame, new Rect(x, y, w, h));
                
                if (frameRoi == null || frameRoi.IsDisposed || frameRoi.Empty())
                    return;

                if (overlay.Channels() == 4)
                {
                    Mat[]? channels = null;
                    try
                    {
                        channels = Cv2.Split(overlay);
                        var alpha = channels[3];
                        
                        if (alpha == null || alpha.IsDisposed) return;

                        if (frameRoi.Channels() == 4)
                        {
                            overlay.CopyTo(frameRoi, alpha);
                        }
                        else
                        {
                            using var overlayBgr = new Mat();
                            Cv2.Merge(new[] { channels[0], channels[1], channels[2] }, overlayBgr);
                            if (!overlayBgr.Empty())
                            {
                                overlayBgr.CopyTo(frameRoi, alpha);
                            }
                        }
                    }
                    finally
                    {
                        if (channels != null)
                        {
                            foreach (var c in channels)
                            {
                                if (c != null && !c.IsDisposed)
                                {
                                    try
                                    {
                                        c.Dispose();
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }
                else if (frameRoi.Channels() == 4)
                {
                    using var overlayBgra = new Mat();
                    Cv2.CvtColor(overlay, overlayBgra, ColorConversionCodes.BGR2BGRA);
                    if (!overlayBgra.Empty())
                    {
                        overlayBgra.CopyTo(frameRoi);
                    }
                }
                else
                {
                    overlay.CopyTo(frameRoi);
                }
            }
            catch (Exception ex)
            {
                logCallback?.Invoke($"❌ BlendMatOnFrame 오류: {ex.Message}");
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
                    try
                    {
                        img.Dispose();
                    }
                    catch { }
                }
            }
            overlayImages.Clear();

            disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}