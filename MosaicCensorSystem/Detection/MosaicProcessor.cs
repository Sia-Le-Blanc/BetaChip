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
    public enum CensorType { Mosaic, Blur, BlackBox }

    public class Detection
    {
        public int TrackId { get; set; }
        public string ClassName { get; set; } = "";
        public float Confidence { get; set; }
        public int[] BBox { get; set; } = new int[4];
        public int Width => BBox[2] - BBox[0];
        public int Height => BBox[3] - BBox[1];
        public float Angle { get; set; } = 0f;
        // OBB 전용: 스케일·패딩 보정된 실제 중심 좌표 및 크기
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float ObbWidth { get; set; }
        public float ObbHeight { get; set; }
    }

    public class MosaicProcessor : IDisposable
    {
        private InferenceSession model;
        private readonly object _lockObj = new object();
        private bool isObbMode = false;
        private readonly float[] inputBuffer = new float[1 * 3 * 640 * 640];
        private readonly SortTracker tracker = new SortTracker();

        private Mat _resizedMat = new Mat();
        private Mat _paddedMat = new Mat();
        private Mat[] _channels = new Mat[3];

        public Action<string> LogCallback { get; set; }
        public float ConfThreshold { get; set; } = 0.3f;
        public List<string> Targets { get; private set; } = new List<string> { "얼굴", "가슴" };
        private CensorType currentCensorType = CensorType.Mosaic;
        private int strength = 20;

        public string CurrentExecutionProvider { get; private set; } = "CPU";

        private static readonly Dictionary<int, string> ClassNames = new()
        {
            {0, "얼굴"}, {1, "가슴"}, {2, "겨드랑이"}, {3, "보지"}, {4, "발"},
            {5, "몸 전체"}, {6, "자지"}, {7, "팬티"}, {8, "눈"}, {9, "손"},
            {10, "교미"}, {11, "신발"}, {12, "가슴_옷"}, {13, "여성"}
        };
        private static readonly Dictionary<int, string> ClassNamesObb = new()
        {
            {0, "얼굴"}, {1, "얼굴"}, {2, "눈"}, {3, "가슴"},
            {4, "가슴"}, {5, "가슴_옷"}, {6, "겨드랑이"}, {7, "배꼽"},
            {8, "자지"}, {9, "보지"}, {10, "엉덩이"}, {11, "팬티"},
            {12, "팬티"}, {13, "손"}, {14, "발"}, {15, "신발"},
            {16, "몸 전체"}, {17, "항문"}, {18, "교미"}, {19, "여성"}
        };
        private static readonly Dictionary<string, float> NmsThresholds = new() { ["얼굴"] = 0.4f, ["가슴"] = 0.4f, ["보지"] = 0.4f };

        public static readonly string[] HbbClasses = new[] { "얼굴", "가슴", "겨드랑이", "보지", "발", "몸 전체", "자지", "팬티", "눈", "손", "교미", "신발", "가슴_옷", "여성" };
        public static readonly string[] ObbClasses = new[] { "Face_Female", "Face_Male", "Eyes", "Breast_Nude", "Breast_Underwear", "Breast_Clothed", "Armpit", "Navel", "Penis", "Vulva_Nude", "Butt_Nude", "Panty", "Butt_Clothed", "Hands", "Feet", "Shoes", "Body_Full", "Anus", "Sex_Act", "Hpis" };
        // OBB 모델 내부 클래스명(한국어, 중복 제거) - 타겟 체크박스 재구성에 사용
        public static readonly string[] ObbUniqueTargets = new[] { "얼굴", "눈", "가슴", "가슴_옷", "겨드랑이", "배꼽", "자지", "보지", "엉덩이", "항문", "팬티", "손", "발", "신발", "몸 전체", "교미", "여성" };

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
                var sessionOptions = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
                string detectedProvider = "CPU";
                try
                {
                    Console.WriteLine("🚀 CUDA 실행 프로바이더(NVIDIA GPU)를 시도합니다...");
                    sessionOptions.AppendExecutionProvider_CUDA();
                    detectedProvider = "CUDA (GPU)";
                    Console.WriteLine("✅ CUDA가 성공적으로 설정되었습니다.");
                }
                catch (Exception)
                {
                    Console.WriteLine("⚠️ CUDA 사용 불가. DirectML(Windows 기본 GPU 가속)을 시도합니다...");
                    try
                    {
                        sessionOptions.AppendExecutionProvider_DML();
                        detectedProvider = "DirectML (GPU)";
                        Console.WriteLine("✅ DirectML이 성공적으로 설정되었습니다.");
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("⚠️ GPU 가속 사용 불가. CPU로 실행합니다.");
                        sessionOptions.AppendExecutionProvider_CPU();
                        detectedProvider = "CPU";
                    }
                }

                model = new InferenceSession(modelPath, sessionOptions);
                Console.WriteLine($"✅ 모델 로드 성공: {modelPath}");
                CurrentExecutionProvider = detectedProvider;
                Console.WriteLine($"📈 현재 실행 장치: {CurrentExecutionProvider}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 모델 로드 중 심각한 오류 발생: {ex.Message}");
                model = null;
                CurrentExecutionProvider = "로드 실패 (CPU)";
            }
        }

        public bool IsModelLoaded() => model != null;

        public bool SwitchModel(string modelPath, bool obbMode)
        {
            lock (_lockObj)
            {
                if (model != null)
                {
                    model.Dispose();
                    model = null;
                }
                isObbMode = obbMode;
                LoadModel(modelPath);
                return IsModelLoaded();
            }
        }

        public List<Detection> DetectObjects(Mat frame)
        {
            if (!IsModelLoaded() || frame == null || frame.Empty()) return new List<Detection>();
            try
            {
                lock (_lockObj)
                {
                    if (model == null) return new List<Detection>();
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
                    var remainingDetections = new List<Detection>(nmsDetections);

                    foreach (var track in trackedResults)
                    {
                        var bestMatch = remainingDetections
                            .Select(det => new { Detection = det, Distance = new Rect2d(det.BBox[0], det.BBox[1], det.Width, det.Height).DistanceTo(track.box) })
                            .OrderBy(x => x.Distance)
                            .FirstOrDefault();

                        if (bestMatch != null && bestMatch.Distance < 50)
                        {
                            bestMatch.Detection.TrackId = track.id;
                            finalDetections.Add(bestMatch.Detection);
                            remainingDetections.Remove(bestMatch.Detection);
                        }
                    }
                    return finalDetections;
                }
            }
            catch (Exception ex) { LogCallback?.Invoke($"🚨 추론 에러: {ex.Message}"); return new List<Detection>(); }
        }

        private (float scale, int padX, int padY) Preprocess(Mat frame, float[] buffer)
        {
            const int TargetSize = 640;
            float scale = Math.Min((float)TargetSize / frame.Width, (float)TargetSize / frame.Height);
            int newWidth = (int)(frame.Width * scale);
            int newHeight = (int)(frame.Height * scale);
            int padX = (TargetSize - newWidth) / 2;
            int padY = (TargetSize - newHeight) / 2;
            
            Cv2.Resize(frame, _resizedMat, new OpenCvSharp.Size(newWidth, newHeight), interpolation: InterpolationFlags.Linear);
            Cv2.CopyMakeBorder(_resizedMat, _paddedMat, padY, TargetSize - newHeight - padY, padX, TargetSize - newWidth - padX, BorderTypes.Constant, new Scalar(114, 114, 114));
            
            _paddedMat.ConvertTo(_paddedMat, MatType.CV_32F, 1.0 / 255.0);
            Cv2.Split(_paddedMat, out _channels);
            
            Marshal.Copy(_channels[2].Data, buffer, 0, TargetSize * TargetSize);
            Marshal.Copy(_channels[1].Data, buffer, TargetSize * TargetSize, TargetSize * TargetSize);
            Marshal.Copy(_channels[0].Data, buffer, 2 * TargetSize * TargetSize, TargetSize * TargetSize);

            return (scale, padX, padY);
        }

        private List<Detection> Postprocess(Tensor<float> output, float scale, int padX, int padY, int originalWidth, int originalHeight)
        {
            var detections = new List<Detection>();
            var dims = output.Dimensions;

            // 차원 구조에 따른 자동 대응 로직
            bool isTransposed = dims.Length == 3 && dims[1] > dims[2]; // 예: [1, 8400, 25] 형태인 경우
            int numAnchors = isTransposed ? dims[1] : dims[2]; // 8400
            int numFeatures = isTransposed ? dims[2] : dims[1]; // 18 or 25

            int numClasses = isObbMode ? numFeatures - 5 : numFeatures - 4;

            for (int i = 0; i < numAnchors; i++)
            {
                float maxScore = 0;
                int maxClassId = -1;

                for (int c = 0; c < numClasses; c++)
                {
                    // 배열 인덱스 초과 방지 안전장치
                    if (4 + c >= numFeatures) break;

                    float score = isTransposed ? output[0, i, 4 + c] : output[0, 4 + c, i];
                    if (score > maxScore)
                    {
                        maxScore = score;
                        maxClassId = c;
                    }
                }

                if (maxScore <= ConfThreshold || maxClassId == -1) continue;

                string className = isObbMode
                    ? ClassNamesObb.GetValueOrDefault(maxClassId)
                    : ClassNames.GetValueOrDefault(maxClassId);

                if (className == null || !Targets.Contains(className)) continue;

                float cx = isTransposed ? output[0, i, 0] : output[0, 0, i];
                float cy = isTransposed ? output[0, i, 1] : output[0, 1, i];
                float w  = isTransposed ? output[0, i, 2] : output[0, 2, i];
                float h  = isTransposed ? output[0, i, 3] : output[0, 3, i];

                int x1 = (int)((cx - w / 2 - padX) / scale);
                int y1 = (int)((cy - h / 2 - padY) / scale);
                int x2 = (int)((cx + w / 2 - padX) / scale);
                int y2 = (int)((cy + h / 2 - padY) / scale);

                float angle = 0f;
                if (isObbMode)
                {
                    // OBB 출력 텐서는 [cx, cy, w, h, cls_0, ..., cls_N, angle] 구조이므로 마지막 인덱스가 angle.
                    int angleIndex = numFeatures - 1;
                    angle = isTransposed ? output[0, i, angleIndex] : output[0, angleIndex, i];
                }

                detections.Add(new Detection {
                    ClassName = className,
                    Confidence = maxScore,
                    BBox = new[] { Math.Max(0, x1), Math.Max(0, y1), Math.Min(originalWidth, x2), Math.Min(originalHeight, y2) },
                    Angle = angle,
                    CenterX = (cx - padX) / scale,
                    CenterY = (cy - padY) / scale,
                    ObbWidth = w / scale,
                    ObbHeight = h / scale,
                });
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
            float cx = detection.CenterX;
            float cy = detection.CenterY;
            float ow = detection.ObbWidth;
            float oh = detection.ObbHeight;
            if (ow <= 0 || oh <= 0) return;

            float degree = detection.Angle * (180.0f / (float)Math.PI);
            var center2f = new Point2f(cx, cy);

            // Step 1: 검출 객체가 수평이 되도록 프레임 전체를 회전
            using var M_fwd = Cv2.GetRotationMatrix2D(center2f, degree, 1.0);
            using var warped = new Mat();
            Cv2.WarpAffine(frame, warped, M_fwd, frame.Size());

            // Step 2: 회전된 프레임에서 수평 ROI 추출
            int rx = Math.Max(0, (int)(cx - ow / 2));
            int ry = Math.Max(0, (int)(cy - oh / 2));
            int rw = Math.Min(frame.Width - rx, (int)ow);
            int rh = Math.Min(frame.Height - ry, (int)oh);
            if (rw <= 0 || rh <= 0) return;

            // Step 3: 수평 ROI에 검열 효과 적용 (격자 자체가 수평 기준으로 생성됨)
            using (var roi = new Mat(warped, new Rect(rx, ry, rw, rh)))
            {
                if (currentCensorType == CensorType.Mosaic)
                {
                    int smallW = Math.Max(1, rw / strength);
                    int smallH = Math.Max(1, rh / strength);
                    using var small = new Mat();
                    using var enlarged = new Mat();
                    Cv2.Resize(roi, small, new OpenCvSharp.Size(smallW, smallH), interpolation: InterpolationFlags.Linear);
                    Cv2.Resize(small, enlarged, new OpenCvSharp.Size(rw, rh), interpolation: InterpolationFlags.Nearest);
                    enlarged.CopyTo(roi);
                }
                else if (currentCensorType == CensorType.Blur)
                {
                    int kernelSize = Math.Max(3, strength + 1);
                    if (kernelSize % 2 == 0) kernelSize++;
                    using var blurred = new Mat();
                    Cv2.GaussianBlur(roi, blurred, new OpenCvSharp.Size(kernelSize, kernelSize), 0);
                    blurred.CopyTo(roi);
                }
                else if (currentCensorType == CensorType.BlackBox)
                {
                    var blackColor = roi.Channels() == 4 ? new Scalar(0, 0, 0, 255) : new Scalar(0, 0, 0);
                    roi.SetTo(blackColor);
                }
            }

            // Step 4: 검열이 적용된 프레임을 원래 각도로 역회전
            using var M_inv = Cv2.GetRotationMatrix2D(center2f, -degree, 1.0);
            using var warpedBack = new Mat();
            Cv2.WarpAffine(warped, warpedBack, M_inv, frame.Size());

            // Step 5: 원본 프레임 좌표계에서 OBB 다각형 마스크 생성
            var rotRect = new RotatedRect(center2f, new Size2f(ow, oh), degree);
            var pts = rotRect.Points().Select(p => new Point((int)p.X, (int)p.Y)).ToArray();
            using var mask = new Mat(frame.Size(), MatType.CV_8UC1, Scalar.Black);
            Cv2.FillConvexPoly(mask, pts, Scalar.White);

            // Step 6: 역회전된 결과를 마스크 영역만큼 원본 프레임에 합성
            warpedBack.CopyTo(frame, mask);
        }

        public void SetTargets(List<string> targets) => Targets = targets ?? new List<string>();
        public void SetStrength(int strength) => this.strength = Math.Max(5, Math.Min(50, strength));
        public void SetCensorType(CensorType censorType) => this.currentCensorType = censorType;

        public void WarmUpModel()
        {
            if (!IsModelLoaded()) return;
            try
            {
                lock (_lockObj)
                {
                    if (model == null) return;
                    Console.WriteLine("🔥 모델 워밍업 시작...");
                    var dummyInput = new DenseTensor<float>(new float[1 * 3 * 640 * 640], new[] { 1, 3, 640, 640 });
                    var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", dummyInput) };

                    using (model.Run(inputs)) { }

                    Console.WriteLine("✅ 모델 워밍업 완료.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 모델 워밍업 실패: {ex.Message}");
            }
        }

        public void Dispose()
        {
            model?.Dispose();
            model = null;

            _resizedMat?.Dispose();
            _resizedMat = null;
            
            _paddedMat?.Dispose();
            _paddedMat = null;
            
            if (_channels != null)
            {
                foreach (var c in _channels)
                {
                    c?.Dispose();
                }
                _channels = null;
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
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