using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using Microsoft.ML.OnnxRuntime;

namespace MosaicCensorSystem.Helpers
{
    public static class GpuDetector
    {
        public enum GpuVendor { Unknown, Nvidia, Amd, Intel }
        public enum RecommendedMode { CPU, DirectML, CUDA }

        public class GpuInfo
        {
            public string Name { get; set; } = "Unknown";
            public GpuVendor Vendor { get; set; } = GpuVendor.Unknown;
            public string DriverVersion { get; set; } = "Unknown";
            public long VramMB { get; set; } = 0;
        }

        public class DetectionResult
        {
            public List<GpuInfo> DetectedGpus { get; set; } = new();
            public RecommendedMode Recommended { get; set; } = RecommendedMode.CPU;
            public bool CudaAvailable { get; set; }
            public bool DirectMLAvailable { get; set; }
            public string FailureReason { get; set; } = "";
            public string DriverDownloadUrl { get; set; } = "";
        }

        public static DetectionResult Detect()
        {
            var result = new DetectionResult();
            
            CollectGpuHardware(result);
            CheckOnnxProviders(result);
            DetermineRecommendedMode(result);
            
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

                    var adapterRam = obj["AdapterRAM"];
                    if (adapterRam != null && long.TryParse(adapterRam.ToString(), out long ram))
                        gpu.VramMB = ram / (1024 * 1024);

                    string nameLower = gpu.Name.ToLower();
                    if (nameLower.Contains("nvidia") || nameLower.Contains("geforce") || nameLower.Contains("rtx") || nameLower.Contains("gtx"))
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

        private static void CheckOnnxProviders(DetectionResult result)
        {
            try
            {
                var providers = OrtEnv.Instance().GetAvailableProviders();
                result.CudaAvailable = providers.Any(p => p.Contains("CUDA"));
                result.DirectMLAvailable = providers.Any(p => p.Contains("Dml"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ONNX Provider 확인 실패: {ex.Message}");
            }
        }

        private static void DetermineRecommendedMode(DetectionResult result)
        {
            bool hasNvidia = result.DetectedGpus.Any(g => g.Vendor == GpuVendor.Nvidia);
            bool hasAmd = result.DetectedGpus.Any(g => g.Vendor == GpuVendor.Amd);
            bool hasIntel = result.DetectedGpus.Any(g => g.Vendor == GpuVendor.Intel);

            if (hasNvidia && result.CudaAvailable)
            {
                result.Recommended = RecommendedMode.CUDA;
            }
            else if (hasNvidia && !result.CudaAvailable)
            {
                result.Recommended = RecommendedMode.DirectML;
                result.FailureReason = "NVIDIA GPU가 감지되었지만 CUDA를 사용할 수 없습니다. 최신 드라이버를 설치하세요.";
                result.DriverDownloadUrl = "https://www.nvidia.com/Download/index.aspx";
            }
            else if ((hasAmd || hasIntel) && result.DirectMLAvailable)
            {
                result.Recommended = RecommendedMode.DirectML;
            }
            else if (hasAmd && !result.DirectMLAvailable)
            {
                result.Recommended = RecommendedMode.CPU;
                result.FailureReason = "AMD GPU가 감지되었지만 DirectML을 사용할 수 없습니다.";
                result.DriverDownloadUrl = "https://www.amd.com/support";
            }
            else
            {
                result.Recommended = RecommendedMode.CPU;
                if (result.DetectedGpus.Count == 0)
                    result.FailureReason = "GPU를 찾을 수 없습니다.";
            }
        }
    }
}