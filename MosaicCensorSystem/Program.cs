using Microsoft.Win32;
using MosaicCensorSystem.Utils;
using System;
using System.IO;
using System.Windows.Forms;

namespace MosaicCensorSystem
{
    internal static class Program
    {
        public static readonly string? ONNX_MODEL_PATH = GetModelPath();
        
        [STAThread]
        static void Main(string[] args)
        {
            // 1. 명령줄 인수 처리 (강제 호환 모드)
            bool forceCompatMode = false;
            foreach (var arg in args)
            {
                if (arg.ToLower() == "--compat" || arg.ToLower() == "/compat")
                {
                    forceCompatMode = true;
                    break;
                }
            }

            // 2. 디스플레이 호환성 초기화 (DPI 설정보다 먼저!)
            try
            {
                var displaySettings = DisplayCompatibility.Initialize();
                
                // 강제 호환 모드 적용
                if (forceCompatMode)
                {
                    Console.WriteLine("강제 호환 모드가 활성화되었습니다.");
                    DisplayCompatibility.ForceCompatibilityMode();
                }
                
                // 문제가 감지된 경우 사용자에게 알림
                if (displaySettings.HasDpiIssues && !forceCompatMode)
                {
                    var result = MessageBox.Show(
                        "디스플레이 설정에 잠재적인 호환성 문제가 감지되었습니다.\n\n" +
                        $"• DPI 스케일: {displaySettings.SystemDpiScale:P0}\n" +
                        $"• 멀티모니터: {(displaySettings.IsMultiMonitor ? "예" : "아니오")}\n" +
                        $"• 고DPI: {(displaySettings.IsHighDpi ? "예" : "아니오")}\n\n" +
                        "호환성 모드로 실행하시겠습니까?\n" +
                        "(화면 확대/축소 문제가 있다면 '예'를 선택하세요)",
                        "디스플레이 호환성 확인",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);
                    
                    if (result == DialogResult.Yes)
                    {
                        DisplayCompatibility.ForceCompatibilityMode();
                    }
                    else if (result == DialogResult.Cancel)
                    {
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"디스플레이 호환성 초기화 실패: {ex.Message}");
                // 실패해도 계속 진행 (기본 설정 사용)
            }

            // 3. Windows Forms 초기화
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // 4. 예외 처리기 설정
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            
            // 5. 모델 파일 확인
            if (string.IsNullOrEmpty(ONNX_MODEL_PATH))
            {
                ShowModelNotFoundError();
                return;
            }

            // 6. 메인 애플리케이션 실행
            try
            {
                var app = new MosaicApp();
                app.Run();
            }
            catch (Exception ex)
            {
                ShowCriticalError(ex);
            }
        }

        private static void OnThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            LogError("UI Thread Exception", e.Exception);
            
            var result = MessageBox.Show(
                $"프로그램 실행 중 오류가 발생했습니다.\n\n" +
                $"오류: {e.Exception.Message}\n\n" +
                "프로그램을 계속 실행하시겠습니까?",
                "오류 발생",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Error);
                
            if (result == DialogResult.No)
            {
                Application.Exit();
            }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            LogError("Unhandled Exception", ex);
            
            MessageBox.Show(
                $"치명적인 오류가 발생했습니다.\n\n" +
                $"오류: {ex?.Message ?? "알 수 없는 오류"}\n\n" +
                "프로그램을 종료합니다.",
                "치명적 오류",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static void LogError(string type, Exception ex)
        {
            try
            {
                string logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BetaChip",
                    "error.log"
                );
                
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {type}: {ex?.ToString() ?? "No exception details"}\n\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // 로깅 실패는 무시
            }
        }

        private static void ShowModelNotFoundError()
        {
            MessageBox.Show(
                "핵심 AI 모델 파일(best.onnx)을 찾을 수 없습니다.\n\n" +
                "다음을 확인해주세요:\n" +
                "1. 프로그램이 올바르게 설치되었는지\n" +
                "2. Resources 폴더에 best.onnx 파일이 있는지\n" +
                "3. 바이러스 백신이 파일을 차단하지 않았는지\n\n" +
                "문제가 지속되면 프로그램을 재설치해주세요.",
                "모델 파일 없음",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static void ShowCriticalError(Exception ex)
        {
            string errorDetails = $"프로그램 초기화 중 심각한 오류가 발생했습니다.\n\n" +
                                $"오류: {ex.Message}\n\n";

            // 디스플레이 관련 오류인지 확인
            if (ex.Message.Contains("display") || ex.Message.Contains("monitor") || 
                ex.Message.Contains("screen") || ex.Message.Contains("DPI"))
            {
                errorDetails += "이 오류는 디스플레이 설정과 관련이 있을 수 있습니다.\n" +
                              "다음 방법을 시도해보세요:\n" +
                              "1. 프로그램을 호환성 모드로 실행 (--compat 옵션)\n" +
                              "2. Windows 디스플레이 설정에서 배율을 100%로 변경\n" +
                              "3. 프로그램을 관리자 권한으로 실행\n\n";
            }

            errorDetails += "자세한 오류 정보는 다음 위치의 로그 파일을 확인하세요:\n" +
                          Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                                     "BetaChip", "error.log");

            MessageBox.Show(errorDetails, "초기화 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static string? GetModelPath()
        {
            const string modelFileName = "best.onnx";

            // 1. 레지스트리에서 찾기
            string? registryPath = GetPathFromRegistry("ModelPath");
            if (!string.IsNullOrEmpty(registryPath))
            {
                if (File.Exists(registryPath))
                {
                    Console.WriteLine($"✅ 레지스트리에서 모델 발견: {registryPath}");
                    return registryPath;
                }
                
                if (Directory.Exists(registryPath))
                {
                    string modelInFolder = Path.Combine(registryPath, modelFileName);
                    if (File.Exists(modelInFolder))
                    {
                        Console.WriteLine($"✅ 레지스트리 폴더에서 모델 발견: {modelInFolder}");
                        return modelInFolder;
                    }
                }
            }

            // 2. 실행 파일 기준 상대 경로
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", modelFileName);
            if (File.Exists(localPath))
            {
                Console.WriteLine($"✅ 로컬 경로에서 모델 발견: {localPath}");
                return localPath;
            }

            // 3. 실행 폴더에 직접 있는 경우
            string directPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelFileName);
            if (File.Exists(directPath))
            {
                Console.WriteLine($"✅ 실행 폴더에서 모델 발견: {directPath}");
                return directPath;
            }

            Console.WriteLine("❌ 모델 파일을 찾을 수 없습니다.");
            return null;
        }

        private static string? GetPathFromRegistry(string valueName)
        {
            const string registryKeyPath = @"SOFTWARE\BetaChip\MosaicCensorSystem";

            RegistryView[] viewsToProbe = { RegistryView.Registry64, RegistryView.Registry32 };

            foreach (RegistryView view in viewsToProbe)
            {
                try
                {
                    using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    using RegistryKey? key = baseKey.OpenSubKey(registryKeyPath);

                    if (key?.GetValue(valueName) is string rawPath && !string.IsNullOrWhiteSpace(rawPath))
                    {
                        string normalized = NormalizePath(rawPath);
                        if (!string.IsNullOrEmpty(normalized))
                        {
                            return normalized;
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        private static string NormalizePath(string rawPath)
        {
            string candidate = rawPath.Trim();

            if (candidate.Length == 0)
                return string.Empty;

            if (candidate.StartsWith('"') && candidate.EndsWith('"') && candidate.Length >= 2)
                candidate = candidate[1..^1];

            candidate = Environment.ExpandEnvironmentVariables(candidate);

            if (File.Exists(candidate) || Directory.Exists(candidate))
                return candidate;

            return string.Empty;
        }
    }
}