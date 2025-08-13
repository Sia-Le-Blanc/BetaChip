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
            // 여러 가능한 경로들을 순서대로 시도
            string[] possiblePaths = {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\best.onnx"),
                Path.Combine(Environment.CurrentDirectory, @"Resources\best.onnx"),
                Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", @"Resources\best.onnx"),
                Path.Combine(Application.StartupPath, @"Resources\best.onnx"),
                @".\Resources\best.onnx",
                @"Resources\best.onnx",
                @"best.onnx"
            };

            Console.WriteLine("=== ONNX 모델 경로 탐색 시작 ===");
            
            foreach (string path in possiblePaths)
            {
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

            // 모든 경로에서 실패한 경우 디렉터리 상세 진단
            DiagnoseEnvironment();
            
            Console.WriteLine("❌ 모든 경로에서 모델을 찾을 수 없음");
            return possiblePaths[0]; // 기본값 반환
        }

        private static void DiagnoseEnvironment()
        {
            Console.WriteLine("\n=== 환경 진단 정보 ===");
            Console.WriteLine($"BaseDirectory: {AppDomain.CurrentDomain.BaseDirectory}");
            Console.WriteLine($"CurrentDirectory: {Environment.CurrentDirectory}");
            Console.WriteLine($"ExecutingAssembly: {Assembly.GetExecutingAssembly().Location}");
            Console.WriteLine($"StartupPath: {Application.StartupPath}");
            
            // Resources 디렉터리 존재 여부 확인
            string[] resourceDirs = {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources"),
                Path.Combine(Environment.CurrentDirectory, "Resources"),
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