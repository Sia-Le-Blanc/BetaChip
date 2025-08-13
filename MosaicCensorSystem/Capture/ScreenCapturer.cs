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
        // DPI 관련 추가
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);
        
        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
        
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;
        private const int LOGPIXELSX = 88;
        private const int LOGPIXELSY = 90;

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
        private readonly int virtualX;
        private readonly int virtualY;
        private IntPtr hDesktopWnd;
        private IntPtr hDesktopDC;
        private IntPtr hMemoryDC;
        private IntPtr hBitmap;
        private IntPtr hOldBitmap;
        private IntPtr pPixelData; // 픽셀 데이터 메모리 포인터
        private bool disposed = false;

        // DPI 스케일링 정보
        public float DpiScaleX { get; private set; }
        public float DpiScaleY { get; private set; }

        public ScreenCapture()
        {
            // 가상 데스크톱 전체 크기 획득 (멀티 모니터 지원)
            virtualX = GetSystemMetrics(SM_XVIRTUALSCREEN);
            virtualY = GetSystemMetrics(SM_YVIRTUALSCREEN);
            width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            // DPI 정보 획득
            IntPtr dc = GetDC(IntPtr.Zero);
            int dpiX = GetDeviceCaps(dc, LOGPIXELSX);
            int dpiY = GetDeviceCaps(dc, LOGPIXELSY);
            ReleaseDC(IntPtr.Zero, dc);

            DpiScaleX = dpiX / 96.0f;
            DpiScaleY = dpiY / 96.0f;

            Console.WriteLine($"가상 데스크톱 물리적 해상도: {width}x{height} at ({virtualX}, {virtualY})");
            Console.WriteLine($"DPI 스케일: {DpiScaleX:F2}x{DpiScaleY:F2}");

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
            
            // 가상 데스크톱 전체 영역 캡처 (멀티 모니터 지원)
            BitBlt(hMemoryDC, 0, 0, width, height, hDesktopDC, virtualX, virtualY, SRCCOPY);

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