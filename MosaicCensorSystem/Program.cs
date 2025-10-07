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

            try
            {
                // HKEY_LOCAL_MACHINE (HKLM)은 시스템 전체에 적용되며 설치 시 관리자 권한으로 기록되어 신뢰할 수 있습니다.
                using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(registryKeyPath))
                {
                    if (key?.GetValue(valueName) is string path && !string.IsNullOrEmpty(path))
                    {
                        // 레지스트리에 기록된 경로가 실제로 존재하는지 최종 확인합니다.
                        // File.Exists는 파일에, Directory.Exists는 폴더에 사용될 수 있습니다.
                        if (File.Exists(path) || Directory.Exists(path))
                        {
                            Console.WriteLine($"✅ 레지스트리에서 유효한 경로 발견 [{valueName}]: {path}");
                            return path;
                        }
                        else
                        {
                             Console.WriteLine($"❌ 레지스트리 경로는 찾았으나 파일/폴더가 없음: {path}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 레지스트리 접근 중 오류 발생 [{valueName}]: {ex.Message}");
            }
            
            Console.WriteLine($"❌ 레지스트리에서 '{valueName}' 경로를 찾을 수 없음");
            return null;
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