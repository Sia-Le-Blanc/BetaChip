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
        #endregion

        private Bitmap currentBitmap;
        private readonly object bitmapLock = new object();

        public FullscreenOverlay()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            
            // ★★★★★★★★★★★★ 수정된 부분 ★★★★★★★★★★★★
            // 사용자의 요청대로 투명 키 값을 (3, 3, 3)으로 조정합니다.
            // 이렇게 하면 실제 이미지의 거의 모든 검은색 부분은 더 이상 투명해지지 않습니다.
            Color transparentColor = Color.FromArgb(3, 3, 3);
            this.BackColor = transparentColor;
            this.TransparencyKey = transparentColor;
            // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★

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
            try
            {
                SetWindowDisplayAffinity(this.Handle, WDA_EXCLUDEFROMCAPTURE);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 캡처 방지 설정 실패: {ex.Message}");
            }
        }

        public void UpdateFrame(Mat processedFrame)
        {
            if (processedFrame == null || processedFrame.Empty()) return;

            Bitmap newBitmap = BitmapConverter.ToBitmap(processedFrame);

            lock (bitmapLock)
            {
                currentBitmap?.Dispose();
                currentBitmap = newBitmap;
            }
            this.Invalidate();
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
                    e.Graphics.DrawImage(currentBitmap, this.ClientRectangle);
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