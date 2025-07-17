#nullable disable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace MosaicCensorSystem.Capture
{
    /// <summary>
    /// 화면 캡처 인터페이스
    /// </summary>
    public interface ICapturer
    {
        Mat GetFrame();
        void StartCaptureThread();
        void StopCaptureThread();
        void SetExcludeHwnd(IntPtr hwnd);
        void AddExcludeRegion(int x, int y, int width, int height);
        void ClearExcludeRegions();
    }

    /// <summary>
    /// 크래시 방지 안전한 화면 캡처 클래스
    /// </summary>
    public class ScreenCapturer : ICapturer, IDisposable
    {
        #region Windows API (안전 버전)
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDesktopWindow();
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, 
            IntPtr hObjectSource, int nXSrc, int nYSrc, int dwRop);
        
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);
        
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);
        
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        private const int SRCCOPY = 0x00CC0020;
        #endregion

        private readonly Dictionary<string, object> config;
        private readonly double captureDownscale;
        private readonly bool debugMode;
        private readonly int debugSaveInterval;

        private readonly int screenWidth;
        private readonly int screenHeight;
        private readonly int screenLeft;
        private readonly int screenTop;
        private readonly int captureWidth;
        private readonly int captureHeight;

        private readonly Rectangle monitor;
        private Mat prevFrame;
        private int frameCount = 0;

        // 스레드 안전 큐 대신 간단한 접근 방식
        private Mat currentFrame;
        private readonly object frameLock = new object();
        private volatile bool isDisposed = false;
        
        // 스레드 관리 (간소화)
        private Thread captureThread;
        private volatile bool shouldRun = false;

        private IntPtr excludeHwnd = IntPtr.Zero;
        private readonly List<Rectangle> excludeRegions = new List<Rectangle>();
        private readonly string debugDir = "debug_captures";

        public ScreenCapturer(Dictionary<string, object> config = null)
        {
            try
            {
                Console.WriteLine("🔧 안전한 ScreenCapturer 초기화 시작");
                
                this.config = config ?? new Dictionary<string, object>();
                
                // 안전한 타입 변환
                captureDownscale = GetConfigValue("downscale", 1.0);
                debugMode = GetConfigValue("debug_mode", false);
                debugSaveInterval = GetConfigValue("debug_save_interval", 300);

                Console.WriteLine($"📊 설정: 다운스케일={captureDownscale}, 디버그={debugMode}");

                // 화면 크기 가져오기 (안전하게)
                try
                {
                    screenLeft = SystemInformation.VirtualScreen.Left;
                    screenTop = SystemInformation.VirtualScreen.Top;
                    screenWidth = SystemInformation.VirtualScreen.Width;
                    screenHeight = SystemInformation.VirtualScreen.Height;
                    
                    Console.WriteLine($"📺 화면 영역: ({screenLeft}, {screenTop}) - {screenWidth}x{screenHeight}");
                }
                catch (Exception screenEx)
                {
                    Console.WriteLine($"⚠️ 화면 정보 가져오기 실패, 기본값 사용: {screenEx.Message}");
                    screenLeft = 0;
                    screenTop = 0;
                    screenWidth = 1920;
                    screenHeight = 1080;
                }

                captureWidth = (int)(screenWidth * captureDownscale);
                captureHeight = (int)(screenHeight * captureDownscale);

                Console.WriteLine($"✅ 캡처 크기: {captureWidth}x{captureHeight}");

                monitor = new Rectangle(screenLeft, screenTop, screenWidth, screenHeight);

                if (debugMode)
                {
                    try
                    {
                        Directory.CreateDirectory(debugDir);
                        Console.WriteLine($"📁 디버그 디렉토리 생성: {debugDir}");
                    }
                    catch (Exception dirEx)
                    {
                        Console.WriteLine($"⚠️ 디버그 디렉토리 생성 실패: {dirEx.Message}");
                    }
                }

                // 기본 프레임 생성 (검은색)
                try
                {
                    currentFrame = Mat.Zeros(captureHeight, captureWidth, MatType.CV_8UC3);
                    Console.WriteLine("✅ 기본 프레임 생성됨");
                }
                catch (Exception frameEx)
                {
                    Console.WriteLine($"❌ 기본 프레임 생성 실패: {frameEx.Message}");
                    currentFrame = null;
                }

                Console.WriteLine("✅ 안전한 ScreenCapturer 초기화 완료");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ScreenCapturer 초기화 실패: {ex.Message}");
                throw;
            }
        }

        private T GetConfigValue<T>(string key, T defaultValue)
        {
            try
            {
                if (config != null && config.ContainsKey(key))
                {
                    return (T)Convert.ChangeType(config[key], typeof(T));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 설정 값 변환 실패 ({key}): {ex.Message}");
            }
            return defaultValue;
        }

        public void SetExcludeHwnd(IntPtr hwnd)
        {
            excludeHwnd = hwnd;
            Console.WriteLine($"✅ 제외 윈도우 핸들 설정: {hwnd}");
        }

        public void AddExcludeRegion(int x, int y, int width, int height)
        {
            excludeRegions.Add(new Rectangle(x, y, width, height));
            Console.WriteLine($"✅ 제외 영역 추가: ({x}, {y}, {width}, {height})");
        }

        public void ClearExcludeRegions()
        {
            excludeRegions.Clear();
        }

        public void StartCaptureThread()
        {
            try
            {
                if (captureThread != null && captureThread.IsAlive)
                {
                    Console.WriteLine("⚠️ 캡처 스레드가 이미 실행 중입니다.");
                    return;
                }

                shouldRun = true;
                captureThread = new Thread(SafeCaptureThreadFunc)
                {
                    Name = "SafeScreenCaptureThread",
                    Priority = ThreadPriority.Normal,
                    IsBackground = true
                };
                captureThread.Start();
                Console.WriteLine("✅ 안전한 캡처 스레드 시작됨");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 캡처 스레드 시작 실패: {ex.Message}");
            }
        }

        public void StopCaptureThread()
        {
            try
            {
                shouldRun = false;
                
                if (captureThread != null && captureThread.IsAlive)
                {
                    captureThread.Join(1000);
                    Console.WriteLine("✅ 캡처 스레드 중지됨");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 캡처 스레드 중지 오류: {ex.Message}");
            }
        }

        private void SafeCaptureThreadFunc()
        {
            Console.WriteLine("🔄 안전한 캡처 스레드 시작");
            int consecutiveErrors = 0;
            const int maxConsecutiveErrors = 5;

            while (shouldRun && !isDisposed)
            {
                try
                {
                    var frame = SafeCaptureScreen();
                    
                    if (frame != null && !frame.Empty())
                    {
                        lock (frameLock)
                        {
                            if (!isDisposed)
                            {
                                currentFrame?.Dispose();
                                currentFrame = frame;
                                frameCount++;
                                consecutiveErrors = 0;
                            }
                            else
                            {
                                frame.Dispose();
                            }
                        }
                    }
                    else
                    {
                        consecutiveErrors++;
                        if (consecutiveErrors > maxConsecutiveErrors)
                        {
                            Console.WriteLine($"❌ 연속 {consecutiveErrors}회 캡처 실패 - 긴 대기");
                            Thread.Sleep(1000);
                            consecutiveErrors = 0;
                        }
                    }
                    
                    Thread.Sleep(33); // ~30fps
                }
                catch (Exception e)
                {
                    consecutiveErrors++;
                    Console.WriteLine($"❌ 캡처 스레드 오류: {e.Message}");
                    
                    if (consecutiveErrors > maxConsecutiveErrors)
                    {
                        Console.WriteLine($"❌ 치명적 오류 - 캡처 스레드 일시 정지");
                        Thread.Sleep(2000);
                        consecutiveErrors = 0;
                    }
                    else
                    {
                        Thread.Sleep(500);
                    }
                }
            }

            Console.WriteLine("🛑 안전한 캡처 스레드 종료");
        }

        /// <summary>
        /// 안전한 화면 캡처 메서드 (크래시 방지)
        /// </summary>
        // ScreenCapturer.cs의 SafeCaptureScreen 메서드만 디버깅 강화 버전으로 교체
        private Mat SafeCaptureScreen()
        {
            IntPtr desktopDC = IntPtr.Zero;
            IntPtr memoryDC = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;
            Bitmap screenBitmap = null;

            try
            {
                Console.WriteLine($"🔍 [프레임 #{frameCount}] 화면 캡처 시작");
                
                // 1단계: 데스크톱 윈도우 핸들 가져오기
                IntPtr desktopWindow = GetDesktopWindow();
                if (desktopWindow == IntPtr.Zero)
                {
                    Console.WriteLine("❌ GetDesktopWindow 실패");
                    return CreateBlackFrame("GetDesktopWindow 실패");
                }
                Console.WriteLine($"✅ 데스크톱 윈도우 핸들: {desktopWindow}");
                
                // 2단계: 데스크톱 DC 가져오기
                desktopDC = GetWindowDC(desktopWindow);
                if (desktopDC == IntPtr.Zero)
                {
                    Console.WriteLine("❌ GetWindowDC 실패");
                    return CreateBlackFrame("GetWindowDC 실패");
                }
                Console.WriteLine($"✅ 데스크톱 DC: {desktopDC}");

                // 3단계: 메모리 DC 생성
                memoryDC = CreateCompatibleDC(desktopDC);
                if (memoryDC == IntPtr.Zero)
                {
                    Console.WriteLine("❌ CreateCompatibleDC 실패");
                    return CreateBlackFrame("CreateCompatibleDC 실패");
                }
                Console.WriteLine($"✅ 메모리 DC: {memoryDC}");

                // 4단계: 호환 비트맵 생성
                Console.WriteLine($"📐 비트맵 크기: {screenWidth}x{screenHeight}");
                hBitmap = CreateCompatibleBitmap(desktopDC, screenWidth, screenHeight);
                if (hBitmap == IntPtr.Zero)
                {
                    Console.WriteLine("❌ CreateCompatibleBitmap 실패");
                    return CreateBlackFrame("CreateCompatibleBitmap 실패");
                }
                Console.WriteLine($"✅ 호환 비트맵: {hBitmap}");

                // 5단계: 비트맵 선택
                oldBitmap = SelectObject(memoryDC, hBitmap);
                if (oldBitmap == IntPtr.Zero)
                {
                    Console.WriteLine("❌ SelectObject 실패");
                    return CreateBlackFrame("SelectObject 실패");
                }
                Console.WriteLine($"✅ 이전 비트맵: {oldBitmap}");

                // 6단계: 화면 복사 (중요!)
                Console.WriteLine($"📋 BitBlt 매개변수:");
                Console.WriteLine($"  대상: memoryDC={memoryDC}, 위치=(0,0), 크기=({screenWidth},{screenHeight})");
                Console.WriteLine($"  소스: desktopDC={desktopDC}, 위치=({screenLeft},{screenTop})");
                
                bool bitBltResult = BitBlt(memoryDC, 0, 0, screenWidth, screenHeight, 
                    desktopDC, screenLeft, screenTop, SRCCOPY);
                
                if (!bitBltResult)
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine($"❌ BitBlt 실패 - 오류 코드: {error}");
                    return CreateBlackFrame($"BitBlt 실패 (오류: {error})");
                }
                Console.WriteLine("✅ BitBlt 성공!");

                // 7단계: Bitmap 생성
                try
                {
                    Console.WriteLine("🖼️ Bitmap 생성 시도...");
                    screenBitmap = Image.FromHbitmap(hBitmap);
                    Console.WriteLine($"✅ Bitmap 생성 성공: {screenBitmap.Width}x{screenBitmap.Height}");
                }
                catch (Exception bitmapEx)
                {
                    Console.WriteLine($"❌ Bitmap 생성 실패: {bitmapEx.Message}");
                    return CreateBlackFrame($"Bitmap 생성 실패: {bitmapEx.Message}");
                }

                // 8단계: OpenCV Mat 변환
                Mat img = null;
                try
                {
                    Console.WriteLine("🔄 OpenCV Mat 변환 시도...");
                    img = BitmapConverter.ToMat(screenBitmap);
                    
                    if (img == null)
                    {
                        Console.WriteLine("❌ Mat 변환 결과가 null");
                        return CreateBlackFrame("Mat 변환 결과 null");
                    }
                    
                    if (img.Empty())
                    {
                        Console.WriteLine("❌ 변환된 Mat이 비어있음");
                        img.Dispose();
                        return CreateBlackFrame("변환된 Mat 비어있음");
                    }
                    
                    Console.WriteLine($"✅ Mat 변환 성공: {img.Width}x{img.Height}, 채널={img.Channels()}");
                    
                    // 픽셀 데이터 검증
                    var scalar = img.Mean();
                    Console.WriteLine($"📊 이미지 평균값: B={scalar[0]:F1}, G={scalar[1]:F1}, R={scalar[2]:F1}");
                    
                    // 완전히 검은색인지 확인
                    if (scalar[0] < 1.0 && scalar[1] < 1.0 && scalar[2] < 1.0)
                    {
                        Console.WriteLine("⚠️ 경고: 캡처된 이미지가 거의 검은색입니다!");
                        Console.WriteLine("💡 가능한 원인:");
                        Console.WriteLine("  - 다른 창이 전체화면을 덮고 있음");
                        Console.WriteLine("  - 디스플레이 설정 문제");
                        Console.WriteLine("  - 권한 문제");
                        
                        // 그래도 반환 (완전히 검은 것이 아닐 수도 있음)
                    }
                    
                }
                catch (Exception convertEx)
                {
                    Console.WriteLine($"❌ Mat 변환 실패: {convertEx.Message}");
                    return CreateBlackFrame($"Mat 변환 실패: {convertEx.Message}");
                }

                // 9단계: 채널 변환
                Mat finalImg = img;
                try
                {
                    if (img.Channels() == 4)
                    {
                        Console.WriteLine("🔄 BGRA -> BGR 변환...");
                        finalImg = new Mat();
                        Cv2.CvtColor(img, finalImg, ColorConversionCodes.BGRA2BGR);
                        img.Dispose();
                        Console.WriteLine("✅ 채널 변환 완료");
                    }
                    else if (img.Channels() == 3)
                    {
                        Console.WriteLine("✅ 이미 BGR 형식");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ 예상치 못한 채널 수: {img.Channels()}");
                    }
                }
                catch (Exception channelEx)
                {
                    Console.WriteLine($"❌ 채널 변환 실패: {channelEx.Message}");
                    if (img != null) img.Dispose();
                    if (finalImg != null && finalImg != img) finalImg.Dispose();
                    return CreateBlackFrame($"채널 변환 실패: {channelEx.Message}");
                }

                // 10단계: 다운스케일
                if (Math.Abs(captureDownscale - 1.0) > 0.001)
                {
                    try
                    {
                        Console.WriteLine($"🔄 리사이즈: {finalImg.Width}x{finalImg.Height} -> {captureWidth}x{captureHeight}");
                        Mat resized = new Mat();
                        Cv2.Resize(finalImg, resized, new OpenCvSharp.Size(captureWidth, captureHeight), 
                            interpolation: InterpolationFlags.Linear);
                        finalImg.Dispose();
                        finalImg = resized;
                        Console.WriteLine("✅ 리사이즈 완료");
                    }
                    catch (Exception resizeEx)
                    {
                        Console.WriteLine($"❌ 리사이즈 실패: {resizeEx.Message}");
                        if (finalImg != null) finalImg.Dispose();
                        return CreateBlackFrame($"리사이즈 실패: {resizeEx.Message}");
                    }
                }

                // 11단계: 최종 검증
                if (finalImg == null || finalImg.Empty())
                {
                    Console.WriteLine("❌ 최종 이미지가 null이거나 비어있음");
                    if (finalImg != null) finalImg.Dispose();
                    return CreateBlackFrame("최종 이미지 null/비어있음");
                }

                Console.WriteLine($"✅ 화면 캡처 성공! 최종 크기: {finalImg.Width}x{finalImg.Height}");
                
                // 성공한 경우 가끔 테스트 저장
                if (frameCount % 100 == 1) // 첫 번째와 100번째마다
                {
                    try
                    {
                        string testPath = Path.Combine(Environment.CurrentDirectory, $"debug_capture_{frameCount}.jpg");
                        Cv2.ImWrite(testPath, finalImg);
                        Console.WriteLine($"💾 디버그 이미지 저장: {testPath}");
                    }
                    catch (Exception saveEx)
                    {
                        Console.WriteLine($"⚠️ 디버그 저장 실패: {saveEx.Message}");
                    }
                }

                return finalImg;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SafeCaptureScreen 전체 오류: {ex.Message}");
                Console.WriteLine($"❌ 스택 트레이스: {ex.StackTrace}");
                return CreateBlackFrame($"전체 오류: {ex.Message}");
            }
            finally
            {
                // 리소스 정리 (더 상세한 로깅)
                try
                {
                    if (screenBitmap != null)
                    {
                        screenBitmap.Dispose();
                        Console.WriteLine("🧹 Bitmap 정리됨");
                    }
                }
                catch (Exception ex) 
                { 
                    Console.WriteLine($"⚠️ Bitmap 정리 오류: {ex.Message}");
                }

                try
                {
                    if (oldBitmap != IntPtr.Zero && memoryDC != IntPtr.Zero)
                    {
                        SelectObject(memoryDC, oldBitmap);
                        Console.WriteLine("🧹 이전 비트맵 복원됨");
                    }
                }
                catch (Exception ex) 
                { 
                    Console.WriteLine($"⚠️ 비트맵 복원 오류: {ex.Message}");
                }

                try
                {
                    if (hBitmap != IntPtr.Zero)
                    {
                        DeleteObject(hBitmap);
                        Console.WriteLine("🧹 비트맵 핸들 삭제됨");
                    }
                }
                catch (Exception ex) 
                { 
                    Console.WriteLine($"⚠️ 비트맵 핸들 삭제 오류: {ex.Message}");
                }

                try
                {
                    if (memoryDC != IntPtr.Zero)
                    {
                        DeleteObject(memoryDC);
                        Console.WriteLine("🧹 메모리 DC 삭제됨");
                    }
                }
                catch (Exception ex) 
                { 
                    Console.WriteLine($"⚠️ 메모리 DC 삭제 오류: {ex.Message}");
                }

                try
                {
                    if (desktopDC != IntPtr.Zero)
                    {
                        ReleaseDC(GetDesktopWindow(), desktopDC);
                        Console.WriteLine("🧹 데스크톱 DC 해제됨");
                    }
                }
                catch (Exception ex) 
                { 
                    Console.WriteLine($"⚠️ 데스크톱 DC 해제 오류: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 검은 프레임 생성 (디버깅 정보 포함)
        /// </summary>
        private Mat CreateBlackFrame(string reason)
        {
            Console.WriteLine($"🖤 검은 프레임 생성: {reason}");
            
            try
            {
                // 완전히 검은색 대신 약간의 회색으로 (디버깅용)
                var blackFrame = new Mat(captureHeight, captureWidth, MatType.CV_8UC3, new Scalar(20, 20, 20));
                
                // 오류 메시지를 이미지에 텍스트로 추가
                try
                {
                    string shortReason = reason.Length > 50 ? reason.Substring(0, 47) + "..." : reason;
                    Cv2.PutText(blackFrame, $"Capture Error: {shortReason}", 
                        new OpenCvSharp.Point(10, 30), 
                        HersheyFonts.HersheySimplex, 0.7, 
                        new Scalar(0, 255, 255), 2); // 노란색 텍스트
                        
                    Cv2.PutText(blackFrame, $"Frame: {frameCount}", 
                        new OpenCvSharp.Point(10, 60), 
                        HersheyFonts.HersheySimplex, 0.5, 
                        new Scalar(0, 255, 0), 1); // 초록색 텍스트
                }
                catch (Exception textEx)
                {
                    Console.WriteLine($"⚠️ 텍스트 추가 실패: {textEx.Message}");
                }
                
                return blackFrame;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 검은 프레임 생성 실패: {ex.Message}");
                return new Mat(480, 640, MatType.CV_8UC3, new Scalar(0, 0, 0));
            }
        }

        /// <summary>
        /// 안전한 프레임 가져오기
        /// </summary>
        public Mat GetFrame()
        {
            if (isDisposed)
            {
                Console.WriteLine("⚠️ ScreenCapturer가 해제된 상태");
                return null;
            }

            try
            {
                lock (frameLock)
                {
                    if (currentFrame != null && !currentFrame.Empty())
                    {
                        var clonedFrame = currentFrame.Clone();
                        
                        int logInterval = GetConfigValue("log_interval", 100);
                        if (frameCount % logInterval == 0)
                        {
                            Console.WriteLine($"📸 안전한 화면 캡처: 프레임 #{frameCount}, 크기: {clonedFrame.Size()}");
                        }

                        return clonedFrame;
                    }
                    else
                    {
                        Console.WriteLine("⚠️ 사용 가능한 프레임이 없음");
                        
                        // 응급 캡처 시도
                        var emergencyFrame = SafeCaptureScreen();
                        if (emergencyFrame != null)
                        {
                            Console.WriteLine("✅ 응급 캡처 성공");
                            return emergencyFrame;
                        }
                        
                        // 최후의 수단: 기본 프레임
                        Console.WriteLine("⚠️ 기본 프레임 반환");
                        return Mat.Zeros(captureHeight, captureWidth, MatType.CV_8UC3);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ GetFrame 오류: {e.Message}");
                
                // 오류 시 기본 프레임 반환
                try
                {
                    return Mat.Zeros(captureHeight, captureWidth, MatType.CV_8UC3);
                }
                catch
                {
                    return null;
                }
            }
        }

        public void Dispose()
        {
            if (isDisposed) return;
            
            Console.WriteLine("🧹 안전한 ScreenCapturer 정리 시작");
            
            try
            {
                isDisposed = true;
                
                // 스레드 중지
                StopCaptureThread();
                
                // 프레임 정리
                lock (frameLock)
                {
                    try
                    {
                        if (currentFrame != null)
                        {
                            currentFrame.Dispose();
                            currentFrame = null;
                        }
                    }
                    catch (Exception frameEx)
                    {
                        Console.WriteLine($"⚠️ 프레임 정리 오류: {frameEx.Message}");
                    }
                    
                    try
                    {
                        if (prevFrame != null)
                        {
                            prevFrame.Dispose();
                            prevFrame = null;
                        }
                    }
                    catch (Exception prevEx)
                    {
                        Console.WriteLine($"⚠️ 이전 프레임 정리 오류: {prevEx.Message}");
                    }
                }
                
                Console.WriteLine("✅ 안전한 ScreenCapturer 정리 완료");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ScreenCapturer 정리 중 오류: {ex.Message}");
            }
        }
    }
}