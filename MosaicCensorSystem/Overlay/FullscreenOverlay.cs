#nullable disable
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace MosaicCensorSystem.Overlay
{
    public class FullscreenOverlay : Form, IOverlay
    {
        #region Windows API
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
        [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
        
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;
        #endregion

        private Bitmap currentBitmap;
        private readonly object bitmapLock = new object();
        private float dpiScaleX = 1.0f;
        private float dpiScaleY = 1.0f;

        // ★★★ 투명키를 매젠타로 설정 (거의 사용되지 않는 색상) ★★★
        private static readonly Color TRANSPARENCY_KEY = Color.FromArgb(255, 0, 255); // 매젠타

        public FullscreenOverlay()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            
            this.BackColor = TRANSPARENCY_KEY;
            this.TransparencyKey = TRANSPARENCY_KEY;

            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT;
                return cp;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            UpdateDpiScale();
            try
            {
                SetWindowDisplayAffinity(this.Handle, WDA_EXCLUDEFROMCAPTURE);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 캡처 방지 설정 실패: {ex.Message}");
            }
        }

        private void UpdateDpiScale()
        {
            try
            {
                IntPtr monitor = MonitorFromWindow(this.Handle, MONITOR_DEFAULTTONEAREST);
                GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY);
                dpiScaleX = dpiX / 96.0f;
                dpiScaleY = dpiY / 96.0f;
            }
            catch
            {
                dpiScaleX = dpiScaleY = 1.0f;
            }
        }

        public void UpdateFrame(Mat processedFrame)
        {
            if (processedFrame == null || processedFrame.Empty()) return;

            // ★★★ 투명 처리를 위한 전처리: 검은색을 매젠타로 변환 ★★★
            Mat transparentFrame = ConvertBlackToTransparent(processedFrame);
            Bitmap newBitmap = BitmapConverter.ToBitmap(transparentFrame);
            transparentFrame.Dispose();

            lock (bitmapLock)
            {
                currentBitmap?.Dispose();
                currentBitmap = newBitmap;
            }
            this.Invalidate();
        }

        // ★★★ 검은색 픽셀을 투명키(매젠타)로 변환하는 메서드 ★★★
        private Mat ConvertBlackToTransparent(Mat originalFrame)
        {
            Mat result = new Mat();
            
            // BGRA 채널로 변환 (투명도 지원)
            if (originalFrame.Channels() == 3)
            {
                Cv2.CvtColor(originalFrame, result, ColorConversionCodes.BGR2BGRA);
            }
            else
            {
                result = originalFrame.Clone();
            }

            // 검은색 픽셀들을 매젠타로 변환하여 투명하게 만들기
            // 하지만 검열된 검은색 박스는 보존하기 위해 완전한 검은색 (0,0,0)만 처리
            Mat mask = new Mat();
            Mat blackMask = new Mat();
            
            // 완전한 검은색 영역 찾기 (B=0, G=0, R=0)
            Cv2.InRange(result, new Scalar(0, 0, 0, 0), new Scalar(2, 2, 2, 255), blackMask);
            
            // 검은색 영역을 매젠타로 변경
            result.SetTo(new Scalar(255, 0, 255, 255), blackMask); // 매젠타 (BGRA)

            mask.Dispose();
            blackMask.Dispose();
            
            return result;
        }

        public void SetMonitorBounds(int x, int y, int width, int height)
        {
            this.StartPosition = FormStartPosition.Manual;
            
            // DPI 스케일링 적용
            int scaledX = (int)(x / dpiScaleX);
            int scaledY = (int)(y / dpiScaleY);
            int scaledWidth = (int)(width / dpiScaleX);
            int scaledHeight = (int)(height / dpiScaleY);
            
            this.Location = new System.Drawing.Point(scaledX, scaledY);
            this.Size = new System.Drawing.Size(scaledWidth, scaledHeight);
            this.WindowState = FormWindowState.Normal;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            lock (bitmapLock)
            {
                // 배경을 투명키로 채우기
                using (SolidBrush brush = new SolidBrush(this.TransparencyKey))
                {
                    e.Graphics.FillRectangle(brush, this.ClientRectangle);
                }

                if (currentBitmap != null)
                {
                    // DPI 스케일링을 고려한 이미지 그리기
                    Rectangle destRect = new Rectangle(0, 0, this.ClientRectangle.Width, this.ClientRectangle.Height);
                    e.Graphics.DrawImage(currentBitmap, destRect);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (bitmapLock)
                {
                    currentBitmap?.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}