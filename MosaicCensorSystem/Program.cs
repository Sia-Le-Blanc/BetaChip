#pragma warning disable CA1416
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
            // 1. 명령줄 인수 처리
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

                bool compatEnabled = UserSettings.IsCompatibilityModeEnabled() || forceCompatMode;
                if (compatEnabled)
                {
                    Console.WriteLine("자동 호환성 모드가 활성화되었습니다. DPI 배율이 해제됩니다.");
                    DisplayCompatibility.ForceCompatibilityMode();
                }
                else
                {
                    Console.WriteLine("호환성 모드가 비활성화되어 있습니다. DPI 설정을 그대로 사용합니다.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"디스플레이 호환성 초기화 실패: {ex.Message}");
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

            // 6. GPU 환경 감지 및 첫 실행 안내
            try
            {
                var gpuResult = Helpers.GpuDetector.Detect();
                bool isFirstRun = !File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gpu_checked.flag"));
                
                // 첫 실행이고 CUDA를 사용할 수 없는 경우 안내 표시
                if (isFirstRun && !gpuResult.CanUseCuda)
                {
                    using var gpuForm = new UI.GpuSetupForm(gpuResult);
                    gpuForm.ShowDialog();
                    
                    // 플래그 파일 생성
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gpu_checked.flag"), DateTime.Now.ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GPU 감지 중 오류: {ex.Message}");
            }

            // 7. 메인 애플리케이션 실행
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

        private static void LogError(string type, Exception? ex)
        {
            try
            {
                string logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BetaChip",
                    "error.log"
                );

                string? logDir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

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

            if (ex.Message.Contains("display") || ex.Message.Contains("monitor") ||
                ex.Message.Contains("screen") || ex.Message.Contains("DPI"))
            {
                errorDetails += "이 오류는 디스플레이 설정과 관련이 있을 수 있습니다.\n" +
                                "다음 방법을 시도해보세요:\n" +
                                "1. Windows 디스플레이 설정에서 배율을 100%로 변경\n" +
                                "2. 프로그램을 관리자 권한으로 실행\n\n";
            }

            errorDetails += "자세한 오류 정보는 다음 위치의 로그 파일을 확인하세요:\n" +
                             Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                        "BetaChip", "error.log");

            MessageBox.Show(errorDetails, "초기화 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static string? GetModelPath()
        {
            const string modelFileName = "best.onnx";

            // 1. 레지스트리에서 찾기 (최우선)
            string? registryPath = GetPathFromRegistry("ModelPath");
            if (!string.IsNullOrEmpty(registryPath))
            {
                // 레지스트리 값 정규화
                registryPath = NormalizePath(registryPath);
                
                // 파일 경로인 경우
                if (!string.IsNullOrEmpty(registryPath) && File.Exists(registryPath))
                {
                    Console.WriteLine($"✅ 레지스트리에서 모델 발견: {registryPath}");
                    return registryPath;
                }

                // 폴더 경로인 경우
                if (!string.IsNullOrEmpty(registryPath) && Directory.Exists(registryPath))
                {
                    string modelInFolder = Path.Combine(registryPath, modelFileName);
                    if (File.Exists(modelInFolder))
                    {
                        Console.WriteLine($"✅ 레지스트리 폴더에서 모델 발견: {modelInFolder}");
                        return modelInFolder;
                    }
                }
                
                Console.WriteLine($"⚠️ 레지스트리 경로가 유효하지 않음: {registryPath}");
            }

            // 2. 레지스트리 폴백 경로들 시도
            string? resourcesPath = GetPathFromRegistry("ResourcesPath");
            if (!string.IsNullOrEmpty(resourcesPath))
            {
                resourcesPath = NormalizePath(resourcesPath);
                if (!string.IsNullOrEmpty(resourcesPath))
                {
                    string modelInResources = Path.Combine(resourcesPath, modelFileName);
                    if (File.Exists(modelInResources))
                    {
                        Console.WriteLine($"✅ 레지스트리 Resources 경로에서 모델 발견: {modelInResources}");
                        return modelInResources;
                    }
                }
            }

            string? installPath = GetPathFromRegistry("InstallPath");
            if (!string.IsNullOrEmpty(installPath))
            {
                installPath = NormalizePath(installPath);
                if (!string.IsNullOrEmpty(installPath))
                {
                    string modelInInstall = Path.Combine(installPath, "Resources", modelFileName);
                    if (File.Exists(modelInInstall))
                    {
                        Console.WriteLine($"✅ 레지스트리 설치 경로에서 모델 발견: {modelInInstall}");
                        return modelInInstall;
                    }
                }
            }

            // 3. 실행 파일 기준 상대 경로 (개발 환경 및 수동 설치)
            string? baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir))
            {
                // 3-1. Resources 폴더
                string localPath = Path.Combine(baseDir, "Resources", modelFileName);
                if (File.Exists(localPath))
                {
                    Console.WriteLine($"✅ 로컬 Resources 폴더에서 모델 발견: {localPath}");
                    return localPath;
                }

                // 3-2. 실행 폴더에 직접
                string directPath = Path.Combine(baseDir, modelFileName);
                if (File.Exists(directPath))
                {
                    Console.WriteLine($"✅ 실행 폴더에서 모델 발견: {directPath}");
                    return directPath;
                }

                // 3-3. 상위 폴더의 Resources (개발 환경)
                string parentPath = Path.Combine(baseDir, "..", "Resources", modelFileName);
                try
                {
                    parentPath = Path.GetFullPath(parentPath);
                    if (File.Exists(parentPath))
                    {
                        Console.WriteLine($"✅ 상위 폴더에서 모델 발견: {parentPath}");
                        return parentPath;
                    }
                }
                catch { }
            }

            // 4. 사용자 문서 폴더 확인 (일부 사용자가 여기에 설치할 수 있음)
            try
            {
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrEmpty(documentsPath))
                {
                    string docModel = Path.Combine(documentsPath, "BetaChip", "Resources", modelFileName);
                    if (File.Exists(docModel))
                    {
                        Console.WriteLine($"✅ 문서 폴더에서 모델 발견: {docModel}");
                        return docModel;
                    }
                }
            }
            catch { }

            // 5. AppData 로컬 확인
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(localAppData))
                {
                    string appDataModel = Path.Combine(localAppData, "BetaChip", "Resources", modelFileName);
                    if (File.Exists(appDataModel))
                    {
                        Console.WriteLine($"✅ AppData에서 모델 발견: {appDataModel}");
                        return appDataModel;
                    }
                }
            }
            catch { }

            // 모든 시도 실패 - 상세 로그 출력
            Console.WriteLine("❌ 모델 파일을 찾을 수 없습니다. 시도한 모든 경로:");
            Console.WriteLine($"  1. 레지스트리 ModelPath: {registryPath ?? "(없음)"}");
            Console.WriteLine($"  2. 레지스트리 ResourcesPath: {resourcesPath ?? "(없음)"}");
            Console.WriteLine($"  3. 레지스트리 InstallPath: {installPath ?? "(없음)"}");
            Console.WriteLine($"  4. 실행 폴더 Resources: {(baseDir != null ? Path.Combine(baseDir, "Resources", modelFileName) : "(baseDir null)")}");
            Console.WriteLine($"  5. 실행 폴더 직접: {(baseDir != null ? Path.Combine(baseDir, modelFileName) : "(baseDir null)")}");
            
            return null;
        }

        private static string? GetPathFromRegistry(string valueName)
        {
            const string registryKeyPath = @"SOFTWARE\BetaChip\MosaicCensorSystem";

            // 64비트와 32비트 레지스트리 모두 확인
            RegistryView[] viewsToProbe = { RegistryView.Registry64, RegistryView.Registry32 };

            foreach (RegistryView view in viewsToProbe)
            {
                RegistryKey? baseKey = null;
                RegistryKey? key = null;
                
                try
                {
                    baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    key = baseKey?.OpenSubKey(registryKeyPath);

                    if (key == null) continue;

                    object? value = key.GetValue(valueName);
                    if (value is string rawPath && !string.IsNullOrWhiteSpace(rawPath))
                    {
                        return rawPath;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ 레지스트리 읽기 실패 ({view}, {valueName}): {ex.Message}");
                }
                finally
                {
                    try
                    {
                        key?.Close();
                        baseKey?.Close();
                    }
                    catch { }
                }
            }

            return null;
        }

        private static string NormalizePath(string? rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
                return string.Empty;

            try
            {
                // 1. 앞뒤 공백 제거
                string candidate = rawPath.Trim();

                // 2. 따옴표 제거
                if (candidate.StartsWith('"') && candidate.EndsWith('"') && candidate.Length >= 2)
                    candidate = candidate.Substring(1, candidate.Length - 2);

                // 3. 환경 변수 확장
                string expanded = Environment.ExpandEnvironmentVariables(candidate);
                
                // 4. 상대 경로를 절대 경로로 변환 시도
                if (!Path.IsPathRooted(expanded))
                {
                    string? baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    if (!string.IsNullOrEmpty(baseDir))
                    {
                        expanded = Path.Combine(baseDir, expanded);
                    }
                }

                // 5. 경로 정규화
                expanded = Path.GetFullPath(expanded);

                // 6. 경로가 실제로 존재하는지 확인
                if (File.Exists(expanded) || Directory.Exists(expanded))
                    return expanded;

                Console.WriteLine($"⚠️ 정규화된 경로가 존재하지 않음: {expanded}");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 경로 정규화 실패: {rawPath} - {ex.Message}");
                return string.Empty;
            }
        }
    }
}