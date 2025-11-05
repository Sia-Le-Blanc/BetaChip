using Microsoft.Win32;
using System;
using System.IO;
using System.Windows.Forms;

namespace MosaicCensorSystem
{
    internal static class Program
    {
        // ONNX 모델의 경로를 레지스트리 또는 로컬에서 찾습니다.
        public static readonly string? ONNX_MODEL_PATH = GetModelPath();
        
        // 후원자 버전의 경우 스티커 경로도 레지스트리에서 읽어올 수 있습니다. (필요 시 주석 해제)
        // public static readonly string? STICKER_PATH = GetPathFromRegistry("StickerPath");

        /// <summary>
        /// ONNX 모델 파일의 경로를 찾습니다.
        /// 1순위: 레지스트리 (정식 설치 시)
        /// 2순위: 실행 파일 기준 상대 경로 (개발 중 또는 포터블 실행)
        /// </summary>
        private static string? GetModelPath()
        {
            const string modelFileName = "best.onnx";

            // 1. 레지스트리에서 찾기 (설치된 경우)
            string? registryPath = GetPathFromRegistry("ModelPath");
            if (!string.IsNullOrEmpty(registryPath))
            {
                // 레지스트리 값이 폴더를 가리킬 수도 있으므로 검증
                if (File.Exists(registryPath))
                {
                    Console.WriteLine($"✅ 레지스트리에서 모델 발견: {registryPath}");
                    return registryPath;
                }
                
                // 레지스트리 값이 폴더인 경우 best.onnx를 추가
                if (Directory.Exists(registryPath))
                {
                    string modelInFolder = Path.Combine(registryPath, modelFileName);
                    if (File.Exists(modelInFolder))
                    {
                        Console.WriteLine($"✅ 레지스트리 폴더에서 모델 발견: {modelInFolder}");
                        return modelInFolder;
                    }
                }
                
                Console.WriteLine($"⚠️ 레지스트리 값이 존재하지만 파일을 찾을 수 없음: {registryPath}");
            }

            // 2. 실행 파일 기준 상대 경로 (개발 중 또는 포터블 실행)
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", modelFileName);
            if (File.Exists(localPath))
            {
                Console.WriteLine($"✅ 로컬 경로에서 모델 발견: {localPath}");
                return localPath;
            }

            // 3. Resources 폴더 없이 바로 실행 폴더에 있는 경우도 확인
            string directPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelFileName);
            if (File.Exists(directPath))
            {
                Console.WriteLine($"✅ 실행 폴더에서 모델 발견: {directPath}");
                return directPath;
            }

            Console.WriteLine("❌ 모델 파일을 어디에서도 찾을 수 없습니다.");
            Console.WriteLine($"   검색 경로 1: {registryPath ?? "(레지스트리 없음)"}");
            Console.WriteLine($"   검색 경로 2: {localPath}");
            Console.WriteLine($"   검색 경로 3: {directPath}");
            return null;
        }

        /// <summary>
        /// 레지스트리에서 지정된 값 이름(ValueName)에 해당하는 경로를 찾습니다.
        /// 이 방식은 설치 경로, OneDrive, 한글 경로 등 모든 환경에서 안정적으로 동작합니다.
        /// </summary>
        /// <param name="valueName">레지스트리에서 찾을 값의 이름 (예: "ModelPath")</param>
        /// <returns>파일/폴더의 전체 경로. 찾지 못하면 null을 반환합니다.</returns>
        private static string? GetPathFromRegistry(string valueName)
        {
            const string registryKeyPath = @"SOFTWARE\BetaChip\MosaicCensorSystem";

            RegistryView[] viewsToProbe =
            {
                RegistryView.Registry64,
                RegistryView.Registry32,
            };

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
                            Console.WriteLine($"✅ 레지스트리({view})에서 유효한 경로 발견 [{valueName}]: {normalized}");
                            return normalized;
                        }

                        Console.WriteLine($"❌ 레지스트리({view}) 값은 존재하지만 유효한 경로가 아님: {rawPath}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 레지스트리({view}) 접근 중 오류 발생 [{valueName}]: {ex.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// 레지스트리에서 읽은 경로를 정규화합니다.
        /// </summary>
        private static string NormalizePath(string rawPath)
        {
            string candidate = rawPath.Trim();

            if (candidate.Length == 0)
            {
                return string.Empty;
            }

            // 따옴표 제거
            if (candidate.StartsWith('"') && candidate.EndsWith('"') && candidate.Length >= 2)
            {
                candidate = candidate[1..^1];
            }

            // 환경 변수 확장
            candidate = Environment.ExpandEnvironmentVariables(candidate);

            // 경로 존재 여부 확인
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }

            return string.Empty;
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // 프로그램 시작 시 모델 경로 유효성 검사
            if (string.IsNullOrEmpty(ONNX_MODEL_PATH))
            {
                MessageBox.Show(
                    "핵심 AI 모델 파일(best.onnx)을 찾을 수 없습니다.\n\n" +
                    "다음을 확인해주세요:\n" +
                    "1. 프로그램이 올바르게 설치되었는지\n" +
                    "2. Resources 폴더에 best.onnx 파일이 있는지\n" +
                    "3. 파일을 직접 실행하는 경우 실행 파일과 같은 폴더 또는 Resources 폴더에 best.onnx가 있는지\n\n" +
                    "문제가 지속되면 프로그램을 재설치해주세요.",
                    "실행 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return; // 모델이 없으면 프로그램 실행 중단
            }

            try
            {
                var app = new MosaicApp(); // MosaicApp 생성자에서 Program.ONNX_MODEL_PATH를 참조하도록 구현
                app.Run();
            }
            catch (Exception ex)
            {
                string errorMessage = $"프로그램 초기화 중 오류가 발생했습니다:\n\n{ex.Message}";
                MessageBox.Show(errorMessage, "초기화 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}