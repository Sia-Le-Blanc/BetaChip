#nullable disable
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using MosaicCensorSystem.Utils;

// 별칭을 정의하여 System.Drawing과 OpenCvSharp의 Point/Size 충돌을 방지합니다.
using DrawingPoint = System.Drawing.Point;
using DrawingSize  = System.Drawing.Size;
using CvPoint      = OpenCvSharp.Point;
using CvSize       = OpenCvSharp.Size;

namespace MosaicCensorSystem.Overlay
{
    /// <summary>
    /// 모든 디스플레이 환경에서 안정적으로 동작하는 개선된 오버레이
    /// </summary>
    public class FullscreenOverlay : Form, IOverlay
    {
        #region Windows API
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
        private const uint WDA_NONE = 0x00000000;

        [DllImport("user32.dll")]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        private const uint LWA_COLORKEY = 0x00000001;
        #endregion

        private Bitmap currentBitmap;
        private readonly object bitmapLock = new object();
        private static readonly Color TRANSPARENCY_KEY = Color.FromArgb(255, 0, 255); // 마젠타

        private Rectangle originalBounds;
        private bool isCompatibilityMode = false;
        private DisplayCompatibility.DisplaySettings displaySettings;

        public FullscreenOverlay(Rectangle bounds) : this()
        {
            originalBounds = bounds;
            ApplyMonitorBounds();
        }

        public FullscreenOverlay()
        {
            // 디스플레이 호환성 설정 가져오기
            displaySettings = DisplayCompatibility.GetCurrentSettings();
            isCompatibilityMode = displaySettings?.HasDpiIssues ?? false;

            // 기본 Form 설정
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;

            // DPI 문제가 있는 경우 특별 처리
            if (isCompatibilityMode)
            {
                this.AutoScaleMode = AutoScaleMode.None;
                Console.WriteLine("[Overlay] 호환성 모드로 실행됨");
            }
            else
            {
                this.AutoScaleMode = AutoScaleMode.Dpi;
            }

            // 투명도 설정
            this.BackColor = TRANSPARENCY_KEY;
            this.TransparencyKey = TRANSPARENCY_KEY;

            // 더블 버퍼링 활성화
            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true
            );

            // 기본 위치 설정
            this.StartPosition = FormStartPosition.Manual;
            originalBounds = Screen.PrimaryScreen.Bounds;
        }

