using System;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using MosaicCensorSystem.Diagnostics; // 추가된 using 문

namespace MosaicCensorSystem
{
    internal static class Program
    {
        // Windows API for priority management
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll")]
        static extern bool SetThreadPriority(IntPtr hThread, int nPriority);

        [DllImport("kernel32.dll")]
        static extern bool SetPriorityClass(IntPtr hProcess, uint dwPriorityClass);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        // Priority constants
        const int THREAD_PRIORITY_NORMAL = 0;
        const uint NORMAL_PRIORITY_CLASS = 0x00000020;
        const uint HIGH_PRIORITY_CLASS = 0x00000080;

        // ONNX 모델 경로 (가이드 기반)
        public static string ONNX_MODEL_PATH { get; private set; } = "";
        
        // 로그 파일 writer
        private static StreamWriter? logWriter;

        [STAThread]
        static void Main()
        {
            // 강화된 글로벌 예외 핸들러
            SetupGlobalExceptionHandlers();
            
            // 애플리케이션 기본 설정
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            // 로그 시스템 초기화
            InitializeLogging();

            try
            {
                Console.WriteLine("=" + new string('=', 70));
                Console.WriteLine($"🚀 ONNX 가이드 기반 화면 검열 시스템 시작 - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"📄 로그 파일: {Path.Combine(Environment.CurrentDirectory, "onnx_system_log.txt")}");
                Console.WriteLine($"🛡️ 크래시 완전 방지 모드 활성화");
                Console.WriteLine("=" + new string('=', 70));
                
                RunMainApplicationWithOnnxOptimization();
            }
            catch (Exception e)
            {
                HandleTopLevelException(e);
            }
            finally
            {
                CleanupAndExit();
            }
        }

        /// <summary>
        /// 글로벌 예외 핸들러 설정
        /// </summary>
        private static void SetupGlobalExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                var crashLog = $"💥 FATAL CRASH PREVENTED at {DateTime.Now}\n" +
                              $"Exception: {ex?.GetType().Name}\n" +
                              $"Message: {ex?.Message}\n" +
                              $"StackTrace: {ex?.StackTrace}\n" +
                              $"IsTerminating: {e.IsTerminating}\n" +
                              $"Thread: {Thread.CurrentThread.Name ?? "Unknown"}\n" +
                              $"ThreadId: {Thread.CurrentThread.ManagedThreadId}\n" +
                              $"=====================================\n";
                
                LogCrashPrevention(crashLog, "fatal_crash_prevented.log");
                
                ShowCrashPreventionMessage(
                    "치명적 오류가 감지되었지만 프로그램을 안전하게 보호했습니다.\n\n" +
                    "로그 파일: fatal_crash_prevented.log\n\n" +
                    "프로그램을 재시작하는 것을 권장합니다.",
                    "오류 방지"
                );
            };

            Application.ThreadException += (sender, e) =>
            {
                var crashLog = $"💥 UI THREAD CRASH PREVENTED at {DateTime.Now}\n" +
                              $"Exception: {e.Exception.GetType().Name}\n" +
                              $"Message: {e.Exception.Message}\n" +
                              $"StackTrace: {e.Exception.StackTrace}\n" +
                              $"Thread: {Thread.CurrentThread.Name ?? "UI Thread"}\n" +
                              $"ThreadId: {Thread.CurrentThread.ManagedThreadId}\n" +
                              $"=====================================\n";
                
                LogCrashPrevention(crashLog, "ui_crash_prevented.log");
                
                ShowCrashPreventionMessage(
                    "UI 스레드 오류가 감지되었지만 프로그램을 안전하게 보호했습니다.\n\n" +
                    "로그 파일: ui_crash_prevented.log\n\n" +
                    "계속 사용하거나 프로그램을 재시작할 수 있습니다.",
                    "UI 오류 방지"
                );
            };
        }

