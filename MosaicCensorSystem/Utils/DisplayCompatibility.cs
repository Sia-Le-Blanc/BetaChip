using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MosaicCensorSystem.Utils
{
    /// <summary>
    /// 모든 디스플레이 환경에서 호환성을 보장하는 헬퍼 클래스
    /// </summary>
    public static class DisplayCompatibility
    {
        #region Win32 API
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
        
        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        
        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
        
        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int awareness);
        
        private const int LOGPIXELSX = 88;
        private const int LOGPIXELSY = 90;
        
        // DPI Awareness Contexts (Windows 10+)
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_UNAWARE = new IntPtr(-1);
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = new IntPtr(-2);
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE = new IntPtr(-3);
        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);
        
        // Process DPI Awareness (Windows 8.1+)
        private const int PROCESS_DPI_UNAWARE = 0;
        private const int PROCESS_SYSTEM_DPI_AWARE = 1;
        private const int PROCESS_PER_MONITOR_DPI_AWARE = 2;
        #endregion

        private static DisplaySettings currentSettings = null;
        private static readonly object lockObject = new object();

        /// <summary>
        /// 현재 디스플레이 설정
        /// </summary>
        public class DisplaySettings
        {
            public float SystemDpiScale { get; set; }
            public int PrimaryScreenDpi { get; set; }
            public bool IsHighDpi { get; set; }
            public string WindowsVersion { get; set; }
            public bool IsMultiMonitor { get; set; }
            public Rectangle VirtualScreenBounds { get; set; }
            public DpiMode RecommendedMode { get; set; }
            public bool HasDpiIssues { get; set; }
        }

        public enum DpiMode
        {
            Unaware,        // DPI 인식 안함 (레거시 모드)
            SystemAware,    // 시스템 DPI만 인식
            PerMonitor,     // 모니터별 DPI 인식
            Auto           // 자동 감지
        }

        /// <summary>
        /// 프로그램 시작 시 호출하여 최적의 DPI 모드 설정
        /// </summary>
        public static DisplaySettings Initialize()
        {
            lock (lockObject)
            {
                if (currentSettings != null)
                    return currentSettings;

                currentSettings = DetectDisplayEnvironment();
                ApplyOptimalDpiMode(currentSettings);
                LogEnvironment(currentSettings);
                
                return currentSettings;
            }
        }

        /// <summary>
        /// 현재 디스플레이 환경 감지
        /// </summary>
        private static DisplaySettings DetectDisplayEnvironment()
        {
            var settings = new DisplaySettings();
            
            // Windows 버전 확인
            settings.WindowsVersion = Environment.OSVersion.Version.ToString();
            var winVersion = Environment.OSVersion.Version;
            
            // 기본 DPI 가져오기
            IntPtr desktopDc = GetDC(IntPtr.Zero);
            settings.PrimaryScreenDpi = GetDeviceCaps(desktopDc, LOGPIXELSX);
            ReleaseDC(IntPtr.Zero, desktopDc);
            
            // DPI 스케일 계산
            settings.SystemDpiScale = settings.PrimaryScreenDpi / 96.0f;
            settings.IsHighDpi = settings.PrimaryScreenDpi > 96;
            
            // 멀티모니터 확인
            settings.IsMultiMonitor = Screen.AllScreens.Length > 1;
            
            // 가상 스크린 범위
            settings.VirtualScreenBounds = SystemInformation.VirtualScreen;
            
            // 문제가 있을 수 있는 환경 감지
            settings.HasDpiIssues = DetectPotentialIssues(settings);
            
            // 권장 DPI 모드 결정
            settings.RecommendedMode = DetermineOptimalMode(settings, winVersion);
            
            return settings;
        }

        /// <summary>
        /// 잠재적 문제 감지
        /// </summary>
        private static bool DetectPotentialIssues(DisplaySettings settings)
        {
            // 문제가 될 수 있는 조건들
            if (settings.SystemDpiScale > 1.5f) // 150% 이상 스케일링
                return true;
                
            if (settings.IsMultiMonitor && settings.IsHighDpi) // 멀티모니터 + 높은 DPI
                return true;
                
            if (settings.VirtualScreenBounds.Width > 3840) // 4K 이상 해상도
                return true;
                
            // 레지스트리에서 사용자 설정 확인
            if (HasCustomDpiSettings())
                return true;
                
            return false;
        }

        /// <summary>
        /// 사용자가 커스텀 DPI 설정을 사용하는지 확인
        /// </summary>
        private static bool HasCustomDpiSettings()
        {
            RegistryKey? key = null;
            try
            {
                key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
                if (key == null) return false;
                
                var logPixels = key.GetValue("LogPixels");
                var win8DpiScaling = key.GetValue("Win8DpiScaling");
                
                return logPixels != null || win8DpiScaling != null;
            }
            catch 
            { 
                return false; 
            }
            finally
            {
                try
                {
                    key?.Close();
                }
                catch { }
            }
        }

        /// <summary>
        /// 최적의 DPI 모드 결정
        /// </summary>
        private static DpiMode DetermineOptimalMode(DisplaySettings settings, Version winVersion)
        {
            // 문제가 감지된 경우 DPI 인식 비활성화
            if (settings.HasDpiIssues)
            {
                Console.WriteLine("[DisplayCompat] DPI 관련 문제 감지됨 - Unaware 모드 사용");
                return DpiMode.Unaware;
            }
            
            // Windows 10 버전 1703 이상 (빌드 15063)
            if (winVersion.Major > 10 || (winVersion.Major == 10 && winVersion.Build >= 15063))
            {
                if (settings.IsMultiMonitor && settings.IsHighDpi)
                    return DpiMode.PerMonitor;
                else
                    return DpiMode.SystemAware;
            }
            
            // Windows 8.1 / Windows 10 초기 버전
            if (winVersion.Major > 6 || (winVersion.Major == 6 && winVersion.Minor >= 3))
            {
                return DpiMode.SystemAware;
            }
            
            // Windows 7 이하
            return DpiMode.Unaware;
        }

        /// <summary>
        /// 선택된 DPI 모드 적용
        /// </summary>
        private static void ApplyOptimalDpiMode(DisplaySettings settings)
        {
            try
            {
                switch (settings.RecommendedMode)
                {
                    case DpiMode.Unaware:
                        ApplyDpiUnaware();
                        break;
                        
                    case DpiMode.SystemAware:
                        ApplySystemDpiAware();
                        break;
                        
                    case DpiMode.PerMonitor:
                        ApplyPerMonitorDpiAware();
                        break;
                        
                    case DpiMode.Auto:
                    default:
                        // 자동으로 최적 설정 시도
                        if (!TryApplyPerMonitorDpiAware())
                            if (!TryApplySystemDpiAware())
                                ApplyDpiUnaware();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DisplayCompat] DPI 모드 적용 실패: {ex.Message}");
                // 실패 시 가장 안전한 모드로 폴백
                try { ApplyDpiUnaware(); } catch { }
            }
        }

        private static void ApplyDpiUnaware()
        {
            // 가장 안전한 모드 - DPI 스케일링 완전 비활성화
            // 아무것도 하지 않음 (기본값이 Unaware)
            Console.WriteLine("[DisplayCompat] DPI Unaware 모드 적용됨");
        }

        private static bool TryApplySystemDpiAware()
        {
            try
            {
                SetProcessDPIAware();
                Console.WriteLine("[DisplayCompat] System DPI Aware 모드 적용됨");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ApplySystemDpiAware()
        {
            try
            {
                // Windows 10
                SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_SYSTEM_AWARE);
            }
            catch
            {
                try
                {
                    // Windows 8.1
                    SetProcessDpiAwareness(PROCESS_SYSTEM_DPI_AWARE);
                }
                catch
                {
                    // Windows 7
                    SetProcessDPIAware();
                }
            }
            Console.WriteLine("[DisplayCompat] System DPI Aware 모드 적용됨");
        }

        private static bool TryApplyPerMonitorDpiAware()
        {
            try
            {
                ApplyPerMonitorDpiAware();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ApplyPerMonitorDpiAware()
        {
            try
            {
                // Windows 10 v2
                SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            }
            catch
            {
                try
                {
                    // Windows 10 v1
                    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE);
                }
                catch
                {
                    // Windows 8.1
                    SetProcessDpiAwareness(PROCESS_PER_MONITOR_DPI_AWARE);
                }
            }
            Console.WriteLine("[DisplayCompat] Per-Monitor DPI Aware 모드 적용됨");
        }

        /// <summary>
        /// 환경 정보 로깅
        /// </summary>
        private static void LogEnvironment(DisplaySettings settings)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("    Display Compatibility Report");
            Console.WriteLine("========================================");
            Console.WriteLine($"Windows Version: {settings.WindowsVersion}");
            Console.WriteLine($"Primary DPI: {settings.PrimaryScreenDpi} ({settings.SystemDpiScale:P0} scale)");
            Console.WriteLine($"High DPI: {settings.IsHighDpi}");
            Console.WriteLine($"Multi-Monitor: {settings.IsMultiMonitor}");
            Console.WriteLine($"Virtual Screen: {settings.VirtualScreenBounds}");
            Console.WriteLine($"Potential Issues: {settings.HasDpiIssues}");
            Console.WriteLine($"Applied Mode: {settings.RecommendedMode}");
            Console.WriteLine("========================================");
        }

        /// <summary>
        /// 스케일링을 고려한 실제 화면 좌표 계산
        /// </summary>
        public static Rectangle GetScaledBounds(Rectangle originalBounds)
        {
            if (currentSettings == null || currentSettings.RecommendedMode == DpiMode.Unaware)
                return originalBounds;
                
            // DPI를 인식하는 모드에서는 원본 좌표 사용
            return originalBounds;
        }

        /// <summary>
        /// 오버레이용 안전한 좌표 계산
        /// </summary>
        public static Rectangle GetSafeOverlayBounds(Rectangle monitorBounds)
        {
            // 문제가 있는 환경에서는 약간의 여백 추가
            if (currentSettings?.HasDpiIssues == true)
            {
                return new Rectangle(
                    monitorBounds.X + 1,
                    monitorBounds.Y + 1,
                    monitorBounds.Width - 2,
                    monitorBounds.Height - 2
                );
            }
            
            return monitorBounds;
        }

        /// <summary>
        /// 현재 설정 가져오기
        /// </summary>
        public static DisplaySettings GetCurrentSettings()
        {
            if (currentSettings == null)
                Initialize();
            return currentSettings;
        }

        /// <summary>
        /// 사용자가 강제로 호환 모드를 설정할 수 있는 메서드
        /// </summary>
        public static void ForceCompatibilityMode()
        {
            Console.WriteLine("[DisplayCompat] 강제 호환 모드 활성화");
            ApplyDpiUnaware();
            if (currentSettings != null)
            {
                currentSettings.RecommendedMode = DpiMode.Unaware;
                currentSettings.HasDpiIssues = true;
            }
        }
    }
}