using System;
using System.Collections.Generic;
using System.IO;
using OpenCvSharp;

namespace MosaicCensorSystem.Overlay
{
    /// <summary>
    /// 화면에 텍스트 이미지(오버레이 텍스트)를 일정 시간 간격으로 랜덤한 위치에 표시하는 관리자입니다.
    /// 객체가 감지되었을 때만 동작하도록 설계되었습니다.
    /// </summary>
    public class OverlayTextManager : IDisposable
    {
        private readonly Random random = new Random();
        private readonly List<Mat> overlayImages = new();

        private Mat? currentOverlay;
        private DateTime lastChangeTime = DateTime.MinValue;
        private double currentInterval = 0;
        private OpenCvSharp.Point currentPosition = new OpenCvSharp.Point(0, 0);
        private bool positionSet = false;
        private bool isActive = false;

        /// <summary>
        /// 생성자. 오버레이 텍스트 이미지를 Resources/OverlayText 폴더에서 로드합니다.
        /// </summary>
        public OverlayTextManager()
        {
            LoadOverlayImages();
        }

        /// <summary>
        /// 지정된 폴더에서 PNG 이미지를 로드합니다.
        /// 폴더가 없거나 파일이 없으면 아무것도 로드하지 않습니다.
        /// </summary>
        private void LoadOverlayImages()
        {
            try
            {
                // OverlayText 폴더 경로를 지정합니다. 프로젝트 빌드 시 Resources/OverlayText 폴더가 복사되어야 합니다.
                string overlayPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "OverlayText");
                if (!Directory.Exists(overlayPath))
                {
                    return;
                }

                var files = Directory.GetFiles(overlayPath, "*.png");
                foreach (var file in files)
                {
                    using var img = Cv2.ImRead(file, ImreadModes.Unchanged);
                    if (!img.Empty())
                    {
                        overlayImages.Add(img.Clone());
                    }
                }
            }
            catch (Exception)
            {
                // 폴더 접근 시 예외가 발생해도 무시합니다. UI 로그는 상위에서 처리할 수 있습니다.
            }
        }

        /// <summary>
        /// 현재 탐지 상태를 업데이트하고, 필요하다면 새 오버레이를 선택합니다.
        /// </summary>
        /// <param name="detectionActive">현재 프레임에서 객체가 감지되었는지 여부</param>
        public void Update(bool detectionActive)
        {
            // 탐지 상태에 따라 활성 여부 설정
            isActive = detectionActive;

            if (!isActive || overlayImages.Count == 0)
            {
                // 감지되지 않거나 이미지가 없으면 오버레이를 비웁니다.
                ClearCurrentOverlay();
                return;
            }

            // 현재 오버레이가 없거나 설정된 시간 간격이 지났으면 새 오버레이로 교체합니다.
            if (currentOverlay == null || (DateTime.Now - lastChangeTime).TotalSeconds >= currentInterval)
            {
                ChangeOverlay();
            }
        }

        /// <summary>
        /// 오버레이를 초기화하거나 삭제합니다.
        /// </summary>
        private void ClearCurrentOverlay()
        {
            if (currentOverlay != null && !currentOverlay.IsDisposed)
            {
                currentOverlay.Dispose();
            }
            currentOverlay = null;
            positionSet = false;
        }

        /// <summary>
        /// 랜덤한 이미지와 간격을 선택하고 위치 재설정을 준비합니다.
        /// </summary>
        private void ChangeOverlay()
        {
            // 이전 오버레이를 해제
            ClearCurrentOverlay();

            // 새 이미지 선택
            int index = random.Next(overlayImages.Count);
            currentOverlay = overlayImages[index].Clone();

            // 10초에서 30초 사이의 랜덤한 간격 설정 (포함 범위)
            currentInterval = 10.0 + random.NextDouble() * 20.0;
            lastChangeTime = DateTime.Now;

            // 새 이미지의 위치는 아직 정하지 않음
            positionSet = false;
        }

        /// <summary>
        /// 활성 상태인 경우 오버레이 이미지를 주어진 프레임에 그립니다.
        /// </summary>
        /// <param name="frame">그릴 대상 프레임(Mat)</param>
        public void DrawOverlayOnFrame(Mat frame)
        {
            if (!isActive || currentOverlay == null)
            {
                return;
            }
            if (frame == null || frame.Empty())
            {
                return;
            }

            int overlayWidth = currentOverlay.Width;
            int overlayHeight = currentOverlay.Height;
            if (overlayWidth <= 0 || overlayHeight <= 0)
            {
                return;
            }
            if (overlayWidth > frame.Width || overlayHeight > frame.Height)
            {
                return;
            }

            // 오버레이 위치를 아직 설정하지 않았다면 랜덤 위치를 계산합니다.
            if (!positionSet)
            {
                int maxX = Math.Max(0, frame.Width - overlayWidth);
                int maxY = Math.Max(0, frame.Height - overlayHeight);
                int x = maxX == 0 ? 0 : random.Next(0, maxX + 1);
                int y = maxY == 0 ? 0 : random.Next(0, maxY + 1);
                currentPosition = new OpenCvSharp.Point(x, y);
                positionSet = true;
            }

            BlendMatOnFrame(frame, currentOverlay, currentPosition.X, currentPosition.Y);
        }

        /// <summary>
        /// 지정된 위치에 오버레이 이미지를 프레임에 블렌딩합니다. 알파 채널을 존중합니다.
        /// </summary>
        /// <param name="frame">기본 프레임</param>
        /// <param name="overlay">오버레이 이미지</param>
        /// <param name="x">왼쪽 상단 x 좌표</param>
        /// <param name="y">왼쪽 상단 y 좌표</param>
        private void BlendMatOnFrame(Mat frame, Mat overlay, int x, int y)
        {
            int w = overlay.Width;
            int h = overlay.Height;
            // 프레임 영역 지정
            if (w <= 0 || h <= 0) return;
            if (x < 0 || y < 0 || x + w > frame.Width || y + h > frame.Height) return;

            using var frameRoi = new Mat(frame, new Rect(x, y, w, h));

            if (overlay.Channels() == 4)
            {
                // 알파 채널이 있는 경우
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
                        c.Dispose();
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

        /// <summary>
        /// 리소스를 정리합니다.
        /// </summary>
        public void Dispose()
        {
            ClearCurrentOverlay();
            foreach (var img in overlayImages)
            {
                if (!img.IsDisposed)
                {
                    img.Dispose();
                }
            }
        }
    }
}
