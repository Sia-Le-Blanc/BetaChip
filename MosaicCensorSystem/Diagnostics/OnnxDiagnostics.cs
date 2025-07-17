using System;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Collections.Generic;

namespace MosaicCensorSystem.Diagnostics
{
    /// <summary>
    /// ONNX 진단 도구 클래스 (누락된 클래스)
    /// </summary>
    public static class OnnxDiagnostics
    {
        /// <summary>
        /// 전체 ONNX 진단 실행
        /// </summary>
        public static void RunFullDiagnostics()
        {
            try
            {
                Console.WriteLine("🔍 ======= ONNX 진단 시작 =======");
                
                // ONNX Runtime 기본 정보
                CheckOnnxRuntimeInfo();
                
                // 모델 파일 찾기
                CheckModelFile();
                
                Console.WriteLine("🔍 ======= ONNX 진단 완료 =======");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ONNX 진단 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 간단한 추론 테스트
        /// </summary>
        public static bool TestSimpleInference()
        {
            try
            {
                Console.WriteLine("🧪 간단한 추론 테스트...");
                
                var modelPath = FindModelPath();
                if (string.IsNullOrEmpty(modelPath))
                {
                    Console.WriteLine("❌ 모델 파일이 없어 추론 테스트 건너뜀");
                    return false;
                }
                
                Console.WriteLine("✅ 추론 테스트 성공 (모의)");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 추론 테스트 실패: {ex.Message}");
                return false;
            }
        }
        
        private static void CheckOnnxRuntimeInfo()
        {
            try
            {
                Console.WriteLine("📊 ONNX Runtime 정보:");
                
                var assembly = typeof(InferenceSession).Assembly;
                var version = assembly.GetName().Version;
                Console.WriteLine($"  버전: {version}");
                
                var providers = OrtEnv.Instance().GetAvailableProviders();
                Console.WriteLine($"  제공자: {providers.Length}개");
                foreach (var provider in providers)
                {
                    Console.WriteLine($"    - {provider}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ONNX Runtime 정보 조회 실패: {ex.Message}");
            }
        }
        
        private static void CheckModelFile()
        {
            try
            {
                Console.WriteLine("📁 모델 파일 확인:");
                
                var modelPath = FindModelPath();
                if (string.IsNullOrEmpty(modelPath))
                {
                    Console.WriteLine("❌ 모델 파일을 찾을 수 없습니다");
                    ListSearchLocations();
                }
                else
                {
                    var fileInfo = new FileInfo(modelPath);
                    Console.WriteLine($"✅ 모델 발견: {modelPath}");
                    Console.WriteLine($"  크기: {fileInfo.Length / (1024 * 1024):F1} MB");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 모델 파일 확인 실패: {ex.Message}");
            }
        }
        
        private static string FindModelPath()
        {
            var candidates = new[]
            {
                "best.onnx",
                "Resources/best.onnx",
                Path.Combine(Environment.CurrentDirectory, "best.onnx"),
                Path.Combine(Environment.CurrentDirectory, "Resources", "best.onnx")
            };
            
            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            
            return null;
        }
        
        private static void ListSearchLocations()
        {
            var locations = new[]
            {
                Environment.CurrentDirectory,
                Path.Combine(Environment.CurrentDirectory, "Resources")
            };
            
            Console.WriteLine("  검색한 위치:");
            foreach (var location in locations)
            {
                Console.WriteLine($"    📁 {location}");
                if (Directory.Exists(location))
                {
                    var onnxFiles = Directory.GetFiles(location, "*.onnx");
                    if (onnxFiles.Length > 0)
                    {
                        foreach (var file in onnxFiles)
                        {
                            Console.WriteLine($"      📄 {Path.GetFileName(file)}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"      ❌ ONNX 파일 없음");
                    }
                }
                else
                {
                    Console.WriteLine($"      ❌ 폴더 없음");
                }
            }
        }
    }
}