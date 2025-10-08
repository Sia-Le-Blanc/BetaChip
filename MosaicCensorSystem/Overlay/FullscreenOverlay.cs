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
        // ★★★ 생략되었던 Windows API 선언부 전체 복원 ★★★
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

        private static readonly Color TRANSPARENCY_KEY = Color.FromArgb(255, 0, 255); // 매젠타
        
        public FullscreenOverlay(Rectangle bounds) : this()
        {
            UpdateDpiScale(); 
            SetMonitorBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }

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

        public void UpdateFrame(Mat processedFrame)
        {
            if (processedFrame == null || processedFrame.Empty()) return;

            using Mat transparentFrame = ConvertBlackToTransparent(processedFrame);
            Bitmap newBitmap = BitmapConverter.ToBitmap(transparentFrame);

            if (this.InvokeRequired)
            {
                try
                {
                    this.Invoke(new Action(() => UpdateBitmapAndInvalidate(newBitmap)));
                }
                catch (ObjectDisposedException)
                {
                    newBitmap.Dispose();
                }
            }
            else
            {
                UpdateBitmapAndInvalidate(newBitmap);
            }
        }

        private void UpdateBitmapAndInvalidate(Bitmap newBitmap)
        {
            lock (bitmapLock)
            {
                currentBitmap?.Dispose();
                currentBitmap = newBitmap;
            }
            this.Invalidate();
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

        private Mat ConvertBlackToTransparent(Mat originalFrame)
        {
            Mat result = new Mat();
            
            if (originalFrame.Channels() == 3)
            {
                Cv2.CvtColor(originalFrame, result, ColorConversionCodes.BGR2BGRA);
            }
            else
            {
                result = originalFrame.Clone();
            }

            using Mat transparencyMask = new Mat();
            Cv2.InRange(result, new Scalar(3, 3, 3, 0), new Scalar(3, 3, 3, 255), transparencyMask);
            result.SetTo(new Scalar(255, 0, 255, 255), transparencyMask); // 매젠타 (BGRA)
            
            return result;
        }

        public void SetMonitorBounds(int x, int y, int width, int height)
        {
            this.StartPosition = FormStartPosition.Manual;
            
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
                using (SolidBrush brush = new SolidBrush(this.TransparencyKey))
                {
                    e.Graphics.FillRectangle(brush, this.ClientRectangle);
                }

                if (currentBitmap != null)
                {
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