        private void ApplyMonitorBounds()
        {
            try
            {
                // 호환성 모드에서는 안전한 경계 사용
                Rectangle safeBounds = isCompatibilityMode
                    ? DisplayCompatibility.GetSafeOverlayBounds(originalBounds)
                    : originalBounds;

                // 명시적으로 System.Drawing의 Point와 Size를 사용
                this.Location = new DrawingPoint(safeBounds.X, safeBounds.Y);
                this.Size = new DrawingSize(safeBounds.Width, safeBounds.Height);
                this.WindowState = FormWindowState.Normal;

                Console.WriteLine($"[Overlay] Bounds 적용: {safeBounds}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Overlay] Bounds 적용 실패, 기본값 사용: {ex.Message}");
                // 실패 시 전체 화면으로 폴백
                this.WindowState = FormWindowState.Maximized;
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT;

                // 추가 플래그로 안정성 향상
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;

                return cp;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // 캡처 방지 설정: 오버레이가 화면 캡처에 포함되지 않도록 항상 설정합니다.
            try
            {
                // Windows 10(1703) 이상에서 지원되는 기능으로, 호환성 모드 여부와 관계없이 적용해줍니다.
                // 이 호출을 통해 오버레이가 BitBlt를 통해 캡처되지 않도록 합니다.
                SetWindowDisplayAffinity(this.Handle, WDA_EXCLUDEFROMCAPTURE);
            }
            catch (Exception ex)
            {
                // 일부 구형 환경에서는 DisplayAffinity 설정이 실패할 수 있지만 무시합니다.
                Console.WriteLine($"[Overlay] 캡처 방지 설정 실패 (무시됨): {ex.Message}");
            }

            // 레이어드 윈도우 속성 명시적 설정
            try
            {
                uint colorKey = (uint)ColorTranslator.ToWin32(TRANSPARENCY_KEY);
                SetLayeredWindowAttributes(this.Handle, colorKey, 255, LWA_COLORKEY);
            }
            catch { }
        }

        public void UpdateFrame(Mat processedFrame)
        {
            if (processedFrame == null || processedFrame.Empty()) return;

            try
            {
                using Mat transparentFrame = ConvertBlackToTransparent(processedFrame);

                // 호환성 모드에서는 리사이징 수행
                if (isCompatibilityMode && NeedsResize(transparentFrame))
                {
                    using Mat resizedFrame = ResizeFrameForDisplay(transparentFrame);
                    UpdateBitmap(resizedFrame);
                }
                else
                {
                    UpdateBitmap(transparentFrame);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Overlay] 프레임 업데이트 실패: {ex.Message}");
            }
        }

        private bool NeedsResize(Mat frame)
        {
            // 프레임 크기와 윈도우 크기가 다르면 리사이즈가 필요하다.
            // 차이를 1픽셀까지 허용하지만 그 이상이면 맞춰준다.
            return Math.Abs(frame.Width - this.ClientSize.Width) > 1 ||
                   Math.Abs(frame.Height - this.ClientSize.Height) > 1;
        }

        private Mat ResizeFrameForDisplay(Mat frame)
        {
            Mat resized = new Mat();
            Cv2.Resize(frame, resized,
                new CvSize(this.ClientSize.Width, this.ClientSize.Height),
                interpolation: InterpolationFlags.Nearest);
            return resized;
        }

        private void UpdateBitmap(Mat frame)
        {
            Bitmap newBitmap = BitmapConverter.ToBitmap(frame);

            if (this.InvokeRequired)
            {
                try
                {
                    this.BeginInvoke(new Action(() => SwapBitmap(newBitmap)));
                }
                catch (ObjectDisposedException)
                {
                    newBitmap?.Dispose();
                }
            }
            else
            {
                SwapBitmap(newBitmap);
            }
        }

        private void SwapBitmap(Bitmap newBitmap)
        {
            lock (bitmapLock)
            {
                var oldBitmap = currentBitmap;
                currentBitmap = newBitmap;
                oldBitmap?.Dispose();
            }
            this.Invalidate();
        }

        private Mat ConvertBlackToTransparent(Mat originalFrame)
        {
            Mat result = new Mat();

            // BGRA로 변환
            if (originalFrame.Channels() == 3)
            {
                Cv2.CvtColor(originalFrame, result, ColorConversionCodes.BGR2BGRA);
            }
            else
            {
                result = originalFrame.Clone();
            }

            using Mat mask = new Mat();
            Cv2.InRange(result, new Scalar(3, 3, 3, 0), new Scalar(3, 3, 3, 255), mask);
            result.SetTo(new Scalar(255, 0, 255, 255), mask); // 마젠타 (BGRA)

            return result;
        }

        public void SetMonitorBounds(int x, int y, int width, int height)
        {
            originalBounds = new Rectangle(x, y, width, height);

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => ApplyMonitorBounds()));
            }
            else
            {
                ApplyMonitorBounds();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            lock (bitmapLock)
            {
                // 배경을 투명색으로 채우기
                using (SolidBrush brush = new SolidBrush(this.TransparencyKey))
                {
                    e.Graphics.FillRectangle(brush, this.ClientRectangle);
                }

                // 비트맵 그리기
                if (currentBitmap != null && !currentBitmap.Size.IsEmpty)
                {
                    try
                    {
                        // 호환성 모드에서는 스트레치 그리기
                        if (isCompatibilityMode)
                        {
                            e.Graphics.DrawImage(currentBitmap, this.ClientRectangle);
                        }
                        else
                        {
                            // 명시적으로 DrawingPoint.Empty 사용
                            e.Graphics.DrawImage(currentBitmap, DrawingPoint.Empty);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Overlay] 그리기 실패: {ex.Message}");
                    }
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.Invalidate();
        }

        public new void Show()
        {
            try
            {
                base.Show();
                this.BringToFront();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Overlay] Show 실패: {ex.Message}");
            }
        }

        public new void Hide()
        {
            try
            {
                base.Hide();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Overlay] Hide 실패: {ex.Message}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (bitmapLock)
                {
                    currentBitmap?.Dispose();
                    currentBitmap = null;
                }
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 강제 호환성 모드 전환
        /// </summary>
        public void EnableCompatibilityMode()
        {
            isCompatibilityMode = true;
            this.AutoScaleMode = AutoScaleMode.None;
            ApplyMonitorBounds();
            Console.WriteLine("[Overlay] 호환성 모드 강제 활성화됨");
        }
    }
}