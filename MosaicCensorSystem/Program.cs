using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Security.AccessControl;
using System.Security.Principal;

namespace MosaicCensorSystem
{
    internal static class Program
    {
        // ★★★ Windows 11 보안 정책 대응 강화된 ONNX 모델 경로 탐색 ★★★
        public static readonly string ONNX_MODEL_PATH = FindModelPathSecure();

        private static string FindModelPathSecure()
        {
            Console.WriteLine("=== Windows 11 보안 강화 대응 ONNX 모델 경로 탐색 시작 ===");
            
            // ★★★ 1단계: 보안 검증된 기본 경로들 ★★★
            string[] primaryPaths = {
                // 가장 안전한 경로부터 시도
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetaChip", "best.onnx"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BetaChip", "best.onnx"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "best.onnx"),
                Path.Combine(Application.StartupPath, "Resources", "best.onnx"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "best.onnx"),
                Path.Combine(Application.StartupPath, "best.onnx"),
                Path.Combine(Environment.CurrentDirectory, "Resources", "best.onnx"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BetaChip", "best.onnx")
            };

            // Assembly 경로 안전하게 추가
            string? assemblyPath = GetAssemblyLocationPathSafe();
            if (!string.IsNullOrEmpty(assemblyPath))
            {
                primaryPaths = primaryPaths.Append(assemblyPath).ToArray();
            }

            // 1단계 탐색 - 접근 권한 검증 포함
            foreach (string path in primaryPaths)
            {
                if (IsValidModelFileSecure(path)) 
                {
                    Console.WriteLine($"✅ 유효한 모델 파일 발견 (1단계): {path}");
                    return path;
                }
            }

            // ★★★ 2단계: 사용자 데이터 폴더에서 복구 시도 ★★★
            string? recoveredPath = TryRecoverFromUserData();
            if (!string.IsNullOrEmpty(recoveredPath)) return recoveredPath;

            // ★★★ 3단계: 권한 상승 없는 안전한 복사 시도 ★★★
            string? safeCopyPath = TrySafeCopyToUserSpace();
            if (!string.IsNullOrEmpty(safeCopyPath)) return safeCopyPath;

            // ★★★ 4단계: 최종 대안 경로들 ★★★
            string[] fallbackPaths = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "BetaChip", "best.onnx"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "BetaChip", "best.onnx"),
                Path.Combine(Path.GetTempPath(), "BetaChip", "best.onnx")
            };

            foreach (string path in fallbackPaths)
            {
                if (IsValidModelFileSecure(path))
                {
                    Console.WriteLine($"✅ 대안 경로에서 발견: {path}");
                    return path;
                }
            }

            // 모든 시도 실패
            Console.WriteLine("❌ 모든 경로에서 유효한 모델을 찾을 수 없음");
            ShowModelNotFoundDialog();
            