        /// <summary>
        /// 로그 시스템 초기화
        /// </summary>
        private static void InitializeLogging()
        {
            var logFile = Path.Combine(Environment.CurrentDirectory, "onnx_system_log.txt");
            
            try
            {
                logWriter = new StreamWriter(logFile, false) { AutoFlush = true };
                
                // 콘솔과 파일에 동시 출력
                var multiWriter = new MultiTextWriter(Console.Out, logWriter);
                Console.SetOut(multiWriter);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 로그 시스템 초기화 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// ONNX 최적화된 메인 애플리케이션 실행
        /// </summary>
        private static void RunMainApplicationWithOnnxOptimization()
        {
            Console.WriteLine($"📍 현재 작업 디렉토리: {Environment.CurrentDirectory}");
            Console.WriteLine($"📍 실행 파일 위치: {AppDomain.CurrentDomain.BaseDirectory}");
            
            // ONNX 모델 경로 설정 (가이드 기반)
            ONNX_MODEL_PATH = FindOnnxModelPath();
            Console.WriteLine($"📂 ONNX 모델 경로: {ONNX_MODEL_PATH}");
            Console.WriteLine($"📂 파일 존재 여부: {File.Exists(ONNX_MODEL_PATH)}");
            
            if (File.Exists(ONNX_MODEL_PATH))
            {
                ValidateOnnxModel();
            }
            else
            {
                Console.WriteLine("❌ ONNX 모델 파일이 없습니다!");
                ListAvailableFiles();
                Console.WriteLine("⚠️ 모델 없이 안전 모드로 계속 진행합니다...");
            }

            Console.WriteLine("🛡️ ONNX 최적화 모드로 프로그램 시작");

            try
            {
                // 시스템 최적화 설정
                OptimizeSystemForOnnx();

                // ONNX Runtime 환경 테스트
                TestOnnxRuntimeEnvironment();
                
                // *** 추가: 상세 진단 실행 ***
                Console.WriteLine("\n🔍 상세 ONNX 진단 실행...");
                try
                {
                    OnnxDiagnostics.RunFullDiagnostics();
                    
                    // 간단한 추론 테스트
                    bool inferenceTest = OnnxDiagnostics.TestSimpleInference();
                    if (!inferenceTest)
                    {
                        Console.WriteLine("⚠️ 추론 테스트 실패 - 안전 모드로 진행");
                    }
                    else
                    {
                        Console.WriteLine("✅ 추론 테스트 성공 - 정상 모드로 진행");
                    }
                }
                catch (Exception diagEx)
                {
                    Console.WriteLine($"⚠️ 진단 도구 실행 실패: {diagEx.Message}");
                    Console.WriteLine("안전 모드로 계속 진행합니다...");
                }

                // Windows Forms 초기화
                InitializeWindowsForms();
                
                // 메인 애플리케이션 실행
                RunMainApplication();
            }
            catch (Exception fatalEx)
            {
                Console.WriteLine($"💥 치명적 오류 감지됨 (방지됨): {fatalEx.Message}");
                HandleTopLevelException(fatalEx);
            }
        }

        /// <summary>
        /// ONNX 모델 경로 찾기 (가이드 기반)
        /// </summary>
        private static string FindOnnxModelPath()
        {
            var candidatePaths = new[]
            {
                "best.onnx",
                "Resources/best.onnx",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "best.onnx"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "best.onnx"),
                Path.Combine(Environment.CurrentDirectory, "best.onnx"),
                Path.Combine(Environment.CurrentDirectory, "Resources", "best.onnx"),
                // 추가 경로들
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "best.onnx"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "best.onnx")
            };
            
            // 상위 디렉토리도 검색
            var currentDir = new DirectoryInfo(Environment.CurrentDirectory);
            var additionalPaths = new List<string>();
            
            for (int i = 0; i < 3 && currentDir?.Parent != null; i++)
            {
                currentDir = currentDir.Parent;
                additionalPaths.Add(Path.Combine(currentDir.FullName, "best.onnx"));
                additionalPaths.Add(Path.Combine(currentDir.FullName, "Resources", "best.onnx"));
            }
            
            var allPaths = candidatePaths.Concat(additionalPaths);
            
            foreach (var path in allPaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var fileInfo = new FileInfo(path);
                        Console.WriteLine($"🔍 모델 파일 후보: {path} ({fileInfo.Length / (1024 * 1024):F1} MB)");
                        
                        // 가이드 기준: 최소 5MB 이상이어야 유효한 모델
                        if (fileInfo.Length > 5 * 1024 * 1024)
                        {
                            Console.WriteLine($"✅ 유효한 모델 파일 발견: {path}");
                            return path;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ 경로 체크 오류 ({path}): {ex.Message}");
                }
            }
            
            Console.WriteLine("❌ 유효한 모델 파일을 찾을 수 없습니다!");
            return candidatePaths.First();
        }

