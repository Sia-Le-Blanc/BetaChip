using System;
using System.Drawing;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace MosaicCensorSystem.Capture
{
    public class ScreenCapture : IDisposable
    {
        #region Win32 API P/Invoke
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("gdi32.dll")] private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int LOGPIXELSX = 88;
        private const int LOGPIXELSY = 90;

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO { public BITMAPINFOHEADER bmiHeader; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public RGBQUAD[] bmiColors; }
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER { public uint biSize; public int biWidth; public int biHeight; public ushort biPlanes; public ushort biBitCount; public uint biCompression; public uint biSizeImage; public int biXPelsPerMeter; public int biYPelsPerMeter; public uint biClrUsed; public uint biClrImportant; }
        [StructLayout(LayoutKind.Sequential)]
        public struct RGBQUAD { public byte rgbBlue; public byte rgbGreen; public byte rgbRed; public byte rgbReserved; }
        
        [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr hdc, [In] ref BITMAPINFO pbmi, uint pila, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);
        [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hWnd);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hObjectHDC, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjSourceHDC, int nXSrc, int nYSrc, int dwRop);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hDC);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        private const int SRCCOPY = 0x00CC0020;
        private const int BI_RGB = 0;
        #endregion

        private int width;
        private int height;
        private int virtualX;
        private int virtualY;
        private IntPtr hDesktopWnd;
        private IntPtr hDesktopDC;
        private IntPtr hMemoryDC;
        private IntPtr hBitmap;
        private IntPtr hOldBitmap;
        private IntPtr pPixelData;
        private bool disposed = false;
        
        public ScreenCapture()
        {
            virtualX = GetSystemMetrics(SM_XVIRTUALSCREEN);
            virtualY = GetSystemMetrics(SM_YVIRTUALSCREEN);
            width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
            InitializeCapture();
        }

        public ScreenCapture(Rectangle monitorBounds)
        {
            virtualX = monitorBounds.X;
            virtualY = monitorBounds.Y;
            width = monitorBounds.Width;
            height = monitorBounds.Height;
            InitializeCapture();
        }

        private void InitializeCapture()
        {
            hDesktopWnd = GetDesktopWindow();
            hDesktopDC = GetWindowDC(hDesktopWnd);
            hMemoryDC = CreateCompatibleDC(hDesktopDC);

            var bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height;
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = BI_RGB;

            hBitmap = CreateDIBSection(hMemoryDC, ref bmi, 0, out pPixelData, IntPtr.Zero, 0);
            hOldBitmap = SelectObject(hMemoryDC, hBitmap);
        }



        public Mat GetFrame()
        {
            if (disposed) return null;
            
            BitBlt(hMemoryDC, 0, 0, width, height, hDesktopDC, virtualX, virtualY, SRCCOPY);
            return new Mat(height, width, MatType.CV_8UC4, pPixelData);
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // ★★★ [수정] 자원 해제 순서를 바로잡은 최종 Dispose 메서드 ★★★
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // 관리되는 리소스 정리 (현재 없음)
                }

                // 관리되지 않는 리소스(Win32 핸들) 정리
                // 1. 메모리 DC에서 우리가 만든 비트맵을 제거하고 원래 비트맵으로 되돌립니다.
                if (hMemoryDC != IntPtr.Zero && hOldBitmap != IntPtr.Zero)
                {
                    SelectObject(hMemoryDC, hOldBitmap);
                    hOldBitmap = IntPtr.Zero;
                }

                // 2. 이제 자유로워진 우리가 만든 비트맵을 삭제합니다.
                if (hBitmap != IntPtr.Zero)
                {
                    DeleteObject(hBitmap);
                    hBitmap = IntPtr.Zero;
                }

                // 3. 비트맵이 모두 정리된 메모리 DC를 삭제합니다.
                if (hMemoryDC != IntPtr.Zero)
                {
                    DeleteDC(hMemoryDC);
                    hMemoryDC = IntPtr.Zero;
                }

                // 4. 마지막으로 데스크탑 DC를 해제합니다.
                if (hDesktopDC != IntPtr.Zero)
                {
                    ReleaseDC(hDesktopWnd, hDesktopDC);
                    hDesktopDC = IntPtr.Zero;
                }
                
                disposed = true;
            }
        }

        ~ScreenCapture()
        {
            Dispose(false);
        }
    }
}