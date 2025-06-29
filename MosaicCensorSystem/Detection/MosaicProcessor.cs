using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace MosaicCensorSystem.Detection
{
    /// <summary>
    /// 객체 감지 결과를 나타내는 클래스
    /// </summary>
    public class Detection
    {
        public string ClassName { get; set; } = "";
        public float Confidence { get; set; }
        public int[] BBox { get; set; } = new int[4]; // [x1, y1, x2, y2]
        public int ClassId { get; set; }
    }

    /// <summary>
    /// 성능 통계를 나타내는 클래스
    /// </summary>
    public class PerformanceStats
    {
        public double AvgDetectionTime { get; set; }
        public double Fps { get; set; }
        public int LastDetectionsCount { get; set; }
    }

    /// <summary>
    /// 모자이크 처리 인터페이스
    /// </summary>
    public interface IProcessor
    {
        void SetTargets(List<string> targets);
        void SetStrength(int strength);
        List<Detection> DetectObjects(Mat frame);
        (Mat processedFrame, List<Detection> detections) DetectObjectsDetailed(Mat frame);
        Mat ApplyMosaic(Mat image, int? strength = null);
        Mat? CreateMosaicForRegion(Mat frame, int x1, int y1, int x2, int y2, int? strength = null);
        PerformanceStats GetPerformanceStats();
        void UpdateConfig(Dictionary<string, object> kwargs);
        bool IsModelLoaded();
        List<string> GetAvailableClasses();
        void ResetStats();
        float ConfThreshold { get; set; }
        List<string> Targets { get; }
        int Strength { get; }
    }

    /// <summary>
    /// CUDA 자동감지 및 최적화된 모자이크 프로세서
    /// </summary>
    public class MosaicProcessor : IProcessor, IDisposable
    {
        private readonly Dictionary<string, object> config;
        private InferenceSession? model;
        private readonly string modelPath;
        private string accelerationMode = "Unknown";

        // 설정값들
        public float ConfThreshold { get; set; }
        public List<string> Targets { get; private set; }
        public int Strength { get; private set; }

        // 클래스 이름 매핑 (YAML과 정확히 일치)
        private readonly List<string> classNames = new List<string>
        {
            "얼굴", "가슴", "겨드랑이", "보지", "발", "몸 전체",
            "자지", "팬티", "눈", "손", "교미", "신발",
            "가슴_옷", "보지_옷", "여성"
        };

        // 성능 통계
        private readonly List<double> detectionTimes = new List<double>();
        private List<Detection> lastDetections = new List<Detection>();
        private readonly object statsLock = new object();

        public MosaicProcessor(string? modelPath = null, Dictionary<string, object>? config = null)
        {
            Console.WriteLine("🔍 CUDA 자동감지 MosaicProcessor 초기화");
            this.config = config ?? new Dictionary<string, object>();
            
            // 모델 경로 설정
            this.modelPath = modelPath ?? "Resources/best.onnx";
            if (!System.IO.File.Exists(this.modelPath))
            {
                this.modelPath = modelPath ?? Program.ONNX_MODEL_PATH;
            }

            // CUDA 우선, CPU 자동 폴백 모델 로드
            LoadModelWithAutoFallback();

            // 설정값들 초기화
            ConfThreshold = 0.35f;
            Targets = new List<string> { "얼굴" };
            Strength = 15;

            Console.WriteLine($"🎯 기본 타겟: {string.Join(", ", Targets)}");
            Console.WriteLine($"⚙️ 기본 설정: 강도={Strength}, 신뢰도={ConfThreshold}");
            Console.WriteLine($"🚀 가속 모드: {accelerationMode}");
        }

        private void LoadModelWithAutoFallback()
        {
            Console.WriteLine($"🤖 YOLO 모델 로딩 시작: {this.modelPath}");
            
            // 1순위: CUDA 시도
            if (TryLoadCudaModel())
            {
                accelerationMode = "CUDA GPU";
                Console.WriteLine("✅ CUDA GPU 가속 모델 로드 성공! (최고 성능)");
                return;
            }
            
            // 2순위: DirectML 시도 (Windows GPU 가속)
            if (TryLoadDirectMLModel())
            {
                accelerationMode = "DirectML GPU";
                Console.WriteLine("✅ DirectML GPU 가속 모델 로드 성공! (고성능)");
                return;
            }
            
            // 3순위: 최적화된 CPU
            if (TryLoadOptimizedCpuModel())
            {
                accelerationMode = "Optimized CPU";
                Console.WriteLine("✅ 최적화된 CPU 모델 로드 성공 (일반 성능)");
                return;
            }
            
            // 4순위: 기본 CPU
            if (TryLoadBasicCpuModel())
            {
                accelerationMode = "Basic CPU";
                Console.WriteLine("⚠️ 기본 CPU 모델 로드 성공 (저성능)");
                return;
            }
            
            // 모든 시도 실패
            accelerationMode = "Failed";
            Console.WriteLine("❌ 모든 모델 로딩 시도 실패");
            model = null;
        }

        private bool TryLoadCudaModel()
        {
            try
            {
                Console.WriteLine("🎯 CUDA GPU 가속 시도 중...");
                
                var sessionOptions = new SessionOptions();
                
                // CUDA 설정 (최고 성능)
                sessionOptions.AppendExecutionProvider_CUDA(0);
                sessionOptions.EnableCpuMemArena = true;
                sessionOptions.EnableMemoryPattern = true;
                sessionOptions.ExecutionMode = ExecutionMode.ORT_PARALLEL;
                sessionOptions.InterOpNumThreads = Environment.ProcessorCount;
                sessionOptions.IntraOpNumThreads = Environment.ProcessorCount;
                sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                
                // CUDA 메모리 최적화 (AddConfigEntry 제거)
                // sessionOptions.AddConfigEntry("memory.enable_memory_arena_shrinkage", "");
                // sessionOptions.AddConfigEntry("session.disable_cpu_ep_fallback", "1");
                
                model = new InferenceSession(this.modelPath, sessionOptions);
                
                // CUDA 테스트
                TestModelInference();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"⚠️ CUDA 로딩 실패: {e.Message}");
                model?.Dispose();
                model = null;
                return false;
            }
        }

        private bool TryLoadDirectMLModel()
        {
            try
            {
                Console.WriteLine("🎯 DirectML GPU 가속 시도 중...");
                
                var sessionOptions = new SessionOptions();
                
                // DirectML 설정 (Windows GPU 가속)
                sessionOptions.AppendExecutionProvider_DML(0);
                sessionOptions.EnableCpuMemArena = true;
                sessionOptions.EnableMemoryPattern = true;
                sessionOptions.ExecutionMode = ExecutionMode.ORT_PARALLEL;
                sessionOptions.InterOpNumThreads = Environment.ProcessorCount;
                sessionOptions.IntraOpNumThreads = Environment.ProcessorCount;
                sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                
                model = new InferenceSession(this.modelPath, sessionOptions);
                
                // DirectML 테스트
                TestModelInference();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"⚠️ DirectML 로딩 실패: {e.Message}");
                model?.Dispose();
                model = null;
                return false;
            }
        }

        private bool TryLoadOptimizedCpuModel()
        {
            try
            {
                Console.WriteLine("🎯 최적화된 CPU 시도 중...");
                
                var sessionOptions = new SessionOptions
                {
                    EnableCpuMemArena = true,
                    EnableMemoryPattern = true,
                    ExecutionMode = ExecutionMode.ORT_PARALLEL,
                    InterOpNumThreads = Environment.ProcessorCount,
                    IntraOpNumThreads = Environment.ProcessorCount,
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
                };
                
                model = new InferenceSession(this.modelPath, sessionOptions);
                
                // 최적화된 CPU 테스트
                TestModelInference();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"⚠️ 최적화된 CPU 로딩 실패: {e.Message}");
                model?.Dispose();
                model = null;
                return false;
            }
        }

        private bool TryLoadBasicCpuModel()
        {
            try
            {
                Console.WriteLine("🎯 기본 CPU 시도 중...");
                
                var sessionOptions = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_BASIC
                };
                
                model = new InferenceSession(this.modelPath, sessionOptions);
                
                // 기본 CPU 테스트
                TestModelInference();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"⚠️ 기본 CPU 로딩 실패: {e.Message}");
                model?.Dispose();
                model = null;
                return false;
            }
        }

        private void TestModelInference()
        {
            if (model == null) return;
            
            // 더미 입력으로 모델 테스트
            var testInput = new DenseTensor<float>(new[] { 1, 3, 640, 640 });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("images", testInput)
            };
            
            using var results = model.Run(inputs);
            var output = results.FirstOrDefault()?.AsEnumerable<float>().ToArray();
            
            if (output == null || output.Length == 0)
            {
                throw new Exception("모델 출력이 비어있음");
            }
        }

        public void SetTargets(List<string> targets)
        {
            Targets = targets ?? new List<string>();
            Console.WriteLine($"🎯 타겟 변경: {string.Join(", ", Targets)}");
        }

        public void SetStrength(int strength)
        {
            Strength = Math.Max(1, Math.Min(50, strength));
            Console.WriteLine($"💪 강도 변경: {Strength}");
        }

        public List<Detection> DetectObjects(Mat frame)
        {
            if (model == null || frame == null || frame.Empty())
                return new List<Detection>();

            try
            {
                var startTime = DateTime.Now;

                // 전처리
                var inputTensor = PreprocessFrame(frame);
                if (inputTensor == null)
                    return new List<Detection>();

                // YOLO 추론
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("images", inputTensor)
                };

                using var results = model.Run(inputs);
                var output = results.FirstOrDefault()?.AsEnumerable<float>().ToArray();

                if (output == null)
                    return new List<Detection>();

                // 후처리
                var detections = PostprocessOutput(output, frame.Width, frame.Height);

                // 성능 통계 업데이트
                var detectionTime = (DateTime.Now - startTime).TotalSeconds;
                lock (statsLock)
                {
                    detectionTimes.Add(detectionTime);
                    if (detectionTimes.Count > 100)
                    {
                        detectionTimes.RemoveRange(0, detectionTimes.Count - 50);
                    }
                    lastDetections = detections;
                }

                return detections;
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ 객체 감지 오류: {e.Message}");
                return new List<Detection>();
            }
        }

        private DenseTensor<float>? PreprocessFrame(Mat frame)
        {
            try
            {
                // 640x640으로 리사이즈
                using var resized = new Mat();
                Cv2.Resize(frame, resized, new OpenCvSharp.Size(640, 640));

                // BGR to RGB 변환
                using var rgb = new Mat();
                Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

                // 정규화 및 텐서 변환
                var tensor = new DenseTensor<float>(new[] { 1, 3, 640, 640 });

                // OpenCV Mat을 직접 사용한 안전한 픽셀 접근
                var indexer = rgb.GetGenericIndexer<Vec3b>();
                
                for (int h = 0; h < 640; h++)
                {
                    for (int w = 0; w < 640; w++)
                    {
                        var pixel = indexer[h, w];
                        // RGB 순서로 저장 (OpenCV는 BGR)
                        tensor[0, 0, h, w] = pixel.Item2 / 255.0f; // R
                        tensor[0, 1, h, w] = pixel.Item1 / 255.0f; // G  
                        tensor[0, 2, h, w] = pixel.Item0 / 255.0f; // B
                    }
                }

                return tensor;
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ 전처리 오류: {e.Message}");
                return null;
            }
        }

        private List<Detection> PostprocessOutput(float[] output, int originalWidth, int originalHeight)
        {
            var detections = new List<Detection>();

            try
            {
                const int numFeatures = 19;
                const int numDetections = 8400;
                const int numClasses = 14;
                
                if (output.Length != numFeatures * numDetections)
                {
                    Console.WriteLine($"❌ 예상치 못한 출력 크기: {output.Length}, 예상: {numFeatures * numDetections}");
                    return detections;
                }

                for (int detIndex = 0; detIndex < numDetections; detIndex++)
                {
                    // 좌표 추출
                    float centerX = output[0 * numDetections + detIndex];
                    float centerY = output[1 * numDetections + detIndex];
                    float width = output[2 * numDetections + detIndex];
                    float height = output[3 * numDetections + detIndex];
                    float objectConfidence = output[4 * numDetections + detIndex];

                    // 클래스별 확률 추출
                    float maxClassProb = 0;
                    int maxClassIndex = 0;
                    
                    for (int classIndex = 0; classIndex < numClasses; classIndex++)
                    {
                        float classProb = output[(5 + classIndex) * numDetections + detIndex];
                        if (classProb > maxClassProb)
                        {
                            maxClassProb = classProb;
                            maxClassIndex = classIndex;
                        }
                    }

                    // 신뢰도 체크
                    if (maxClassProb > ConfThreshold && maxClassIndex < classNames.Count)
                    {
                        string className = classNames[maxClassIndex];

                        // 좌표 변환
                        float scaleX = originalWidth / 640.0f;
                        float scaleY = originalHeight / 640.0f;

                        int x1 = (int)((centerX - width / 2) * scaleX);
                        int y1 = (int)((centerY - height / 2) * scaleY);
                        int x2 = (int)((centerX + width / 2) * scaleX);
                        int y2 = (int)((centerY + height / 2) * scaleY);

                        // 경계 확인
                        x1 = Math.Max(0, Math.Min(x1, originalWidth - 1));
                        y1 = Math.Max(0, Math.Min(y1, originalHeight - 1));
                        x2 = Math.Max(0, Math.Min(x2, originalWidth - 1));
                        y2 = Math.Max(0, Math.Min(y2, originalHeight - 1));

                        int boxWidth = x2 - x1;
                        int boxHeight = y2 - y1;
                        
                        if (boxWidth > 10 && boxHeight > 10)
                        {
                            var detection = new Detection
                            {
                                ClassName = className,
                                Confidence = maxClassProb,
                                BBox = new int[] { x1, y1, x2, y2 },
                                ClassId = maxClassIndex
                            };
                            detections.Add(detection);
                        }
                    }
                }

                // NMS 적용
                if (detections.Count > 0)
                {
                    detections = ApplyNMS(detections);
                }

                return detections;
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ 후처리 오류: {e.Message}");
                return new List<Detection>();
            }
        }

        private List<Detection> ApplyNMS(List<Detection> detections)
        {
            if (detections.Count == 0) return detections;

            var nmsThresholds = new Dictionary<string, float>
            {
                ["얼굴"] = 0.3f, ["가슴"] = 0.4f, ["보지"] = 0.3f, ["자지"] = 0.3f,
                ["팬티"] = 0.4f, ["눈"] = 0.2f, ["손"] = 0.5f, ["발"] = 0.5f,
                ["몸 전체"] = 0.6f, ["여성"] = 0.7f, ["겨드랑이"] = 0.4f,
                ["신발"] = 0.5f, ["가슴_옷"] = 0.4f, ["보지_옷"] = 0.4f, ["교미"] = 0.3f
            };

            detections = detections.OrderByDescending(d => d.Confidence).ToList();
            var keep = new List<Detection>();

            while (detections.Count > 0)
            {
                var current = detections[0];
                keep.Add(current);
                detections.RemoveAt(0);

                float nmsThreshold = nmsThresholds.GetValueOrDefault(current.ClassName, 0.4f);

                for (int i = detections.Count - 1; i >= 0; i--)
                {
                    if (detections[i].ClassName == current.ClassName)
                    {
                        float iou = CalculateIoU(current.BBox, detections[i].BBox);
                        if (iou > nmsThreshold)
                        {
                            detections.RemoveAt(i);
                        }
                    }
                }
            }

            return keep;
        }
        
        private float CalculateIoU(int[] box1, int[] box2)
        {
            int x1 = Math.Max(box1[0], box2[0]);
            int y1 = Math.Max(box1[1], box2[1]);
            int x2 = Math.Min(box1[2], box2[2]);
            int y2 = Math.Min(box1[3], box2[3]);

            if (x2 <= x1 || y2 <= y1) return 0;

            float intersection = (x2 - x1) * (y2 - y1);
            float area1 = (box1[2] - box1[0]) * (box1[3] - box1[1]);
            float area2 = (box2[2] - box2[0]) * (box2[3] - box2[1]);
            float union = area1 + area2 - intersection;

            return union > 0 ? intersection / union : 0;
        }

        public (Mat processedFrame, List<Detection> detections) DetectObjectsDetailed(Mat frame)
        {
            var detections = DetectObjects(frame);
            var processedFrame = frame.Clone();

            foreach (var detection in detections)
            {
                if (Targets.Contains(detection.ClassName))
                {
                    int x1 = detection.BBox[0], y1 = detection.BBox[1];
                    int x2 = detection.BBox[2], y2 = detection.BBox[3];

                    if (x2 > x1 && y2 > y1 && x1 >= 0 && y1 >= 0 && x2 <= frame.Width && y2 <= frame.Height)
                    {
                        using var region = new Mat(processedFrame, new Rect(x1, y1, x2 - x1, y2 - y1));
                        if (!region.Empty())
                        {
                            using var mosaicRegion = ApplyMosaic(region, Strength);
                            mosaicRegion.CopyTo(region);
                        }
                    }
                }
            }

            return (processedFrame, detections);
        }

        public Mat ApplyMosaic(Mat image, int? strength = null)
        {
            int mosaicStrength = strength ?? Strength;
            
            if (image == null || image.Empty())
                return image?.Clone() ?? new Mat();

            try
            {
                int h = image.Height;
                int w = image.Width;

                int smallH = Math.Max(1, h / mosaicStrength);
                int smallW = Math.Max(1, w / mosaicStrength);

                using var small = new Mat();
                Cv2.Resize(image, small, new OpenCvSharp.Size(smallW, smallH), interpolation: InterpolationFlags.Linear);
                
                var mosaic = new Mat();
                Cv2.Resize(small, mosaic, new OpenCvSharp.Size(w, h), interpolation: InterpolationFlags.Nearest);

                return mosaic;
            }
            catch (Exception e)
            {
                Console.WriteLine($"⚠️ 모자이크 적용 오류: {e.Message}");
                return image.Clone();
            }
        }

        public Mat? CreateMosaicForRegion(Mat frame, int x1, int y1, int x2, int y2, int? strength = null)
        {
            try
            {
                if (x2 <= x1 || y2 <= y1 || x1 < 0 || y1 < 0 || x2 > frame.Width || y2 > frame.Height)
                    return null;

                using var region = new Mat(frame, new Rect(x1, y1, x2 - x1, y2 - y1));
                if (region.Empty())
                    return null;

                return ApplyMosaic(region, strength);
            }
            catch (Exception e)
            {
                Console.WriteLine($"⚠️ 영역 모자이크 생성 오류: {e.Message}");
                return null;
            }
        }

        public PerformanceStats GetPerformanceStats()
        {
            lock (statsLock)
            {
                if (detectionTimes.Count == 0)
                {
                    return new PerformanceStats
                    {
                        AvgDetectionTime = 0,
                        Fps = 0,
                        LastDetectionsCount = 0
                    };
                }

                double avgTime = detectionTimes.Average();
                double fps = avgTime > 0 ? 1.0 / avgTime : 0;

                return new PerformanceStats
                {
                    AvgDetectionTime = avgTime,
                    Fps = fps,
                    LastDetectionsCount = lastDetections.Count
                };
            }
        }

        public void UpdateConfig(Dictionary<string, object> kwargs)
        {
            foreach (var kvp in kwargs)
            {
                switch (kvp.Key)
                {
                    case "conf_threshold":
                        ConfThreshold = Math.Max(0.01f, Math.Min(0.99f, Convert.ToSingle(kvp.Value)));
                        break;
                    case "targets":
                        if (kvp.Value is List<string> targets)
                            Targets = targets;
                        break;
                    case "strength":
                        Strength = Math.Max(1, Math.Min(50, Convert.ToInt32(kvp.Value)));
                        break;
                }
            }

            Console.WriteLine($"⚙️ 설정 업데이트: {string.Join(", ", kwargs.Keys)}");
        }

        public bool IsModelLoaded()
        {
            return model != null;
        }

        public List<string> GetAvailableClasses()
        {
            return new List<string>(classNames);
        }

        public void ResetStats()
        {
            lock (statsLock)
            {
                detectionTimes.Clear();
                lastDetections.Clear();
            }
            Console.WriteLine("📊 성능 통계 초기화됨");
        }

        public string GetAccelerationMode()
        {
            return accelerationMode;
        }

        public void Dispose()
        {
            model?.Dispose();
            Console.WriteLine($"🧹 {accelerationMode} MosaicProcessor 리소스 정리됨");
        }
    }
}