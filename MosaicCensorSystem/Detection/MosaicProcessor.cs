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
    public enum CensorType { Mosaic, Blur }

    public class Detection
    {
        public int TrackId { get; set; }
        public string ClassName { get; set; } = "";
        public float Confidence { get; set; }
        public int[] BBox { get; set; } = new int[4];
        public int Width => BBox[2] - BBox[0];
        public int Height => BBox[3] - BBox[1];
    }

    public class MosaicProcessor : IDisposable
    {
        private InferenceSession model;
        private readonly float[] inputBuffer = new float[1 * 3 * 640 * 640];
        private readonly SortTracker tracker = new SortTracker();

        public float ConfThreshold { get; set; } = 0.3f;
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
            if (!File.Exists(modelPath)) { Console.WriteLine($"❌ 모델 파일 없음: {modelPath}"); return; }
            try
            {
                var sessionOptions = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
                sessionOptions.AppendExecutionProvider_CPU();
                model = new InferenceSession(modelPath, sessionOptions);
                Console.WriteLine($"✅ 모델 로드 성공: {modelPath}");
            }
            catch (Exception ex) { Console.WriteLine($"❌ 모델 로드 실패: {ex.Message}"); model = null; }
        }

        public bool IsModelLoaded() => model != null;

        public List<Detection> DetectObjects(Mat frame)
        {
            if (!IsModelLoaded() || frame == null || frame.Empty()) return new List<Detection>();
            try
            {
                var (scale, padX, padY) = Preprocess(frame, inputBuffer);
                var inputTensor = new DenseTensor<float>(inputBuffer, new[] { 1, 3, 640, 640 });
                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", inputTensor) };
                using var results = model.Run(inputs);
                var outputTensor = results.First().AsTensor<float>();
                var detections = Postprocess(outputTensor, scale, padX, padY, frame.Width, frame.Height);
                var nmsDetections = ApplyNMS(detections);

                var trackBoxes = nmsDetections.Select(d => new Rect2d(d.BBox[0], d.BBox[1], d.Width, d.Height)).ToList();
                var trackedResults = tracker.Update(trackBoxes);

                var finalDetections = new List<Detection>();
                
                // --- ★★★ 여기가 수정된 부분입니다 ★★★ ---
                var remainingDetections = new List<Detection>(nmsDetections);

                foreach (var track in trackedResults)
                {
                    // 현재 트랙과 가장 가까운 감지 결과 찾기
                    var bestMatch = remainingDetections
                        .Select(det => new { Detection = det, Distance = new Rect2d(det.BBox[0], det.BBox[1], det.Width, det.Height).DistanceTo(track.box) })
                        .OrderBy(x => x.Distance)
                        .FirstOrDefault();

                    // IoU를 추가로 확인하여 더 정확하게 매칭 (선택적)
                    if (bestMatch != null && bestMatch.Distance < 50) // 일정 거리 내에 있을 때만 매칭
                    {
                        bestMatch.Detection.TrackId = track.id;
                        finalDetections.Add(bestMatch.Detection);
                        remainingDetections.Remove(bestMatch.Detection); // 매칭된 감지 결과는 목록에서 제거
                    }
                }
                // --- ★★★ 수정 끝 ★★★ ---

                return finalDetections;
            }
            catch (Exception ex) { Console.WriteLine($"🚨 DetectObjects 오류: {ex.Message}"); return new List<Detection>(); }
        }

        private (float scale, int padX, int padY) Preprocess(Mat frame, float[] buffer)
        {
            const int TargetSize = 640;
            float scale = Math.Min((float)TargetSize / frame.Width, (float)TargetSize / frame.Height);
            int newWidth = (int)(frame.Width * scale);
            int newHeight = (int)(frame.Height * scale);
            int padX = (TargetSize - newWidth) / 2;
            int padY = (TargetSize - newHeight) / 2;

            using var resized = new Mat();
            Cv2.Resize(frame, resized, new OpenCvSharp.Size(newWidth, newHeight), interpolation: InterpolationFlags.Linear);

            using var padded = new Mat();
            Cv2.CopyMakeBorder(resized, padded, padY, TargetSize - newHeight - padY, padX, TargetSize - newWidth - padX, BorderTypes.Constant, new Scalar(114, 114, 114));
            
            padded.ConvertTo(padded, MatType.CV_32F, 1.0 / 255.0);
            Cv2.Split(padded, out Mat[] channels);
            
            Marshal.Copy(channels[2].Data, buffer, 0, TargetSize * TargetSize);
            Marshal.Copy(channels[1].Data, buffer, TargetSize * TargetSize, TargetSize * TargetSize);
            Marshal.Copy(channels[0].Data, buffer, 2 * TargetSize * TargetSize, TargetSize * TargetSize);

            foreach (var ch in channels) ch.Dispose();
            return (scale, padX, padY);
        }

        private List<Detection> Postprocess(Tensor<float> output, float scale, int padX, int padY, int originalWidth, int originalHeight)
        {
            var detections = new List<Detection>();
            for (int i = 0; i < 8400; i++)
            {
                float maxScore = 0;
                int maxClassId = -1;
                for (int c = 0; c < 14; c++) { float score = output[0, 4 + c, i]; if (score > maxScore) { maxScore = score; maxClassId = c; } }

                if (maxScore <= ConfThreshold) continue;

                string className = ClassNames.GetValueOrDefault(maxClassId);
                if (className == null || !Targets.Contains(className)) continue;
                
                float cx = output[0, 0, i]; float cy = output[0, 1, i]; float w = output[0, 2, i]; float h = output[0, 3, i];
                int x1 = (int)((cx - w / 2 - padX) / scale); int y1 = (int)((cy - h / 2 - padY) / scale);
                int x2 = (int)((cx + w / 2 - padX) / scale); int y2 = (int)((cy + h / 2 - padY) / scale);

                detections.Add(new Detection { ClassName = className, Confidence = maxScore, BBox = new[] { Math.Max(0, x1), Math.Max(0, y1), Math.Min(originalWidth, x2), Math.Min(originalHeight, y2) } });
            }
            return detections;
        }

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
            float unionArea = boxAArea + boxBArea - interArea;
            return unionArea > 0 ? interArea / unionArea : 0;
        }
        
        public void ApplySingleCensorOptimized(Mat frame, Detection detection)
        {
            if (detection.Width <= 0 || detection.Height <= 0) return;
            Rect roi = new Rect(detection.BBox[0], detection.BBox[1], detection.Width, detection.Height);
            using Mat region = new Mat(frame, roi);
            if (currentCensorType == CensorType.Mosaic)
            {
                int w = region.Width, h = region.Height; int smallW = Math.Max(1, w / strength), smallH = Math.Max(1, h / strength);
                using Mat small = new Mat();
                Cv2.Resize(region, small, new OpenCvSharp.Size(smallW, smallH), interpolation: InterpolationFlags.Linear);
                Cv2.Resize(small, region, new OpenCvSharp.Size(w, h), interpolation: InterpolationFlags.Nearest);
            }
            else { int kernelSize = Math.Max(3, strength + 1); if (kernelSize % 2 == 0) kernelSize++; Cv2.GaussianBlur(region, region, new OpenCvSharp.Size(kernelSize, kernelSize), 0); }
        }

        public void SetTargets(List<string> targets) => Targets = targets ?? new List<string>();
        public void SetStrength(int strength) => this.strength = Math.Max(5, Math.Min(50, strength));
        public void SetCensorType(CensorType censorType) => this.currentCensorType = censorType;
        public void Dispose() { model?.Dispose(); model = null; }
    }
    
    public static class Rect2dExtensions
    {
        public static double DistanceTo(this Rect2d r1, Rect2d r2)
        {
            double dx = (r1.X + r1.Width / 2) - (r2.X + r2.Width / 2);
            double dy = (r1.Y + r1.Height / 2) - (r2.Y + r2.Height / 2);
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}