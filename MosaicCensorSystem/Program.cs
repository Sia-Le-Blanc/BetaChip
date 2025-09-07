using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MosaicCensorSystem
{
    internal static class Program
    {
        // ★★★ 개선된 ONNX 모델 경로 탐색 로직 ★★★
        public static readonly string ONNX_MODEL_PATH = FindModelPath();

        private static string FindModelPath()
        {
            Console.WriteLine("=== 강화된 ONNX 모델 경로 탐색 시작 ===");
            
            // ★★★ 1단계: 기본 경로들 ★★★
            string[] primaryPaths = {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "best.onnx"),
                Path.Combine(Application.StartupPath, "Resources", "best.onnx"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "best.onnx"),
                Path.Combine(Application.StartupPath, "best.onnx"),
                Path.Combine(Environment.CurrentDirectory, "Resources", "best.onnx"),
                Path.Combine(Environment.CurrentDirectory, "best.onnx"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MosaicCensorSystem", "best.onnx"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MosaicCensorSystem", "best.onnx"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MosaicCensorSystem", "best.onnx")
            };

            // Assembly 경로 추가
            string? assemblyPath = GetAssemblyLocationPath();
            if (!string.IsNullOrEmpty(assemblyPath))
            {
                primaryPaths = primaryPaths.Append(assemblyPath).ToArray();
            }

            // 1단계 탐색
            foreach (string path in primaryPaths)
            {
                if (IsValidModelFile(path)) return path;
            }

            // ★★★ 2단계: 레지스트리에서 설치 경로 찾기 ★★★
            string? registryPath = TryGetInstallPathFromRegistry();
            if (!string.IsNullOrEmpty(registryPath))
            {
                string[] registryPaths = {
                    Path.Combine(registryPath, "Resources", "best.onnx"),
                    Path.Combine(registryPath, "best.onnx")
                };
                
                foreach (string path in registryPaths)
                {
                    if (IsValidModelFile(path)) return path;
                }
            }

            // ★★★ 3단계: 백업에서 복구 시도 ★★★
            string? recoveredPath = TryRecoverFromBackup();
            if (!string.IsNullOrEmpty(recoveredPath)) return recoveredPath;

            // ★★★ 4단계: 주요 디렉토리 검색 ★★★
            string? foundPath = TryLimitedDriveSearch();
            if (!string.IsNullOrEmpty(foundPath)) return foundPath;

            // 모든 시도 실패
            DiagnoseEnvironment();
            Console.WriteLine("❌ 모든 경로에서 유효한 모델을 찾을 수 없음");
            
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "best.onnx");
        }

        private static bool IsValidModelFile(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;
                
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length < 1024 * 1024)
                {
                    Console.WriteLine($"⚠️ 파일이 너무 작음 ({fileInfo.Length:N0} bytes): {filePath}");
                    return false;
                }
                
                using var stream = File.OpenRead(filePath);
                var buffer = new byte[8];
                int bytesRead = stream.Read(buffer, 0, 8);
                if (bytesRead >= 8)
                {
                    Console.WriteLine($"✅ 유효한 모델 파일 발견: {fileInfo.Length:N0} bytes - {filePath}");
                    return true;
                }
                
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"❌ 파일 접근 권한 없음: {filePath}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 파일 검증 실패: {filePath} - {ex.Message}");
                return false;
            }
        }

        private static string? TryGetInstallPathFromRegistry()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (key == null) return null;

                foreach (string subKeyName in key.GetSubKeyNames())
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    string? displayName = subKey.GetValue("DisplayName") as string;
                    if (string.IsNullOrEmpty(displayName)) continue;

                    if (displayName.Contains("MosaicCensorSystem") || 
                        displayName.Contains("BetaChip") ||
                        displayName.Contains("Mosaic Censor"))
                    {
                        string? installLocation = subKey.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                        {
                            Console.WriteLine($"📍 레지스트리에서 설치 경로 발견: {installLocation}");
                            return installLocation;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 레지스트리 검색 실패: {ex.Message}");
            }
            return null;
        }

        private static string? TryRecoverFromBackup()
        {
            try
            {
                string userBackupPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                    "MosaicCensorSystem", "best.onnx");
                    
                if (IsValidModelFile(userBackupPath))
                {
                    string mainPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "best.onnx");
                    string? mainDir = Path.GetDirectoryName(mainPath);
                    
                    if (!string.IsNullOrEmpty(mainDir) && !Directory.Exists(mainDir))
                    {
                        Directory.CreateDirectory(mainDir);
                    }
                    
                    File.Copy(userBackupPath, mainPath, true);
                    Console.WriteLine($"🔄 백업에서 모델 복구 성공: {userBackupPath} → {mainPath}");
                    return mainPath;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 백업 복구 실패: {ex.Message}");
            }
            return null;
        }

        private static string? TryLimitedDriveSearch()
        {
            try
            {
                Console.WriteLine("🔍 제한적 드라이브 검색 시작...");
                
                string[] searchDirs = {
                    @"C:\Program Files\MosaicCensorSystem",
                    @"C:\Program Files\BetaChip",
                    @"C:\Program Files (x86)\MosaicCensorSystem",
                    @"C:\Program Files (x86)\BetaChip",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MosaicCensorSystem"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MosaicCensorSystem"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "MosaicCensorSystem")
                };
                
                foreach (string searchDir in searchDirs)
                {
                    if (Directory.Exists(searchDir))
                    {
                        string[] possibleFiles = {
                            Path.Combine(searchDir, "best.onnx"),
                            Path.Combine(searchDir, "Resources", "best.onnx")
                        };
                        
                        foreach (string file in possibleFiles)
                        {
                            if (IsValidModelFile(file))
                            {
                                Console.WriteLine($"🎯 드라이브 검색에서 발견: {file}");
                                return file;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 드라이브 검색 실패: {ex.Message}");
            }
            
            return null;
        }

        private static string? GetAssemblyLocationPath()
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

        private static void DiagnoseEnvironment()
        {
            Console.WriteLine("\n=== 환경 진단 정보 ===");
            
            try
            {
                Console.WriteLine($"BaseDirectory: {AppDomain.CurrentDomain.BaseDirectory}");
                Console.WriteLine($"StartupPath: {Application.StartupPath}");
                Console.WriteLine($"CurrentDirectory: {Environment.CurrentDirectory}");
                Console.WriteLine($"ExecutingAssembly: {Assembly.GetExecutingAssembly().Location}");
                Console.WriteLine($"ProcessPath: {Environment.ProcessPath ?? "null"}");
                Console.WriteLine($"UserName: {Environment.UserName}");
                Console.WriteLine($"MachineName: {Environment.MachineName}");
                Console.WriteLine($"OS Version: {Environment.OSVersion}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"진단 정보 수집 실패: {ex.Message}");
            }
            
            string[] resourceDirs = {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources"),
                Path.Combine(Application.StartupPath, "Resources"),
                Path.Combine(Environment.CurrentDirectory, "Resources")
            };

            foreach (string dir in resourceDirs)
            {
                try
                {
                    Console.WriteLine($"\n📁 디렉터리 체크: {dir}");
                    Console.WriteLine($"   존재: {Directory.Exists(dir)}");
                    
                    if (Directory.Exists(dir))
                    {
                        var onnxFiles = Directory.GetFiles(dir, "*.onnx");
                        Console.WriteLine($"   ONNX 파일들: {string.Join(", ", onnxFiles.Select(Path.GetFileName))}");
                        
                        foreach (var file in onnxFiles)
                        {
                            try
                            {
                                var info = new FileInfo(file);
                                Console.WriteLine($"   {Path.GetFileName(file)}: {info.Length:N0} bytes");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"   {Path.GetFileName(file)}: 파일 정보 읽기 실패 - {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ❌ 디렉터리 접근 오류: {ex.Message}");
                }
            }
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
                    File.WriteAllText("fatal_error.log", errorLog);
                }
                catch { }
                
                MessageBox.Show("치명적인 오류가 발생했습니다. 프로그램을 종료합니다.", "오류", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                Console.WriteLine($"최종 ONNX 모델 경로: {ONNX_MODEL_PATH}");
                
                if (!IsValidModelFile(ONNX_MODEL_PATH))
                {
                    string message = "ONNX 모델 파일을 찾을 수 없거나 손상되었습니다.\n\n" +
                                   "필요한 파일: best.onnx\n" +
                                   "권장 위치:\n" +
                                   $"• {Path.Combine(Application.StartupPath, "Resources")}\n" +
                                   $"• {Application.StartupPath}\n\n" +
                                   "파일을 올바른 위치에 배치한 후 다시 실행해주세요.\n\n" +
                                   "또는 프로그램을 재설치해보세요.";
                    
                    MessageBox.Show(message, "모델 파일 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Console.WriteLine("⚠️ 모델 파일 없이 프로그램을 시작합니다. 일부 기능이 제한될 수 있습니다.");
                }
                else
                {
                    var fileInfo = new FileInfo(ONNX_MODEL_PATH);
                    Console.WriteLine($"✅ 모델 파일 검증 완료: {fileInfo.Length:N0} bytes");
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
                    File.WriteAllText("init_error.log", $"{DateTime.Now}: {ex}");
                }
                catch { }
            }
        }
    }
}