            // 기본 경로 반환 (런타임에서 다시 시도)
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "best.onnx");
        }

        private static bool IsValidModelFileSecure(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath)) return false;
                
                // 파일 존재 여부 확인
                if (!File.Exists(filePath)) return false;
                
                // 파일 접근 권한 검증
                if (!CanAccessFile(filePath)) return false;
                
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length < 1024 * 1024) // 1MB 미만이면 의심스러움
                {
                    Console.WriteLine($"⚠️ 파일이 너무 작음 ({fileInfo.Length:N0} bytes): {filePath}");
                    return false;
                }
                
                // 파일 내용 간단 검증
                using var stream = File.OpenRead(filePath);
                var buffer = new byte[16];
                int bytesRead = stream.Read(buffer, 0, 16);
                if (bytesRead >= 8)
                {
                    Console.WriteLine($"✅ 유효한 모델 파일: {fileInfo.Length:N0} bytes - {filePath}");
                    return true;
                }
                
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"❌ 파일 접근 권한 없음: {filePath} - {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 파일 검증 실패: {filePath} - {ex.Message}");
                return false;
            }
        }

        private static bool CanAccessFile(string filePath)
        {
            try
            {
                // 파일 읽기 권한 테스트
                using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return fs.CanRead;
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"⚠️ 파일 읽기 권한 없음: {filePath}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 파일 접근 테스트 실패: {filePath} - {ex.Message}");
                return false;
            }
        }

        private static string? TryRecoverFromUserData()
        {
            try
            {
                // 사용자 데이터 폴더들에서 백업 찾기
                string[] userDataPaths = {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetaChip", "best.onnx"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BetaChip", "best.onnx"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BetaChip", "best.onnx")
                };

                foreach (string backupPath in userDataPaths)
                {
                    if (IsValidModelFileSecure(backupPath))
                    {
                        // 메인 경로로 안전하게 복사 시도
                        string mainPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "best.onnx");
                        if (TrySafeCopy(backupPath, mainPath))
                        {
                            Console.WriteLine($"🔄 사용자 데이터에서 모델 복구 성공: {backupPath} → {mainPath}");
                            return mainPath;
                        }
                        else
                        {
                            // 복사 실패시 원본 경로 그대로 사용
                            Console.WriteLine($"🔄 사용자 데이터에서 모델 발견: {backupPath} (원본 경로 사용)");
                            return backupPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 사용자 데이터 복구 실패: {ex.Message}");
            }
            return null;
        }

        private static string? TrySafeCopyToUserSpace()
        {
            try
            {
                // 프로그램 설치 폴더에서 사용자 공간으로 복사 시도
                string[] sourcePaths = {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "best.onnx"),
                    Path.Combine(Application.StartupPath, "Resources", "best.onnx"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "best.onnx")
                };

                string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BetaChip");
                string targetPath = Path.Combine(targetDir, "best.onnx");

                foreach (string sourcePath in sourcePaths)
                {
                    if (File.Exists(sourcePath))
                    {
                        if (TrySafeCopy(sourcePath, targetPath))
                        {
                            Console.WriteLine($"🔄 사용자 공간으로 안전 복사 성공: {sourcePath} → {targetPath}");
                            return targetPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 사용자 공간 복사 실패: {ex.Message}");
            }
            return null;
        }

        private static bool TrySafeCopy(string sourcePath, string targetPath)
        {
            try
            {
                string? targetDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                File.Copy(sourcePath, targetPath, true);
                return IsValidModelFileSecure(targetPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 파일 복사 실패: {sourcePath} → {targetPath} - {ex.Message}");
                return false;
            }
        }

        private static string? GetAssemblyLocationPathSafe()
        {
            try
            {
                string? location = Assembly.GetExecutingAssembly().Location;
                
                if (string.IsNullOrEmpty(location))
                {
                    location = Environment.ProcessPath;
                }
                
                if (!string.IsNullOrEmpty(location))
                {
                    string? dir = Path.GetDirectoryName(location);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        return Path.Combine(dir, "Resources", "best.onnx");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Assembly 위치 확인 실패: {ex.Message}");
            }
            return null;
        }

        private static void ShowModelNotFoundDialog()
        {
            string message = "ONNX 모델 파일을 찾을 수 없습니다.\n\n" +
                           "Windows 11의 보안 정책으로 인해 파일 접근이 제한될 수 있습니다.\n\n" +
                           "해결 방법:\n" +
                           "1. 관리자 권한으로 프로그램 실행\n" +
                           "2. Windows 보안 > 바이러스 및 위협 방지에서 BetaChip을 예외 추가\n" +
                           "3. 프로그램 재설치 (관리자 권한으로)\n" +
                           "4. 바탕화면에 best.onnx 파일을 복사 후 프로그램 재실행\n\n" +
                           "그래도 문제가 지속되면 지원팀에 문의하세요.";
            
            MessageBox.Show(message, "모델 파일 접근 오류 - Windows 11 보안", 
                          MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                string errorLog = $"{DateTime.Now}: {ex?.ToString()}";
                
                try
                {
                    string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                                                 "BetaChip", "fatal_error.log");
                    string? logDir = Path.GetDirectoryName(logPath);
                    if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }
                    File.WriteAllText(logPath, errorLog);
                }
                catch 
                { 
                    // 로그 저장 실패해도 계속 진행
                }
                
                MessageBox.Show("치명적인 오류가 발생했습니다. 프로그램을 종료합니다.", "오류", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                Console.WriteLine($"최종 ONNX 모델 경로: {ONNX_MODEL_PATH}");
                
                if (!IsValidModelFileSecure(ONNX_MODEL_PATH))
                {
                    string message = "⚠️ ONNX 모델 파일에 접근할 수 없습니다.\n\n" +
                                   "Windows 11 보안 정책으로 인한 제한일 수 있습니다.\n" +
                                   "프로그램은 시작되지만 일부 기능이 제한됩니다.\n\n" +
                                   "완전한 기능을 위해서는 위의 해결 방법을 참고하세요.";
                    
                    MessageBox.Show(message, "모델 파일 제한적 접근", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Console.WriteLine("⚠️ 모델 파일 없이 프로그램을 시작합니다.");
                }
                else
                {
                    var fileInfo = new FileInfo(ONNX_MODEL_PATH);
                    Console.WriteLine($"✅ 모델 파일 접근 성공: {fileInfo.Length:N0} bytes");
                }
                
                var app = new MosaicApp();
                app.Run();
            }
            catch (Exception ex)
            {
                string errorMessage = $"프로그램 초기화 중 오류가 발생했습니다:\n\n{ex.Message}";
                MessageBox.Show(errorMessage, "초기화 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                
                try
                {
                    string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                                                 "BetaChip", "init_error.log");
                    string? logDir = Path.GetDirectoryName(logPath);
                    if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }
                    File.WriteAllText(logPath, $"{DateTime.Now}: {ex}");
                }
                catch { }
            }
        }
    }
}