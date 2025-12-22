#pragma warning disable CA1416
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using Microsoft.ML.OnnxRuntime;
using Microsoft.Win32;

namespace MosaicCensorSystem.Helpers
{
    public static class GpuDetector
    {
        public enum GpuVendor { Unknown, Nvidia, Amd, Intel }

        public class GpuInfo
        {
            public string Name { get; set; } = "Unknown";
            public GpuVendor Vendor { get; set; } = GpuVendor.Unknown;
            public string DriverVersion { get; set; } = "Unknown";
        }

        public class ComponentStatus
        {
            public string Name { get; set; } = "";
            public bool IsInstalled { get; set; }
            public string InstalledVersion { get; set; } = "";
            public string RequiredVersion { get; set; } = "";
            public string DownloadUrl { get; set; } = "";
            public string InstallGuide { get; set; } = "";
        }

        public class DetectionResult
        {
            public List<GpuInfo> DetectedGpus { get; set; } = new();
            public ComponentStatus NvidiaDriver { get; set; } = new();
            public ComponentStatus CudaToolkit { get; set; } = new();
            public ComponentStatus CuDnn { get; set; } = new();
            public bool CudaRuntimeAvailable { get; set; }
            public bool DirectMLAvailable { get; set; }
            public bool CanUseCuda => NvidiaDriver.IsInstalled && CudaToolkit.IsInstalled && CuDnn.IsInstalled && CudaRuntimeAvailable;
            public bool CanUseDirectML => DirectMLAvailable;
        }

        private const string CUDA_REQUIRED_VERSION = "11.8";
        private const string CUDNN_REQUIRED_VERSION = "8.x";

        public static DetectionResult Detect()
        {
            var result = new DetectionResult();

            CollectGpuHardware(result);
            CheckNvidiaDriver(result);
            CheckCudaToolkit(result);
            CheckCuDnn(result);
            CheckOnnxProviders(result);

            return result;
        }

        private static void CollectGpuHardware(DetectionResult result)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var gpu = new GpuInfo
                    {
                        Name = obj["Name"]?.ToString() ?? "Unknown",
                        DriverVersion = obj["DriverVersion"]?.ToString() ?? "Unknown"
                    };

                    string nameLower = gpu.Name.ToLower();
                    if (nameLower.Contains("nvidia") || nameLower.Contains("geforce") || nameLower.Contains("rtx") || nameLower.Contains("gtx") || nameLower.Contains("quadro"))
                        gpu.Vendor = GpuVendor.Nvidia;
                    else if (nameLower.Contains("amd") || nameLower.Contains("radeon"))
                        gpu.Vendor = GpuVendor.Amd;
                    else if (nameLower.Contains("intel"))
                        gpu.Vendor = GpuVendor.Intel;

