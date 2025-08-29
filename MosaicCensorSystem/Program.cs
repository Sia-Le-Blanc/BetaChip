using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace MosaicCensorSystem
{
    internal static class Program
    {
        // ★★★ 견고한 ONNX 모델 경로 탐색 로직 ★★★
        public static readonly string ONNX_MODEL_PATH = FindModelPath();

        private static string FindModelPath()
        {
            // ★★★ 안전한 경로들만 우선 순위별로 시도 ★★★
            string[] safePaths = {
                // 1. 가장 안전 - 실행파일 기준 Resources 폴더
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "best.onnx"),
                
                // 2. WinForms 환경에서 안전
                Path.Combine(Application.StartupPath, "Resources", "best.onnx"),
                
                // 3. 백업 - 실행파일과 같은 폴더에 직접
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "best.onnx"),
                
                // 4. 백업 - StartupPath에 직접  
                Path.Combine(Application.StartupPath, "best.onnx"),
                
                // 5. 단일파일 배포 대응 (조건부)
                GetAssemblyLocationPath(),
            };

            Console.WriteLine("=== 견고한 ONNX 모델 경로 탐색 시작 ===");
            
            foreach (string path in safePaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                
                try
                {
                    string fullPath = Path.GetFullPath(path);
                    Console.WriteLine($"시도: {fullPath}");
                    
                    if (File.Exists(fullPath))
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
            
            Console.WriteLine("❌ 모든 안전한 경로에서 모델을 찾을 수 없음");
            return safePaths[0]; // 기본값 반환
        }

        // ★★★ 단일파일 배포 대응 경로 ★★★
        private static string GetAssemblyLocationPath()
        {
            try
            {
                var location = Assembly.GetExecutingAssembly().Location;
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"진단 정보 수집 실패: {ex.Message}");
            }
            
            // Resources 디렉터리 존재 여부 확인
            string[] resourceDirs = {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources"),
                Path.Combine(Application.StartupPath, "Resources")
            };

            foreach (string dir in resourceDirs)
            {
                try
                {
                    Console.WriteLine($"\n📁 디렉터리 체크: {dir}");
                    Console.WriteLine($"   존재: {Directory.Exists(dir)}");
                    
                    if (Directory.Exists(dir))
                    {
                        var files = Directory.GetFiles(dir, "*.onnx");
                        Console.WriteLine($"   ONNX 파일들: {string.Join(", ", files.Select(Path.GetFileName))}");
                        
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
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                File.WriteAllText("fatal_error.log", $"{DateTime.Now}: {ex?.ToString()}");
                MessageBox.Show("치명적인 오류가 발생했습니다. 프로그램을 종료합니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                // ONNX 모델 경로 진단 정보 출력
                Console.WriteLine($"최종 ONNX 모델 경로: {ONNX_MODEL_PATH}");
                Console.WriteLine($"파일 존재 여부: {File.Exists(ONNX_MODEL_PATH)}");
                
                var app = new MosaicApp();
                app.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"프로그램 초기화 중 오류가 발생했습니다: {ex.Message}", "초기화 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                File.WriteAllText("init_error.log", $"{DateTime.Now}: {ex.ToString()}");
            }
        }
    }
}