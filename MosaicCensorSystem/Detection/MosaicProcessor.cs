#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace MosaicCensorSystem.Detection
{
    // CensorType, Detection 등 관련 클래스는 그대로 유지합니다.
    public enum CensorType { Mosaic, Blur }

    public class Detection
    {
        public string ClassName { get; set; } = "";
        public float Confidence { get; set; }
        public int[] BBox { get; set; } = new int[4];
        public int Width => BBox[2] - BBox[0];
        public int Height => BBox[3] - BBox[1];
    }

    /// <summary>
    /// ONNX 모델 로딩과 객체 감지(추론) 역할에만 집중하도록 대폭 축소된 클래스
    /// </summary>
    public class MosaicProcessor : IDisposable
    {
        private InferenceSession model;
        private readonly float[] inputBuffer = new float[1 * 3 * 640 * 640]; // 입력 버퍼 재사용

        public float ConfThreshold { get; set; } = 0.3f; // 기본 신뢰도
        public List<string> Targets { get; private set; } = new List<string> { "얼굴", "가슴" };
        private CensorType currentCensorType = CensorType.Mosaic;
        private int strength = 20;

        private static readonly Dictionary<int, string> ClassNames = new()
        {
            {0, "얼굴"}, {1, "가슴"}, {2, "겨드랑이"}, {3, "보지"}, {4, "발"},
            {5, "몸 전체"}, {6, "자지"}, {7, "팬티"}, {8, "눈"}, {9, "손"},
            {10, "교미"}, {11, "신발"}, {12, "가슴_옷"}, {13, "여성"}
        };
        private static readonly Dictionary<string, float> NmsThresholds = new() { ["얼굴"] = 0.4f, ["가슴"] = 0.4f, ["보지"] = 0.4f };

        public MosaicProcessor(string modelPath)
        {
            LoadModel(modelPath);
        }

        private void LoadModel(string modelPath)
        {
            if (!File.Exists(modelPath))
            {
                Console.WriteLine($"❌ 모델 파일 없음: {modelPath}");
                return;
            }

            try
            {
                // 가장 기본적인 옵션으로 모델 로드 시도
                var sessionOptions = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
                };
                sessionOptions.AppendExecutionProvider_CPU(); // 명시적으로 CPU 사용
                model = new InferenceSession(modelPath, sessionOptions);
                Console.WriteLine($"✅ 모델 로드 성공: {modelPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 모델 로드 실패: {ex.Message}");
                model = null;
            }
        }

        public bool IsModelLoaded() => model != null;

        /// <summary>
        /// 객체 감지 핵심 메서드 (단순화된 버전)
        /// </summary>
        public List<Detection> DetectObjects(Mat frame)
        {
            if (!IsModelLoaded() || frame == null || frame.Empty())
            {
                return new List<Detection>();
            }

            try
            {
                // 1. 전처리
                var (scale, padX, padY) = Preprocess(frame, inputBuffer);

                // 2. 추론
                var inputTensor = new DenseTensor<float>(inputBuffer, new[] { 1, 3, 640, 640 });
                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", inputTensor) };
                using var results = model.Run(inputs);
                var outputTensor = results.First().AsTensor<float>();

                // 3. 후처리 및 NMS
                var detections = Postprocess(outputTensor, scale, padX, padY, frame.Width, frame.Height);
                return ApplyNMS(detections);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🚨 DetectObjects 오류: {ex.Message}");
                return new List<Detection>();
            }
        }

        /// <summary>
        /// 이미지 전처리
        /// </summary>
        private (float scale, int padX, int padY) Preprocess(Mat frame, float[] buffer)
        {
            const int TargetSize = 640;
            float scale = Math.Min((float)TargetSize / frame.Width, (float)TargetSize / frame.Height);
            int newWidth = (int)(frame.Width * scale);
            int newHeight = (int)(frame.Height * scale);
            int padX = (TargetSize - newWidth) / 2;
            int padY = (TargetSize - newHeight) / 2;

            using var resized = new Mat();
            Cv2.Resize(frame, resized, new Size(newWidth, newHeight), interpolation: InterpolationFlags.Linear);

            using var padded = new Mat();
            Cv2.CopyMakeBorder(resized, padded, padY, TargetSize - newHeight - padY, padX, TargetSize - newWidth - padX, BorderTypes.Constant, new Scalar(114, 114, 114));
            
            padded.ConvertTo(padded, MatType.CV_32F, 1.0 / 255.0);
            Cv2.Split(padded, out Mat[] channels);
            
            // NCHW 형식으로 버퍼에 데이터 복사
            Marshal.Copy(channels[2].Data, buffer, 0, TargetSize * TargetSize); // R
            Marshal.Copy(channels[1].Data, buffer, TargetSize * TargetSize, TargetSize * TargetSize); // G
            Marshal.Copy(channels[0].Data, buffer, 2 * TargetSize * TargetSize, TargetSize * TargetSize); // B

            foreach (var ch in channels) ch.Dispose();
            return (scale, padX, padY);
        }

        /// <summary>
        /// 추론 결과 후처리
        /// </summary>
        private List<Detection> Postprocess(Tensor<float> output, float scale, int padX, int padY, int originalWidth, int originalHeight)
        {
            var detections = new List<Detection>();
            for (int i = 0; i < 8400; i++)
            {
                float maxScore = 0;
                int maxClassId = -1;
                for (int c = 0; c < 14; c++)
                {
                    float score = output[0, 4 + c, i];
                    if (score > maxScore)
                    {
                        maxScore = score;
                        maxClassId = c;
                    }
                }

                if (maxScore <= ConfThreshold) continue;

                string className = ClassNames.GetValueOrDefault(maxClassId);
                if (className == null || !Targets.Contains(className)) continue;
                
                float cx = output[0, 0, i];
                float cy = output[0, 1, i];
                float w = output[0, 2, i];
                float h = output[0, 3, i];

                int x1 = (int)((cx - w / 2 - padX) / scale);
                int y1 = (int)((cy - h / 2 - padY) / scale);
                int x2 = (int)((cx + w / 2 - padX) / scale);
                int y2 = (int)((cy + h / 2 - padY) / scale);

                detections.Add(new Detection
                {
                    ClassName = className,
                    Confidence = maxScore,
                    BBox = new[] { Math.Max(0, x1), Math.Max(0, y1), Math.Min(originalWidth, x2), Math.Min(originalHeight, y2) }
                });
            }
            return detections;
        }

        /// <summary>
        /// NMS (Non-Maximum Suppression) 적용
        /// </summary>
        private List<Detection> ApplyNMS(List<Detection> detections)
        {
            var finalDetections = new List<Detection>();
            foreach (var group in detections.GroupBy(d => d.ClassName))
            {
                var orderedGroup = group.OrderByDescending(d => d.Confidence).ToList();
                float nmsThreshold = NmsThresholds.GetValueOrDefault(group.Key, 0.45f);

                while (orderedGroup.Any())
                {
                    var best = orderedGroup.First();
                    finalDetections.Add(best);
                    orderedGroup = orderedGroup.Where(d => CalculateIoU(best.BBox, d.BBox) < nmsThreshold).ToList();
                }
            }
            return finalDetections;
        }

        private float CalculateIoU(int[] boxA, int[] boxB)
        {
            int xA = Math.Max(boxA[0], boxB[0]);
            int yA = Math.Max(boxA[1], boxB[1]);
            int xB = Math.Min(boxA[2], boxB[2]);
            int yB = Math.Min(boxA[3], boxB[3]);
            float interArea = Math.Max(0, xB - xA) * Math.Max(0, yB - yA);
            float boxAArea = (boxA[2] - boxA[0]) * (boxA[3] - boxA[1]);
            float boxBArea = (boxB[2] - boxB[0]) * (boxB[3] - boxB[1]);
            return interArea / (boxAArea + boxBArea - interArea);
        }
        
        /// <summary>
        /// 검열 효과 적용 (단순화된 버전)
        /// </summary>
        public void ApplySingleCensorOptimized(Mat frame, Detection detection)
        {
            if (detection.Width <= 0 || detection.Height <= 0) return;
            Rect roi = new Rect(detection.BBox[0], detection.BBox[1], detection.Width, detection.Height);
            using Mat region = new Mat(frame, roi);
            
            if (currentCensorType == CensorType.Mosaic)
            {
                int w = region.Width, h = region.Height;
                int smallW = Math.Max(1, w / strength), smallH = Math.Max(1, h / strength);
                using Mat small = new Mat();
                Cv2.Resize(region, small, new Size(smallW, smallH), interpolation: InterpolationFlags.Linear);
                Cv2.Resize(small, region, new Size(w, h), interpolation: InterpolationFlags.Nearest);
            }
            else // Blur
            {
                int kernelSize = Math.Max(3, strength + 1);
                if (kernelSize % 2 == 0) kernelSize++;
                Cv2.GaussianBlur(region, region, new Size(kernelSize, kernelSize), 0);
            }
        }

        // --- 설정 업데이트 메서드 ---
        public void SetTargets(List<string> targets) => Targets = targets ?? new List<string>();
        public void SetStrength(int strength) => this.strength = Math.Max(5, Math.Min(50, strength));
        public void SetCensorType(CensorType censorType) => this.currentCensorType = censorType;

        public void Dispose()
        {
            model?.Dispose();
            model = null;
        }
    }
}