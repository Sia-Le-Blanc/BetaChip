#nullable disable
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpenCvSharp;
using MosaicCensorSystem.Utils;

// 별칭 정의
using DrawingPoint = System.Drawing.Point;
using DrawingSize  = System.Drawing.Size;
using CvSize       = OpenCvSharp.Size;

namespace MosaicCensorSystem.Overlay
{
    public class FullscreenOverlay : Form, IOverlay
    {
        #region Windows API
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref DrawingPoint pptDst, ref DrawingSize psize, IntPtr hdcSrc, ref DrawingPoint pptSrc, uint crKey, [In] ref BLENDFUNCTION pblend, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct BLENDFUNCTION { public byte BlendOp; public byte BlendFlags; public byte SourceConstantAlpha; public byte AlphaFormat; }

        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;
        private const uint ULW_ALPHA = 0x00000002;
        #endregion

        private readonly object bitmapLock = new object();
        private Rectangle originalBounds;
        private bool isCompatibilityMode = false;
        
        private Mat _resizedFrame;
        private Bitmap _bitmapBuffer;

        public FullscreenOverlay(Rectangle bounds) : this()
        {
            originalBounds = bounds;
            ApplyMonitorBounds();
        }

        public FullscreenOverlay()
        {
            isCompatibilityMode = UserSettings.IsCompatibilityModeEnabled();

            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.Black;

            if (isCompatibilityMode) this.AutoScaleMode = AutoScaleMode.None;
            else this.AutoScaleMode = AutoScaleMode.Dpi;
        }

        private void ApplyMonitorBounds()
        {
            try {
                Rectangle safeBounds = isCompatibilityMode ? DisplayCompatibility.GetSafeOverlayBounds(originalBounds) : originalBounds;
                this.Location = new DrawingPoint(safeBounds.X, safeBounds.Y);
                this.Size = new DrawingSize(safeBounds.Width, safeBounds.Height);
                this.WindowState = FormWindowState.Normal;
            } catch { this.WindowState = FormWindowState.Maximized; }
        }

        protected override CreateParams CreateParams
        {
            get {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            try { SetWindowDisplayAffinity(this.Handle, WDA_EXCLUDEFROMCAPTURE); } catch { }
        }

        public unsafe void UpdateFrame(Mat processedFrame)
        {
            if (processedFrame == null || processedFrame.Empty() || !this.IsHandleCreated || this.IsDisposed) return;

            DrawingSize targetSize = new DrawingSize(this.ClientSize.Width, this.ClientSize.Height);
            Mat src = processedFrame;

            if (isCompatibilityMode || Math.Abs(processedFrame.Width - targetSize.Width) > 1 || Math.Abs(processedFrame.Height - targetSize.Height) > 1) {
                _resizedFrame ??= new Mat();
                Cv2.Resize(processedFrame, _resizedFrame, new CvSize(targetSize.Width, targetSize.Height), interpolation: InterpolationFlags.Nearest);
                src = _resizedFrame;
            }

            UpdateOverlayDirect(src);
        }

        private unsafe void UpdateOverlayDirect(Mat frame)
        {
            if (frame.Channels() != 4) return;

            lock (bitmapLock) {
                if (_bitmapBuffer == null || _bitmapBuffer.Width != frame.Width || _bitmapBuffer.Height != frame.Height) {
                    _bitmapBuffer?.Dispose();
                    _bitmapBuffer = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppArgb);
                }

                BitmapData data = _bitmapBuffer.LockBits(new Rectangle(0, 0, frame.Width, frame.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try {
                    unsafe {
                        byte* s = (byte*)frame.Data;
                        byte* d = (byte*)data.Scan0;
                        int sStep = (int)frame.Step();
                        int dStride = data.Stride;
                        for (int y = 0; y < frame.Height; y++) {
                            byte* sRow = s + y * sStep;
                            byte* dRow = d + y * dStride;
                            for (int x = 0; x < frame.Width; x++) {
                                dRow[x * 4 + 0] = sRow[x * 4 + 0]; // B
                                dRow[x * 4 + 1] = sRow[x * 4 + 1]; // G
                                dRow[x * 4 + 2] = sRow[x * 4 + 2]; // R
                                dRow[x * 4 + 3] = 255;             // Force Opaque Alpha
                            }
                        }
                    }
                } finally { _bitmapBuffer.UnlockBits(data); }

                IntPtr sDC = GetDC(IntPtr.Zero);
                IntPtr mDC = CreateCompatibleDC(sDC);
                IntPtr hBmp = _bitmapBuffer.GetHbitmap(Color.FromArgb(0));
                IntPtr oldBmp = SelectObject(mDC, hBmp);

                try {
                    DrawingSize size = new DrawingSize(_bitmapBuffer.Width, _bitmapBuffer.Height);
                    DrawingPoint pS = new DrawingPoint(0, 0);
                    DrawingPoint pD = this.Location;
                    BLENDFUNCTION blend = new BLENDFUNCTION { BlendOp = AC_SRC_OVER, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
                    UpdateLayeredWindow(this.Handle, sDC, ref pD, ref size, mDC, ref pS, 0, ref blend, ULW_ALPHA);
                } finally {
                    SelectObject(mDC, oldBmp);
                    DeleteObject(hBmp);
                    DeleteDC(mDC);
                    ReleaseDC(IntPtr.Zero, sDC);
                }
            }
        }

        public void SetMonitorBounds(int x, int y, int width, int height)
        {
            originalBounds = new Rectangle(x, y, width, height);
            if (this.InvokeRequired) this.BeginInvoke(new Action(() => ApplyMonitorBounds()));
            else ApplyMonitorBounds();
        }

        protected override void OnPaint(PaintEventArgs e) { /* UpdateLayeredWindow 사용하므로 무시 */ }

        public new void Show() { try { base.Show(); this.BringToFront(); } catch { } }
        public new void Hide() { try { base.Hide(); } catch { } }

        protected override void Dispose(bool disposing)
        {
            if (disposing) {
                lock (bitmapLock) {
                    _bitmapBuffer?.Dispose(); _bitmapBuffer = null;
                }
                _resizedFrame?.Dispose(); _resizedFrame = null;
            }
            base.Dispose(disposing);
        }

        public void EnableCompatibilityMode()
        {
            isCompatibilityMode = true;
            this.AutoScaleMode = AutoScaleMode.None;
            ApplyMonitorBounds();
        }
    }
}
