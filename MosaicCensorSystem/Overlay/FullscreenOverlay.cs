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
            this.BackColor = Color.Black;
            this.TransparencyKey = Color.Black;
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
                // 매번 그리기 전에 투명색(검은색)으로 배경을 완전히 지워 잔상을 제거합니다.
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