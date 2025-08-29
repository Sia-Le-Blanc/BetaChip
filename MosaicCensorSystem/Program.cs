using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace MosaicCensorSystem
{
    internal static class Program
    {
        // ★★★ 개선된 ONNX 모델 경로 탐색 로직 ★★★
        public static readonly string ONNX_MODEL_PATH = FindModelPath();

        private static string FindModelPath()
        {
            // ★★★ 단순하고 확실한 경로들만 시도 ★★★
            string[] safePaths = {
                // 1. 가장 안전 - 실행파일 기준 Resources 폴더
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "best.onnx"),
                
                // 2. WinForms 환경에서 안전
                Path.Combine(Application.StartupPath, "Resources", "best.onnx"),
                
                // 3. 백업 - 실행파일과 같은 폴더에 직접
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "best.onnx"),
                
                // 4. 백업 - StartupPath에 직접  
                Path.Combine(Application.StartupPath, "best.onnx"),
                
                // 5. 현재 작업 디렉터리 기준
                Path.Combine(Environment.CurrentDirectory, "Resources", "best.onnx"),
                
                // 6. 단일파일 배포 대응
                GetAssemblyLocationPath(),
            };

            Console.WriteLine("=== 견고한 ONNX 모델 경로 탐색 시작 ===");
            
            foreach (string path in safePaths.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                try
                {
                    string fullPath = Path.GetFullPath(path);
                    Console.WriteLine($"시도: {fullPath}");
                    
                    // ★★★ 파일 존재 여부와 유효성을 동시에 확인 ★★★
                    if (IsValidModelFile(fullPath))
                    {
                        Console.WriteLine($"✅ 모델 발견: {fullPath}");
                        return fullPath;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ 경로 오류 ({path}): {ex.Message}");
                }
            }

            // 모든 안전한 경로에서 실패한 경우 상세 진단
            DiagnoseEnvironment();
            
            Console.WriteLine("❌ 모든 경로에서 유효한 모델을 찾을 수 없음");
            return safePaths[0]; // 기본값 반환
        }

        // ★★★ 유효한 모델 파일인지 확인 (존재 여부 + 크기 체크) ★★★
        private static bool IsValidModelFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;
                
                var fileInfo = new FileInfo(filePath);
                
                // 0바이트 파일은 잘못된 파일 (빌드 실패 등으로 인한)
                if (fileInfo.Length == 0)
                {
                    Console.WriteLine($"⚠️ 파일이 비어있음: {filePath}");
                    return false;
                }
                
                // ONNX 파일은 최소 몇 KB는 되어야 함
                if (fileInfo.Length < 1024)
                {
                    Console.WriteLine($"⚠️ 파일이 너무 작음 ({fileInfo.Length} bytes): {filePath}");
                    return false;
                }
                
                // ★★★ 파일 읽기 권한 확인 ★★★
                using (var stream = File.OpenRead(filePath))
                {
                    var buffer = new byte[4];
                    int bytesRead = stream.Read(buffer, 0, 4);
                    if (bytesRead > 0)
                    {
                        Console.WriteLine($"✅ 유효한 모델 파일: {fileInfo.Length:N0} bytes");
                        return true;
                    }
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
                Console.WriteLine($"❌ 파일 검증 중 오류: {filePath} - {ex.Message}");
                return false;
            }
        }

        // ★★★ 단일파일 배포 대응 경로 ★★★
        private static string GetAssemblyLocationPath()
        {
            try
            {
                var location = Assembly.GetExecutingAssembly().Location;
                
                // .NET 5+ 단일파일 배포에서는 Location이 빈 문자열일 수 있음
                if (string.IsNullOrEmpty(location))
                {
                    location = Environment.ProcessPath;
                }
                
                if (!string.IsNullOrEmpty(location))
                {
                    var dir = Path.GetDirectoryName(location);
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
                Console.WriteLine($"ProcessPath: {Environment.ProcessPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"진단 정보 수집 실패: {ex.Message}");
            }
            
            // Resources 디렉터리 존재 여부 확인
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
                        
                        var allFiles = Directory.GetFiles(dir);
                        Console.WriteLine($"   모든 파일들: {string.Join(", ", allFiles.Select(Path.GetFileName))}");
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
            // ★★★ 전역 예외 처리 ★★★
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
                // ★★★ 프로그램 시작 전 모델 파일 필수 체크 ★★★
                Console.WriteLine($"최종 ONNX 모델 경로: {ONNX_MODEL_PATH}");
                
                if (!IsValidModelFile(ONNX_MODEL_PATH))
                {
                    // 사용자에게 명확한 안내 제공
                    string message = "ONNX 모델 파일을 찾을 수 없거나 손상되었습니다.\n\n" +
                                   "필요한 파일: best.onnx\n" +
                                   "권장 위치:\n" +
                                   $"• {Path.Combine(Application.StartupPath, "Resources")}\n" +
                                   $"• {Application.StartupPath}\n\n" +
                                   "파일을 올바른 위치에 배치한 후 다시 실행해주세요.";
                    
                    MessageBox.Show(message, "모델 파일 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    
                    // ★★★ 파일이 없어도 프로그램은 실행하되, 사용자에게 알림 ★★★
                    Console.WriteLine("⚠️ 모델 파일 없이 프로그램을 시작합니다. 일부 기능이 제한될 수 있습니다.");
                }
                else
                {
                    Console.WriteLine($"✅ 모델 파일 검증 완료: {new FileInfo(ONNX_MODEL_PATH).Length:N0} bytes");
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
                    File.WriteAllText("init_error.log", $"{DateTime.Now}: {ex.ToString()}");
                }
                catch { }
            }
        }
    }
}