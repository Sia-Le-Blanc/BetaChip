using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpenCvSharp;

namespace MosaicCensorSystem.Capture
{
    public class ScreenCapture : IDisposable
    {
        #region Win32 API P/Invoke
        // CreateDIBSection 함수와 BITMAPINFO 구조체를 추가합니다.
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public RGBQUAD[] bmiColors;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RGBQUAD
        {
            public byte rgbBlue;
            public byte rgbGreen;
            public byte rgbRed;
            public byte rgbReserved;
        }

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, [In] ref BITMAPINFO pbmi, uint pila, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);
        
        // --- 기존 함수들 ---
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

        private readonly int width;
        private readonly int height;
        private IntPtr hDesktopWnd;
        private IntPtr hDesktopDC;
        private IntPtr hMemoryDC;
        private IntPtr hBitmap;
        private IntPtr hOldBitmap;
        private IntPtr pPixelData; // 픽셀 데이터 메모리 포인터
        private bool disposed = false;

        public ScreenCapture()
        {
            Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
            width = screenBounds.Width;
            height = screenBounds.Height;

            hDesktopWnd = GetDesktopWindow();
            hDesktopDC = GetWindowDC(hDesktopWnd);
            hMemoryDC = CreateCompatibleDC(hDesktopDC);

            // CreateDIBSection을 사용하기 위한 BITMAPINFO 설정
            var bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height; // Top-down DIB
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32; // 32비트 (BGRA)
            bmi.bmiHeader.biCompression = BI_RGB;

            // CreateCompatibleBitmap 대신 CreateDIBSection을 사용해 pPixelData 포인터를 직접 얻습니다.
            hBitmap = CreateDIBSection(hMemoryDC, ref bmi, 0, out pPixelData, IntPtr.Zero, 0);
            hOldBitmap = SelectObject(hMemoryDC, hBitmap);
        }

        public Mat GetFrame()
        {
            if (disposed) return null;
            
            BitBlt(hMemoryDC, 0, 0, width, height, hDesktopDC, 0, 0, SRCCOPY);

            // Bitmap 객체 변환 없이, 메모리 포인터(pPixelData)로 직접 Mat 객체를 생성합니다.
            // MatType.CV_8UC4는 8비트 부호없는 4채널(BGRA) 이미지를 의미합니다.
            return new Mat(height, width, MatType.CV_8UC4, pPixelData);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                SelectObject(hMemoryDC, hOldBitmap);
                DeleteObject(hBitmap);
                DeleteDC(hMemoryDC);
                ReleaseDC(hDesktopWnd, hDesktopDC);
                disposed = true;
            }
        }
    }
}