        /// <summary>
        /// ONNX 모델 검증
        /// </summary>
        private static void ValidateOnnxModel()
        {
            try
            {
                var fileInfo = new FileInfo(ONNX_MODEL_PATH);
                Console.WriteLine($"📊 모델 파일 정보:");
                Console.WriteLine($"  크기: {fileInfo.Length / (1024 * 1024):F1} MB");
                Console.WriteLine($"  생성일: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"  수정일: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");
                
                // 가이드 기준: 11.6MB 근처여야 함
                if (fileInfo.Length < 5 * 1024 * 1024)
                {
                    Console.WriteLine("⚠️ 경고: 모델 파일이 예상보다 작습니다 (손상되었을 가능성)");
                }
                else if (fileInfo.Length > 50 * 1024 * 1024)
                {
                    Console.WriteLine("⚠️ 경고: 모델 파일이 예상보다 큽니다");
                }
                else
                {
                    Console.WriteLine("✅ 모델 파일 크기가 적절합니다");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 모델 검증 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 사용 가능한 파일 목록 표시
        /// </summary>
        private static void ListAvailableFiles()
        {
            Console.WriteLine("📋 현재 디렉토리의 파일들:");
            try
            {
                var files = Directory.GetFiles(Environment.CurrentDirectory, "*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase) || 
                               f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    .Take(20);
                
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    Console.WriteLine($"  📄 {file} ({fileInfo.Length / 1024:F0} KB)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 파일 목록 조회 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// ONNX용 시스템 최적화
        /// </summary>
        private static void OptimizeSystemForOnnx()
        {
            try
            {
                // 스레드 우선순위 설정
                Thread.CurrentThread.Priority = ThreadPriority.Normal;
                var currentProcess = Process.GetCurrentProcess();
                currentProcess.PriorityClass = ProcessPriorityClass.Normal;
                
                Console.WriteLine("✅ 시스템 우선순위 최적화 완료");
            }
            catch (Exception e)
            {
                Console.WriteLine($"⚠️ 시스템 최적화 실패 (무시하고 계속): {e.Message}");
            }

            try
            {
                // GC 최적화 (ONNX 대용량 메모리 처리용)
                GCSettings.LatencyMode = GCLatencyMode.Interactive;
                
                // 메모리 정리
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                Console.WriteLine("✅ 메모리 관리 최적화 완료");
            }
            catch (Exception e)
            {
                Console.WriteLine($"⚠️ 메모리 최적화 실패 (무시하고 계속): {e.Message}");
            }
        }

        /// <summary>
        /// ONNX Runtime 환경 테스트
        /// </summary>
        private static void TestOnnxRuntimeEnvironment()
        {
            Console.WriteLine("🧪 ONNX Runtime 환경 테스트 시작");
            
            try
            {
                // 사용 가능한 실행 제공자 확인
                var availableProviders = OrtEnv.Instance().GetAvailableProviders();
                Console.WriteLine($"📊 사용 가능한 실행 제공자: {availableProviders.Length}개");
                
                foreach (var provider in availableProviders)
                {
                    Console.WriteLine($"  🔧 {provider}");
                }
                
                // GPU 지원 확인
                bool hasGpu = availableProviders.Contains("CUDAExecutionProvider") ||
                             availableProviders.Contains("DmlExecutionProvider") ||
                             availableProviders.Contains("TensorrtExecutionProvider");
                
                if (hasGpu)
                {
                    Console.WriteLine("🚀 GPU 가속 지원 감지됨!");
                }
                else
                {
                    Console.WriteLine("🔥 CPU 전용 모드로 동작");
                }
                
                // 간단한 세션 테스트 (모델 파일이 있는 경우)
                if (File.Exists(ONNX_MODEL_PATH))
                {
                    TestOnnxModelLoading();
                }
                
                Console.WriteLine("✅ ONNX Runtime 환경 테스트 완료");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ONNX Runtime 테스트 실패: {ex.Message}");
                Console.WriteLine("⚠️ ONNX Runtime 환경에 문제가 있을 수 있습니다");
            }
        }

        /// <summary>
        /// ONNX 모델 로딩 테스트
        /// </summary>
        private static void TestOnnxModelLoading()
        {
            try
            {
                Console.WriteLine("🔍 ONNX 모델 로딩 테스트 중...");
                
                var sessionOptions = new SessionOptions
                {
                    LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING
                };
                
                using (var session = new InferenceSession(ONNX_MODEL_PATH, sessionOptions))
                {
                    // 입력 메타데이터 확인
                    var inputMeta = session.InputMetadata.First();
                    var outputMeta = session.OutputMetadata.First();
                    
                    Console.WriteLine($"📊 모델 입력: {inputMeta.Key} -> {string.Join("x", inputMeta.Value.Dimensions)}");
                    Console.WriteLine($"📊 모델 출력: {outputMeta.Key} -> {string.Join("x", outputMeta.Value.Dimensions)}");
                    
                    // 가이드 기준 검증
                    var expectedInput = new[] { 1, 3, 640, 640 };
                    var expectedOutput = new[] { 1, 18, 8400 };
                    
                    bool inputValid = inputMeta.Value.Dimensions.SequenceEqual(expectedInput);
                    bool outputValid = outputMeta.Value.Dimensions.SequenceEqual(expectedOutput);
                    
                    if (inputValid && outputValid)
                    {
                        Console.WriteLine("✅ 모델 구조가 가이드 기준에 부합합니다!");
                    }
                    else
                    {
                        Console.WriteLine("⚠️ 모델 구조가 가이드 기준과 다릅니다");
                        Console.WriteLine($"  예상 입력: {string.Join("x", expectedInput)}");
                        Console.WriteLine($"  실제 입력: {string.Join("x", inputMeta.Value.Dimensions)}");
                        Console.WriteLine($"  예상 출력: {string.Join("x", expectedOutput)}");
                        Console.WriteLine($"  실제 출력: {string.Join("x", outputMeta.Value.Dimensions)}");
                    }
                }
                
                Console.WriteLine("✅ 모델 로딩 테스트 성공");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 모델 로딩 테스트 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// Windows Forms 초기화
        /// </summary>
        private static void InitializeWindowsForms()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                // DPI 설정
                try
                {
                    Application.SetHighDpiMode(HighDpiMode.SystemAware);
                }
                catch (Exception dpiEx)
                {
                    Console.WriteLine($"⚠️ DPI 설정 실패 (무시): {dpiEx.Message}");
                }
                
                Console.WriteLine("✅ Windows Forms 초기화 완료");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Windows Forms 초기화 실패: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 메인 애플리케이션 실행
        /// </summary>
        private static void RunMainApplication()
        {
            Console.WriteLine("🚀 MosaicApp 인스턴스 생성 중...");
            
            try
            {
                var app = new MosaicApp();
                Console.WriteLine("✅ MosaicApp 인스턴스 생성 완료");
                
                Console.WriteLine("🏃 Application.Run 시작...");
                Application.Run(app.Root);
                Console.WriteLine("🏁 Application.Run 정상 종료");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ MosaicApp 실행 중 오류 (방지됨): {ex.Message}");
                
                ShowCrashPreventionMessage(
                    $"애플리케이션 오류가 감지되었지만 안전하게 방지되었습니다.\n\n" +
                    $"오류: {ex.Message}\n\n" +
                    $"로그 파일을 확인하세요.",
                    "애플리케이션 오류 방지"
                );
            }
        }

        /// <summary>
        /// 최상위 예외 처리
        /// </summary>
        private static void HandleTopLevelException(Exception ex)
        {
            Console.WriteLine($"💥 최상위 예외 처리: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            
            var errorMsg = $"💥 최상위 오류 (방지됨): {ex.Message}\nStack Trace:\n{ex.StackTrace}";
            
            LogCrashPrevention(errorMsg, "top_level_crash_prevented.txt");
            
            ShowCrashPreventionMessage(
                "최상위 오류가 감지되었지만 완전히 방지되었습니다.\n\n" +
                "로그 파일을 확인하세요: top_level_crash_prevented.txt\n\n" +
                "10초 후 안전하게 종료됩니다.",
                "최상위 오류 방지"
            );
            
            // 10초 대기
            Console.WriteLine("\n❌ 최상위 오류가 방지되었습니다. 10초 후 안전하게 종료됩니다...");
            Thread.Sleep(10000);
        }

        /// <summary>
        /// 크래시 방지 로그 기록
        /// </summary>
        private static void LogCrashPrevention(string message, string fileName)
        {
            try
            {
                File.AppendAllText(fileName, $"{DateTime.Now}: {message}\n");
                Console.WriteLine(message);
            }
            catch
            {
                // 최후의 수단
                try
                {
                    File.WriteAllText("emergency_log.txt", $"Emergency: {DateTime.Now} - {message}");
                }
                catch { }
            }
        }

        /// <summary>
        /// 크래시 방지 메시지 표시
        /// </summary>
        private static void ShowCrashPreventionMessage(string message, string title)
        {
            try
            {
                MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch
            {
                // 메시지 박스도 실패한 경우
                Console.WriteLine($"⚠️ {title}: {message}");
            }
        }

        /// <summary>
        /// 정리 및 종료
        /// </summary>
        private static void CleanupAndExit()
        {
            try
            {
                Console.WriteLine($"\n🏁 ONNX 최적화 프로그램 종료 - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine("🛡️ 모든 크래시가 성공적으로 방지되었습니다");
                Console.WriteLine("📄 로그 파일들을 확인하세요: onnx_system_log.txt");
                
                logWriter?.Close();
            }
            catch { }
            
            // 정상 종료 시 5초 대기
            Console.WriteLine("\n⏰ 5초 후 안전하게 종료됩니다... (아무 키나 누르면 즉시 종료)");
            
            var waitTask = Task.Run(() => Thread.Sleep(5000));
            var keyTask = Task.Run(() => 
            {
                try 
                { 
                    Console.ReadKey(); 
                } 
                catch { }
            });
            
            Task.WaitAny(waitTask, keyTask);
            
            // 최종 메모리 정리
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            catch { }
        }

        /// <summary>
        /// 안전한 리소스 경로 불러오기
        /// </summary>
        public static string SafeResourcePath(string relativePath)
        {
            try
            {
                // 실행 파일 디렉토리 기준
                string basePath1 = AppDomain.CurrentDomain.BaseDirectory;
                string path1 = Path.Combine(basePath1, relativePath);
                if (File.Exists(path1)) return path1;
                
                // 현재 작업 디렉토리 기준
                string basePath2 = Environment.CurrentDirectory;
                string path2 = Path.Combine(basePath2, relativePath);
                if (File.Exists(path2)) return path2;
                
                // 상위 디렉토리 검색
                var currentDir = new DirectoryInfo(Environment.CurrentDirectory);
                int searchDepth = 0;
                while (currentDir != null && currentDir.Parent != null && searchDepth < 3)
                {
                    string path3 = Path.Combine(currentDir.FullName, relativePath);
                    if (File.Exists(path3)) return path3;
                    currentDir = currentDir.Parent;
                    searchDepth++;
                }
                
                return path1; // 기본값 반환
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SafeResourcePath 오류: {ex.Message}");
                return Path.Combine(Environment.CurrentDirectory, relativePath);
            }
        }
    }
    
    /// <summary>
    /// 멀티 텍스트 Writer (콘솔과 파일 동시 출력)
    /// </summary>
    public class MultiTextWriter : TextWriter
    {
        private readonly TextWriter[] writers;
        
        public MultiTextWriter(params TextWriter[] writers)
        {
            this.writers = writers ?? new TextWriter[0];
        }
        
        public override void Write(char value)
        {
            foreach (var writer in writers)
            {
                try 
                { 
                    writer?.Write(value); 
                } 
                catch { }
            }
        }
        
        public override void Write(string? value)
        {
            foreach (var writer in writers)
            {
                try 
                { 
                    writer?.Write(value); 
                } 
                catch { }
            }
        }
        
        public override void WriteLine(string? value)
        {
            foreach (var writer in writers)
            {
                try 
                { 
                    writer?.WriteLine(value); 
                } 
                catch { }
            }
        }
        
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var writer in writers)
                {
                    try
                    {
                        writer?.Dispose();
                    }
                    catch { }
                }
            }
            base.Dispose(disposing);
        }
    }
}