                    result.DetectedGpus.Add(gpu);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GPU 하드웨어 감지 실패: {ex.Message}");
            }
        }

        private static void CheckNvidiaDriver(DetectionResult result)
        {
            result.NvidiaDriver.Name = "NVIDIA 드라이버";
            result.NvidiaDriver.DownloadUrl = "https://www.nvidia.com/Download/index.aspx";
            result.NvidiaDriver.InstallGuide = "NVIDIA 공식 사이트에서 GPU 모델에 맞는 최신 드라이버를 다운로드하여 설치하세요.";

            var nvidiaGpu = result.DetectedGpus.FirstOrDefault(g => g.Vendor == GpuVendor.Nvidia);
            if (nvidiaGpu != null && nvidiaGpu.DriverVersion != "Unknown")
            {
                result.NvidiaDriver.IsInstalled = true;
                result.NvidiaDriver.InstalledVersion = nvidiaGpu.DriverVersion;
            }
        }

        private static void CheckCudaToolkit(DetectionResult result)
        {
            result.CudaToolkit.Name = "CUDA Toolkit";
            result.CudaToolkit.RequiredVersion = CUDA_REQUIRED_VERSION;
            result.CudaToolkit.DownloadUrl = "https://developer.nvidia.com/cuda-11-8-0-download-archive";
            result.CudaToolkit.InstallGuide = 
                "1. 위 링크에서 CUDA Toolkit 11.8 다운로드\n" +
                "2. 설치 프로그램 실행 (Express 설치 권장)\n" +
                "3. 설치 완료 후 PC 재시작";

            // 방법 1: 환경변수 확인
            string cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH") ?? "";
            if (!string.IsNullOrEmpty(cudaPath) && Directory.Exists(cudaPath))
            {
                result.CudaToolkit.IsInstalled = true;
                result.CudaToolkit.InstalledVersion = Path.GetFileName(cudaPath.TrimEnd('\\', '/'));
                return;
            }

            // 방법 2: 레지스트리 확인
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\NVIDIA Corporation\GPU Computing Toolkit\CUDA");
                if (key != null)
                {
                    var subkeys = key.GetSubKeyNames();
                    if (subkeys.Length > 0)
                    {
                        result.CudaToolkit.IsInstalled = true;
                        result.CudaToolkit.InstalledVersion = subkeys.OrderByDescending(v => v).First();
                        return;
                    }
                }
            }
            catch { }

            // 방법 3: 기본 설치 경로 확인
            string[] defaultPaths = {
                @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA",
                @"C:\Program Files\NVIDIA Corporation\CUDA"
            };

            foreach (var basePath in defaultPaths)
            {
                if (Directory.Exists(basePath))
                {
                    var versions = Directory.GetDirectories(basePath, "v*");
                    if (versions.Length > 0)
                    {
                        result.CudaToolkit.IsInstalled = true;
                        result.CudaToolkit.InstalledVersion = Path.GetFileName(versions.OrderByDescending(v => v).First());
                        return;
                    }
                }
            }
        }

        private static void CheckCuDnn(DetectionResult result)
        {
            result.CuDnn.Name = "cuDNN";
            result.CuDnn.RequiredVersion = CUDNN_REQUIRED_VERSION;
            result.CuDnn.DownloadUrl = "https://developer.nvidia.com/cudnn";
            result.CuDnn.InstallGuide =
                "1. NVIDIA Developer 계정 로그인 필요\n" +
                "2. cuDNN v8.x for CUDA 11.x 다운로드\n" +
                "3. 압축 해제 후 다음 파일들을 CUDA 폴더에 복사:\n" +
                "   • bin\\cudnn*.dll → C:\\Program Files\\NVIDIA GPU Computing Toolkit\\CUDA\\v11.8\\bin\\\n" +
                "   • include\\cudnn*.h → ...\\include\\\n" +
                "   • lib\\x64\\cudnn*.lib → ...\\lib\\x64\\";

            // CUDA 경로에서 cuDNN DLL 확인
            string cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH") ?? 
                              @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v11.8";
            
            string cudnnDllPath = Path.Combine(cudaPath, "bin", "cudnn64_8.dll");
            string cudnnDllPathAlt = Path.Combine(cudaPath, "bin", "cudnn_ops_infer64_8.dll");

            if (File.Exists(cudnnDllPath) || File.Exists(cudnnDllPathAlt))
            {
                result.CuDnn.IsInstalled = true;
                result.CuDnn.InstalledVersion = "8.x";
                return;
            }

            // 다른 버전 확인
            string binPath = Path.Combine(cudaPath, "bin");
            if (Directory.Exists(binPath))
            {
                var cudnnFiles = Directory.GetFiles(binPath, "cudnn*.dll");
                if (cudnnFiles.Length > 0)
                {
                    result.CuDnn.IsInstalled = true;
                    result.CuDnn.InstalledVersion = "감지됨";
                }
            }
        }

        private static void CheckOnnxProviders(DetectionResult result)
        {
            try
            {
                var providers = OrtEnv.Instance().GetAvailableProviders();
                result.CudaRuntimeAvailable = providers.Any(p => p.ToLower().Contains("cuda"));
                result.DirectMLAvailable = providers.Any(p => p.ToLower().Contains("dml"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ONNX Provider 확인 실패: {ex.Message}");
            }
        }
    }
}