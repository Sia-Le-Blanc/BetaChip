using Microsoft.Win32;
using System;
using System.IO;
using System.Windows.Forms;

namespace MosaicCensorSystem
{
    internal static class Program
    {
        // ONNX 모델의 경로는 이제 레지스트리에서 직접 읽어옵니다.
        public static readonly string? ONNX_MODEL_PATH = GetPathFromRegistry("ModelPath");
        
        // 후원자 버전의 경우 스티커 경로도 레지스트리에서 읽어올 수 있습니다. (필요 시 주석 해제)
        // public static readonly string? STICKER_PATH = GetPathFromRegistry("StickerPath");

        /// <summary>
        /// 레지스트리에서 지정된 값 이름(ValueName)에 해당하는 경로를 찾습니다.
        /// 이 방식은 설치 경로, OneDrive, 한글 경로 등 모든 환경에서 안정적으로 동작합니다.
        /// </summary>
        /// <param name="valueName">레지스트리에서 찾을 값의 이름 (예: "ModelPath")</param>
        /// <returns>파일/폴더의 전체 경로. 찾지 못하면 null을 반환합니다.</returns>
        private static string? GetPathFromRegistry(string valueName)
        {
            const string registryKeyPath = @"SOFTWARE\BetaChip\MosaicCensorSystem";
            const string modelFileName = "best.onnx";

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
                        if (TryNormalizePath(rawPath, valueName, out string? normalized, modelFileName))
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

            Console.WriteLine($"❌ 32/64비트 레지스트리 어디에서도 '{valueName}' 경로를 찾을 수 없음");
            return null;
        }

        private static bool TryNormalizePath(string rawPath, string valueName, out string? normalizedPath, string modelFileName)
        {
            normalizedPath = null;

            string candidate = rawPath.Trim();

            if (candidate.Length == 0)
            {
                return false;
            }

            if (candidate.StartsWith('"') && candidate.EndsWith('"') && candidate.Length >= 2)
            {
                candidate = candidate[1..^1];
            }

            candidate = Environment.ExpandEnvironmentVariables(candidate);

            if (File.Exists(candidate))
            {
                normalizedPath = candidate;
                return true;
            }

            if (Directory.Exists(candidate) && valueName.Equals("ModelPath", StringComparison.OrdinalIgnoreCase))
            {
                string modelCandidate = Path.Combine(candidate, modelFileName);
                if (File.Exists(modelCandidate))
                {
                    Console.WriteLine($"ℹ️ '{valueName}' 값이 폴더를 가리켜 best.onnx 파일로 자동 보정: {modelCandidate}");
                    normalizedPath = modelCandidate;
                    return true;
                }
            }

            return false;
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
                    "프로그램 설치가 손상되었을 수 있습니다.\n\n" +
                    "제어판에서 프로그램을 완전히 제거한 후 다시 설치해 주세요.",
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