using System;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;

namespace MosaicCensorSystem.Diagnostics
{
    /// <summary>
    /// ONNX Runtime 환경을 간단히 진단하는 클래스 (단순화 버전)
    /// </summary>
    public static class OnnxDiagnostics
    {
        /// <summary>
        /// ONNX Runtime의 핵심 정보를 확인하고 출력합니다.
        /// </summary>
        public static void RunFullDiagnostics()
        {
            try
            {
                Console.WriteLine("--- ONNX Runtime 진단 시작 ---");

                // 1. ONNX 런타임 버전 정보 확인
                var runtimeVersion = typeof(InferenceSession).Assembly.GetName().Version;
                Console.WriteLine($"✅ ONNX Runtime 버전: {runtimeVersion}");

                // 2. 사용 가능한 실행 제공자(Execution Provider) 확인
                var providers = OrtEnv.Instance().GetAvailableProviders();
                Console.WriteLine($"✅ 사용 가능한 제공자: {string.Join(", ", providers)}");

                // 3. 모델 파일 존재 여부 확인
                var modelPath = Program.STANDARD_MODEL_PATH; // Program.cs에서 찾은 경로 활용
                if (File.Exists(modelPath))
                {
                    Console.WriteLine($"✅ 모델 파일 발견: {modelPath}");
                }
                else
                {
                    Console.WriteLine($"❌ 모델 파일을 찾을 수 없음: {modelPath}");
                }
                
                Console.WriteLine("--- ONNX Runtime 진단 완료 ---");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🚨 ONNX 진단 중 오류 발생: {ex.Message}");
            }
        }
    }
}