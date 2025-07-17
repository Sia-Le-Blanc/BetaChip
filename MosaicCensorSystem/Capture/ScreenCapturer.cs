#nullable disable
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace MosaicCensorSystem.Capture
{
    /// <summary>
    /// GDI를 사용하여 화면을 캡처하는 단순화된 클래스.
    /// 스레드 없이 GetFrame() 호출 시점에 직접 캡처를 수행.
    /// </summary>
    public class ScreenCapturer : IDisposable
    {
        #region Windows API
        [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, int dwRop);
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

        public ScreenCapturer()
        {
            try
            {
                // 전체 가상 화면의 크기와 위치를 가져옵니다.
                screenLeft = SystemInformation.VirtualScreen.Left;
                screenTop = SystemInformation.VirtualScreen.Top;
                screenWidth = SystemInformation.VirtualScreen.Width;
                screenHeight = SystemInformation.VirtualScreen.Height;
                Console.WriteLine($"✅ 캡처 영역 설정: {screenWidth}x{screenHeight} at ({screenLeft},{screenTop})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 화면 정보 가져오기 실패, 기본값(1920x1080) 사용: {ex.Message}");
                screenWidth = 1920;
                screenHeight = 1080;
            }
        }

        /// <summary>
        /// 현재 화면을 캡처하여 Mat 객체로 반환합니다. (직접 호출 방식)
        /// </summary>
        public Mat GetFrame()
        {
            IntPtr desktopDC = IntPtr.Zero;
            IntPtr memoryDC = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            try
            {
                // GDI 핸들 가져오기
                desktopDC = GetWindowDC(GetDesktopWindow());
                memoryDC = CreateCompatibleDC(desktopDC);
                hBitmap = CreateCompatibleBitmap(desktopDC, screenWidth, screenHeight);
                oldBitmap = SelectObject(memoryDC, hBitmap);

                // 화면 데이터를 비트맵으로 복사
                if (!BitBlt(memoryDC, 0, 0, screenWidth, screenHeight, desktopDC, screenLeft, screenTop, SRCCOPY))
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }

                // 비트맵을 OpenCV Mat 객체로 변환
                using var screenBitmap = Image.FromHbitmap(hBitmap);
                Mat frame = BitmapConverter.ToMat(screenBitmap);
                
                // 알파 채널(4채널)이 있는 경우, 3채널(BGR)로 변환
                if (frame.Channels() == 4)
                {
                    Cv2.CvtColor(frame, frame, ColorConversionCodes.BGRA2BGR);
                }
                
                return frame;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 캡처 실패: {ex.Message}");
                // 실패 시 빈 Mat 객체 대신 null을 반환하여 호출 측에서 처리하도록 함
                return null;
            }
            finally
            {
                // 리소스 누수 방지를 위해 모든 GDI 핸들 정리
                if (oldBitmap != IntPtr.Zero) SelectObject(memoryDC, oldBitmap);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (memoryDC != IntPtr.Zero) DeleteObject(memoryDC);
                if (desktopDC != IntPtr.Zero) ReleaseDC(GetDesktopWindow(), desktopDC);
            }
        }

        public void Dispose()
        {
            // 이 클래스는 특별히 정리할 관리 리소스가 없습니다.
        }
    }
}