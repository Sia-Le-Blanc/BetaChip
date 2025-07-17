#nullable disable
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace MosaicCensorSystem.Capture
{
    /// <summary>
    /// 백그라운드 스레드를 사용하여 화면을 지속적으로 캡처하는 클래스 (안정화 버전)
    /// </summary>
    public class ScreenCapturer : IDisposable
    {
        #region Windows API
        [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hSrc, int nXSrc, int nYSrc, int dwRop);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        private const int SRCCOPY = 0x00CC0020;
        #endregion

        private readonly int screenWidth;
        private readonly int screenHeight;
        private readonly int screenLeft;
        private readonly int screenTop;

        private Mat currentFrame;
        private readonly object frameLock = new object();
        private Thread captureThread;
        private volatile bool isRunning;

        public ScreenCapturer()
        {
            screenLeft = SystemInformation.VirtualScreen.Left;
            screenTop = SystemInformation.VirtualScreen.Top;
            screenWidth = SystemInformation.VirtualScreen.Width;
            screenHeight = SystemInformation.VirtualScreen.Height;
            currentFrame = new Mat(screenHeight, screenWidth, MatType.CV_8UC3);
        }

        public void StartCapture()
        {
            if (isRunning) return;
            isRunning = true;
            captureThread = new Thread(CaptureThreadLoop) { IsBackground = true, Name = "ScreenCaptureThread" };
            captureThread.Start();
        }

        public void StopCapture()
        {
            isRunning = false;
            captureThread?.Join(500);
        }

        private void CaptureThreadLoop()
        {
            while (isRunning)
            {
                IntPtr desktopDC = IntPtr.Zero, memoryDC = IntPtr.Zero, hBitmap = IntPtr.Zero, oldBitmap = IntPtr.Zero;
                try
                {
                    desktopDC = GetWindowDC(GetDesktopWindow());
                    memoryDC = CreateCompatibleDC(desktopDC);
                    hBitmap = CreateCompatibleBitmap(desktopDC, screenWidth, screenHeight);
                    oldBitmap = SelectObject(memoryDC, hBitmap);

                    BitBlt(memoryDC, 0, 0, screenWidth, screenHeight, desktopDC, screenLeft, screenTop, SRCCOPY);
                    
                    using var bmp = Image.FromHbitmap(hBitmap);
                    using Mat newFrame = BitmapConverter.ToMat(bmp);
                    
                    lock (frameLock)
                    {
                        if(newFrame.Channels() == 4)
                            Cv2.CvtColor(newFrame, currentFrame, ColorConversionCodes.BGRA2BGR);
                        else
                            newFrame.CopyTo(currentFrame);
                    }
                }
                finally
                {
                    if (oldBitmap != IntPtr.Zero) SelectObject(memoryDC, oldBitmap);
                    if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                    if (memoryDC != IntPtr.Zero) DeleteObject(memoryDC);
                    if (desktopDC != IntPtr.Zero) ReleaseDC(GetDesktopWindow(), desktopDC);
                }
                // 캡처 간격을 줘서 CPU 사용률을 낮춤 (약 60FPS)
                Thread.Sleep(16); 
            }
        }

        public Mat GetFrame()
        {
            lock (frameLock)
            {
                return currentFrame.Clone();
            }
        }

        public void Dispose()
        {
            StopCapture();
            currentFrame?.Dispose();
        }
    }
}