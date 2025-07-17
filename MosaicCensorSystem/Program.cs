using System;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using System.Runtime;
using Microsoft.ML.OnnxRuntime;
using MosaicCensorSystem.Diagnostics;

namespace MosaicCensorSystem
{
    internal static class Program
    {
        public static string ONNX_MODEL_PATH { get; private set; } = "";

        [STAThread]
        static void Main()
        {
            try
            {
                Console.WriteLine("🚀 간소화된 화면 검열 시스템 시작");
                Console.WriteLine($"📅 시작 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"📁 작업 디렉토리: {Environment.CurrentDirectory}");
                
                // 글로벌 예외 핸들러 설정
                SetupExceptionHandlers();
                
                // ONNX 모델 찾기
                ONNX_MODEL_PATH = FindOnnxModelPath();
                Console.WriteLine($"📂 ONNX 모델 경로: {ONNX_MODEL_PATH}");
                Console.WriteLine($"📂 파일 존재: {File.Exists(ONNX_MODEL_PATH)}");
                
                // 간단한 ONNX 진단
                Console.WriteLine("\n🔍 ONNX 진단 시작...");
                OnnxDiagnostics.RunFullDiagnostics();
                
                // Windows Forms 초기화
                Console.WriteLine("\n🖼️ Windows Forms 초기화...");
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                try
                {
                    Application.SetHighDpiMode(HighDpiMode.SystemAware);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ DPI 설정 실패: {ex.Message}");
                }
                
                // 메인 애플리케이션 실행
                Console.WriteLine("\n🚀 MosaicApp 시작...");
                var app = new MosaicApp();
                Console.WriteLine("✅ MosaicApp 인스턴스 생성 완료");
                
                Console.WriteLine("🏃 Application.Run 시작...");
                Application.Run(app.Root);
                Console.WriteLine("🏁 Application.Run 정상 종료");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 오류 발생: {ex.GetType().Name}");
                Console.WriteLine($"메시지: {ex.Message}");
                Console.WriteLine($"스택 트레이스: {ex.StackTrace}");
                
                // 오류 로그 저장
                try
                {
                    File.WriteAllText("error_log.txt", $"{DateTime.Now}: {ex}\n");
                    Console.WriteLine("📄 오류 로그 저장: error_log.txt");
                }
                catch { }
                
                // 사용자에게 오류 표시
                try
                {
                    MessageBox.Show($"오류가 발생했습니다:\n\n{ex.Message}\n\n로그 파일: error_log.txt", 
                        "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
            }
            finally
            {
                Console.WriteLine($"\n🏁 프로그램 종료: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine("5초 후 종료됩니다...");
                Thread.Sleep(5000);
            }
        }

        private static void SetupExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                Console.WriteLine($"💥 치명적 오류: {ex?.Message}");
                
                try
                {
                    File.WriteAllText("fatal_error.txt", $"{DateTime.Now}: {ex}\n");
                }
                catch { }
            };

            Application.ThreadException += (sender, e) =>
            {
                Console.WriteLine($"💥 UI 오류: {e.Exception.Message}");
                
                try
                {
                    File.WriteAllText("ui_error.txt", $"{DateTime.Now}: {e.Exception}\n");
                }
                catch { }
            };
        }

        private static string FindOnnxModelPath()
        {
            var candidates = new[]
            {
                "Resources/best.onnx",
                "best.onnx",
                Path.Combine(Environment.CurrentDirectory, "Resources", "best.onnx"),
                Path.Combine(Environment.CurrentDirectory, "best.onnx"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "best.onnx"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "best.onnx")
            };
            
            foreach (var path in candidates)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var fileInfo = new FileInfo(path);
                        Console.WriteLine($"🔍 모델 후보: {path} ({fileInfo.Length / (1024 * 1024):F1} MB)");
                        
                        if (fileInfo.Length > 5 * 1024 * 1024) // 5MB 이상
                        {
                            Console.WriteLine($"✅ 유효한 모델 발견: {path}");
                            return path;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ 경로 체크 오류 ({path}): {ex.Message}");
                }
            }
            
            Console.WriteLine("❌ 유효한 모델을 찾을 수 없습니다");
            return candidates[0]; // 기본값 반환
        }
    }
}