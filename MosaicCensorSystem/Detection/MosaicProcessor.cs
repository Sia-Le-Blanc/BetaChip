#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using MosaicCensorSystem;

namespace MosaicCensorSystem.Detection
{
    public enum CensorType { Mosaic, Blur, BlackBox }

    public class Detection
    {
        public int TrackId { get; set; }
        public string ClassName { get; set; } = "";
        public float Confidence { get; set; }
        public int[] BBox { get; set; } = new int[4];
        public int Width  => BBox[2] - BBox[0];
        public int Height => BBox[3] - BBox[1];

        // OBB 전용 속성
        public float Angle   { get; set; } = 0f;
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float ObbWidth  { get; set; }
        public float ObbHeight { get; set; }
    }

    /// <summary>
    /// 스레드별 독립 추론 컨텍스트.
    /// 단일 MosaicProcessor(InferenceSession)를 여러 모니터가 공유할 때
    /// inputBuffer, Mat 버퍼, 검출 리스트, 트래커를 스레드별로 완전히 격리합니다.
    /// </summary>
    public sealed class InferenceContext : IDisposable
    {
        // ── 전처리 전용 버퍼 ────────────────────────────────────────────────────
        internal float[] InputBuffer;          // ONNX 입력 텐서 메모리
        internal Mat     ResizedMat = new Mat();
        internal Mat     PaddedMat  = new Mat();
        internal Mat[]   Channels   = null;    // Cv2.Split 결과

        // ── 후처리 전용 버퍼 (GC 재사용) ───────────────────────────────────────
        internal readonly List<Detection> DetectionBuffer = new(256);
        internal readonly List<Detection> NmsBuffer       = new(64);
        internal readonly List<Detection> FinalBuffer     = new(64);

        // ── 타겟 클래스 인덱스 캐시 (스레드별 독립) ────────────────────────────
        internal int[]  TargetClassIndices = Array.Empty<int>();
        internal string TargetCacheKey     = "";

        // ── 트래킹 (모니터별 독립 상태) ────────────────────────────────────────
        internal readonly SortTracker Tracker = new SortTracker();

        internal InferenceContext() { }

        /// <summary>inputBuffer가 필요한 최소 크기 미만이면 재할당합니다.</summary>
        internal void EnsureBufferSize(int size)
        {
            if (InputBuffer == null || InputBuffer.Length < size)
                InputBuffer = new float[size];
        }

        public void Dispose()
        {
            ResizedMat?.Dispose();
            PaddedMat?.Dispose();
            if (Channels != null)
            {
                foreach (var c in Channels) c?.Dispose();
                Channels = null;
            }
        }
    }

    public class MosaicProcessor : IDisposable
    {
        private InferenceSession _model;

        // ── 동시성 보호 ──────────────────────────────────────────────────────────
        // 읽기(추론): 여러 InferenceThread가 동시에 ReadLock 획득 → 병렬 model.Run() 허용
        // 쓰기(교체/해제): SwitchModel·Dispose 가 WriteLock 획득 → 독점 실행
        // ONNX Runtime: InferenceSession.Run()은 공식적으로 멀티스레드-세이프
        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();

        public bool isObbMode = false;
        private int _inputSize = 640;

        // FREE 등급(단일 InferenceThread) 전용 인스턴스 컨텍스트
        // — RWLock 읽기 구역 내에서만 접근되므로 thread-safe
        private readonly InferenceContext _instanceCtx = new InferenceContext();

        public Action<string> LogCallback { get; set; }
        public float ConfThreshold { get; set; } = 0.05f;
        public List<string> Targets { get; private set; } = new List<string> { "얼굴", "가슴" };
        private CensorType currentCensorType = CensorType.Mosaic;
        private int strength = 20;

        public string CurrentExecutionProvider { get; private set; } = "CPU";

        public static string[] HbbClasses        => ModelRegistry.Standard.Classes;
        public static string[] ObbUniqueTargets  => ModelRegistry.Oriented.Classes;

        public MosaicProcessor(string modelPath)
        {
            LoadModel(modelPath);
            _instanceCtx.EnsureBufferSize(3 * _inputSize * _inputSize);
        }

        // ── 모델 로드 (항상 WriteLock 내 혹은 생성자에서 호출) ──────────────────
        private void LoadModel(string modelPath)
        {
            if (!File.Exists(modelPath))
            {
                string msg = $"❌ 모델 파일 없음: {modelPath}";
                Console.WriteLine(msg);
                LogCallback?.Invoke(msg);
                return;
            }

            var fileInfo = new System.IO.FileInfo(modelPath);
            Console.WriteLine($"[LoadModel] 파일 크기: {fileInfo.Length:N0} bytes ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)");
            if (fileInfo.Length < 1024)
            {
                string msg = $"❌ 파일 손상 의심 ({fileInfo.Length} bytes): {modelPath}";
                Console.WriteLine(msg);
                LogCallback?.Invoke(msg);
                return;
            }

            try
            {
                int cpuCores = Environment.ProcessorCount;
                var sessionOptions = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    EnableCpuMemArena      = true,
                    EnableMemoryPattern    = true,
                    ExecutionMode          = ExecutionMode.ORT_PARALLEL,
                    IntraOpNumThreads      = cpuCores,
                    InterOpNumThreads      = Math.Max(1, cpuCores / 2),
                };
                Console.WriteLine($"[SessionOptions] Cores={cpuCores}, IntraOp={cpuCores}, InterOp={Math.Max(1, cpuCores / 2)}, Mode=PARALLEL");

                string detectedProvider = "CPU";
                try
                {
                    Console.WriteLine("🚀 CUDA 실행 프로바이더를 시도합니다...");
                    sessionOptions.AppendExecutionProvider_CUDA();
                    detectedProvider = "CUDA (GPU)";
                    Console.WriteLine("✅ CUDA 설정 완료.");
                }
                catch (Exception cudaEx)
                {
                    Console.WriteLine($"⚠️ CUDA 불가 ({cudaEx.Message}). DirectML 시도...");
                    try
                    {
                        sessionOptions.AppendExecutionProvider_DML();
                        detectedProvider = "DirectML (GPU)";
                        Console.WriteLine("✅ DirectML 설정 완료.");
                    }
                    catch (Exception dmlEx)
                    {
                        Console.WriteLine($"⚠️ GPU 가속 불가 ({dmlEx.Message}). CPU 실행.");
                        sessionOptions.AppendExecutionProvider_CPU();
                        detectedProvider = "CPU";
                    }
                }

                try
                {
                    _model = new InferenceSession(modelPath, sessionOptions);
                }
                catch (Exception sessionEx)
                {
                    Console.WriteLine("❌ InferenceSession 생성 실패:");
                    Console.WriteLine(sessionEx.ToString());
                    LogCallback?.Invoke($"❌ 모델 로드 실패: {sessionEx.Message}");
                    if (sessionEx.InnerException != null)
                        LogCallback?.Invoke($"   ㄴ 상세: {sessionEx.InnerException.Message}");
                    _model = null;
                    CurrentExecutionProvider = "로드 실패 (CPU)";
                    return;
                }

                Console.WriteLine($"✅ 모델 로드 성공: {modelPath}");
                CurrentExecutionProvider = detectedProvider;
                Console.WriteLine($"📈 실행 장치: {CurrentExecutionProvider}");

                foreach (var kv in _model.InputMetadata)
                    Console.WriteLine($"  Input  '{kv.Key}': type={kv.Value.ElementType}, shape=[{string.Join(",", kv.Value.Dimensions)}]");
                foreach (var kv in _model.OutputMetadata)
                    Console.WriteLine($"  Output '{kv.Key}': type={kv.Value.ElementType}, shape=[{string.Join(",", kv.Value.Dimensions)}]");

                try
                {
                    var inputDims = _model.InputMetadata.Values.First().Dimensions;
                    if (inputDims.Length >= 4 && inputDims[2] > 0 && inputDims[3] > 0)
                    {
                        int newSize = (int)inputDims[2];
                        if (newSize != _inputSize)
                        {
                            Console.WriteLine($"  → 입력 크기: {_inputSize}→{newSize}px");
                            LogCallback?.Invoke($"📐 입력 크기 조정: {_inputSize}→{newSize}px");
                            _inputSize = newSize;
                        }
                    }
                }
                catch (Exception metaEx)
                {
                    Console.WriteLine($"⚠️ InputMetadata 읽기 실패: {metaEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ 모델 로드 중 심각한 오류:");
                Console.WriteLine(ex.ToString());
                LogCallback?.Invoke($"❌ 모델 로드 오류: {ex.Message}");
                _model = null;
                CurrentExecutionProvider = "로드 실패 (CPU)";
            }
        }

        public bool IsModelLoaded() => _model != null;

        public bool SwitchModel(string modelPath, bool obbMode)
        {
            _rwLock.EnterWriteLock();
            try
            {
                _model?.Dispose();
                _model = null;
                isObbMode = obbMode;
                LoadModel(modelPath);
                // 인스턴스 컨텍스트 버퍼 크기 갱신
                _instanceCtx.EnsureBufferSize(3 * _inputSize * _inputSize);
                return IsModelLoaded();
            }
            finally { _rwLock.ExitWriteLock(); }
        }

        // ── 다중 모니터용 컨텍스트 생성 ─────────────────────────────────────────
        /// <summary>
        /// 각 모니터의 InferenceThread에서 하나씩 생성하세요.
        /// 반환된 컨텍스트는 caller가 Dispose해야 합니다.
        /// </summary>
        public InferenceContext CreateContext()
        {
            var ctx = new InferenceContext();
            _rwLock.EnterReadLock();
            try { ctx.EnsureBufferSize(3 * _inputSize * _inputSize); }
            finally { _rwLock.ExitReadLock(); }
            return ctx;
        }

        // ── FREE 등급: 단일 스레드, 인스턴스 컨텍스트 사용 ──────────────────────
        public List<Detection> DetectObjects(Mat frame)
        {
            if (frame == null || frame.Empty()) return new List<Detection>();
            _rwLock.EnterReadLock();
            try
            {
                if (_model == null) return new List<Detection>();
                _instanceCtx.EnsureBufferSize(3 * _inputSize * _inputSize);
                return DetectObjectsCore(frame, _instanceCtx, _model, _inputSize);
            }
            catch (Exception ex)
            {
                LogCallback?.Invoke($"🚨 추론 에러: {ex.Message}");
                return new List<Detection>();
            }
            finally { _rwLock.ExitReadLock(); }
        }

        // ── PLUS/PATREON 등급: 다중 스레드, 전용 컨텍스트 사용 → 진정한 병렬 추론 ──
        public List<Detection> DetectObjects(Mat frame, InferenceContext ctx)
        {
            if (frame == null || frame.Empty()) return new List<Detection>();
            _rwLock.EnterReadLock();
            try
            {
                if (_model == null) return new List<Detection>();
                ctx.EnsureBufferSize(3 * _inputSize * _inputSize);
                return DetectObjectsCore(frame, ctx, _model, _inputSize);
            }
            catch (Exception ex)
            {
                LogCallback?.Invoke($"🚨 추론 에러: {ex.Message}");
                return new List<Detection>();
            }
            finally { _rwLock.ExitReadLock(); }
        }

        // ── 공통 추론 핵심 (ctx로 완전히 격리됨) ──────────────────────────────────
        private List<Detection> DetectObjectsCore(Mat frame, InferenceContext ctx, InferenceSession m, int inputSize)
        {
            var (scale, padX, padY) = Preprocess(frame, ctx, inputSize);
            var inputTensor = new DenseTensor<float>(ctx.InputBuffer, new[] { 1, 3, inputSize, inputSize });
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", inputTensor) };

            // ONNX Runtime: Run()은 동일 session에 대해 멀티스레드 동시 호출이 안전함
            using var results = m.Run(inputs);
            var outputTensor = results.First().AsTensor<float>();
            var detections   = Postprocess(outputTensor, scale, padX, padY, frame.Width, frame.Height, ctx, inputSize);
            var nmsDetections = ApplyNMS(detections, ctx);

            var trackBoxes   = nmsDetections.Select(d => new Rect2d(d.BBox[0], d.BBox[1], d.Width, d.Height)).ToList();
            var trackedResults = ctx.Tracker.Update(trackBoxes);

            ctx.FinalBuffer.Clear();
            var remaining = new List<Detection>(nmsDetections);
            foreach (var track in trackedResults)
            {
                var bestMatch = remaining
                    .Select(det => new { Detection = det, Distance = new Rect2d(det.BBox[0], det.BBox[1], det.Width, det.Height).DistanceTo(track.box) })
                    .OrderBy(x => x.Distance)
                    .FirstOrDefault();
                if (bestMatch != null && bestMatch.Distance < 50)
                {
                    bestMatch.Detection.TrackId = track.id;
                    ctx.FinalBuffer.Add(bestMatch.Detection);
                    remaining.Remove(bestMatch.Detection);
                }
            }
            return ctx.FinalBuffer;
        }

        // ── AVX2 SIMD 단일패스 byte→float 정규화 ────────────────────────────────
        private static unsafe void ByteToFloatNormalizeSimd(byte* src, float* dst, int count)
        {
            const float inv255 = 1.0f / 255.0f;
            int i = 0;

            if (Avx2.IsSupported)
            {
                var invVec256 = Vector256.Create(inv255);
                for (; i <= count - 8; i += 8)
                {
                    var bytes8   = Sse2.LoadScalarVector128((long*)(src + i)).AsByte();
                    var ints256  = Avx2.ConvertToVector256Int32(bytes8);
                    var floats256 = Avx.ConvertToVector256Single(ints256);
                    var norm256  = Avx.Multiply(floats256, invVec256);
                    Avx.Store(dst + i, norm256);
                }
            }
            for (; i < count; i++)
                dst[i] = src[i] * inv255;
        }

        private unsafe (float scale, int padX, int padY) Preprocess(Mat frame, InferenceContext ctx, int targetSize)
        {
            float scale    = Math.Min((float)targetSize / frame.Width, (float)targetSize / frame.Height);
            int newWidth   = (int)(frame.Width  * scale);
            int newHeight  = (int)(frame.Height * scale);
            int padX       = (targetSize - newWidth)  / 2;
            int padY       = (targetSize - newHeight) / 2;

            Cv2.Resize(frame, ctx.ResizedMat, new OpenCvSharp.Size(newWidth, newHeight),
                interpolation: InterpolationFlags.Linear);
            Cv2.CopyMakeBorder(ctx.ResizedMat, ctx.PaddedMat,
                padY, targetSize - newHeight - padY,
                padX, targetSize - newWidth  - padX,
                BorderTypes.Constant, new Scalar(114, 114, 114));

            if (ctx.Channels != null)
                foreach (var c in ctx.Channels) c?.Dispose();
            Cv2.Split(ctx.PaddedMat, out ctx.Channels);

            int planeSize = targetSize * targetSize;
            var gcHandle  = GCHandle.Alloc(ctx.InputBuffer, GCHandleType.Pinned);
            try
            {
                float* bufPtr = (float*)gcHandle.AddrOfPinnedObject();
                Parallel.For(0, 3, ch =>
                {
                    int srcCh = 2 - ch;  // BGR → RGB
                    ByteToFloatNormalizeSimd(
                        (byte*)ctx.Channels[srcCh].Data,
                        bufPtr + ch * planeSize,
                        planeSize);
                });
            }
            finally { gcHandle.Free(); }

            return (scale, padX, padY);
        }

        private void RebuildTargetIndicesIfNeeded(int numClasses, InferenceContext ctx)
        {
            string key = string.Join(",", Targets) + (isObbMode ? ":obb" : ":hbb");
            if (key == ctx.TargetCacheKey) return;

            ctx.TargetCacheKey = key;
            var indices = new List<int>(Targets.Count);
            for (int c = 0; c < numClasses; c++)
            {
                string name = isObbMode
                    ? ModelRegistry.Oriented.GetClassName(c)
                    : ModelRegistry.Standard.GetClassName(c);
                if (name != null && Targets.Contains(name))
                    indices.Add(c);
            }
            ctx.TargetClassIndices = indices.ToArray();
        }

        private List<Detection> Postprocess(Tensor<float> output, float scale, int padX, int padY,
            int originalWidth, int originalHeight, InferenceContext ctx, int inputSize)
        {
            ctx.DetectionBuffer.Clear();

            var dims      = output.Dimensions;
            bool isTransposed = dims.Length == 3 && dims[1] > dims[2];
            int numAnchors  = isTransposed ? dims[1] : dims[2];
            int numFeatures = isTransposed ? dims[2] : dims[1];
            int numClasses  = isObbMode ? numFeatures - 5 : numFeatures - 4;

            RebuildTargetIndicesIfNeeded(numClasses, ctx);
            if (ctx.TargetClassIndices.Length == 0) return ctx.DetectionBuffer;

            ReadOnlySpan<float> flat;
            float[] flatArr = null;
            if (output is DenseTensor<float> dt) flat = dt.Buffer.Span;
            else { flatArr = output.ToArray(); flat = flatArr; }

            const float RadToDeg = 180.0f / MathF.PI;

            for (int i = 0; i < numAnchors; i++)
            {
                float maxScore   = 0f;
                int   maxClassId = -1;

                foreach (int c in ctx.TargetClassIndices)
                {
                    float score = isTransposed
                        ? flat[i * numFeatures + 4 + c]
                        : flat[(4 + c) * numAnchors + i];
                    if (score > maxScore) { maxScore = score; maxClassId = c; }
                }

                if (maxScore <= ConfThreshold || maxClassId == -1) continue;

                string className = isObbMode
                    ? ModelRegistry.Oriented.GetClassName(maxClassId)
                    : ModelRegistry.Standard.GetClassName(maxClassId);
                if (className == null) continue;

                float cx, cy, w, h;
                if (isTransposed)
                {
                    int b = i * numFeatures;
                    cx = flat[b]; cy = flat[b + 1]; w = flat[b + 2]; h = flat[b + 3];
                }
                else
                {
                    cx = flat[i];
                    cy = flat[    numAnchors + i];
                    w  = flat[2 * numAnchors + i];
                    h  = flat[3 * numAnchors + i];
                }

                float origCx = (cx - padX) / scale;
                float origCy = (cy - padY) / scale;
                float origW  = w / scale;
                float origH  = h / scale;

                float angle = 0f;
                int x1, y1, x2, y2;

                if (isObbMode)
                {
                    int angleIndex = numFeatures - 1;
                    if (angleIndex > 4)
                    {
                        float rawAngle = isTransposed
                            ? flat[i * numFeatures + angleIndex]
                            : flat[angleIndex * numAnchors + i];
                        angle = rawAngle * RadToDeg;
                    }
                    var rotRect   = new RotatedRect(new Point2f(origCx, origCy), new Size2f(origW, origH), angle);
                    var boundRect = rotRect.BoundingRect();
                    x1 = boundRect.Left; y1 = boundRect.Top;
                    x2 = boundRect.Right; y2 = boundRect.Bottom;
                }
                else
                {
                    x1 = (int)(origCx - origW / 2);
                    y1 = (int)(origCy - origH / 2);
                    x2 = (int)(origCx + origW / 2);
                    y2 = (int)(origCy + origH / 2);
                }

                ctx.DetectionBuffer.Add(new Detection
                {
                    ClassName  = className,
                    Confidence = maxScore,
                    BBox       = new[] { Math.Max(0, x1), Math.Max(0, y1), Math.Min(originalWidth, x2), Math.Min(originalHeight, y2) },
                    Angle      = angle,
                    CenterX    = origCx,
                    CenterY    = origCy,
                    ObbWidth   = origW,
                    ObbHeight  = origH
                });
            }
            return ctx.DetectionBuffer;
        }

        private List<Detection> ApplyNMS(List<Detection> detections, InferenceContext ctx)
        {
            ctx.NmsBuffer.Clear();
            var thresholds = isObbMode ? ModelRegistry.Oriented.NmsThresholds : ModelRegistry.Standard.NmsThresholds;

            foreach (var group in detections.GroupBy(d => d.ClassName))
            {
                var orderedGroup  = group.OrderByDescending(d => d.Confidence).ToList();
                float nmsThreshold = thresholds.TryGetValue(group.Key, out float t) ? t : 0.4f;
                while (orderedGroup.Count > 0)
                {
                    var best = orderedGroup[0];
                    ctx.NmsBuffer.Add(best);
                    for (int i = orderedGroup.Count - 1; i >= 1; i--)
                    {
                        if (CalculateIoU(best.BBox, orderedGroup[i].BBox) >= nmsThreshold)
                            orderedGroup.RemoveAt(i);
                    }
                    orderedGroup.RemoveAt(0);
                }
            }
            return ctx.NmsBuffer;
        }

        private float CalculateIoU(int[] boxA, int[] boxB)
        {
            int xA = Math.Max(boxA[0], boxB[0]);
            int yA = Math.Max(boxA[1], boxB[1]);
            int xB = Math.Min(boxA[2], boxB[2]);
            int yB = Math.Min(boxA[3], boxB[3]);
            float interArea = Math.Max(0, xB - xA) * Math.Max(0, yB - yA);
            float boxAArea  = (boxA[2] - boxA[0]) * (boxA[3] - boxA[1]);
            float boxBArea  = (boxB[2] - boxB[0]) * (boxB[3] - boxB[1]);
            float unionArea = boxAArea + boxBArea - interArea;
            return unionArea > 0 ? interArea / unionArea : 0;
        }

        public void ApplySingleCensorOptimized(Mat frame, Detection detection)
        {
            if (isObbMode)
            {
                if (detection.ObbWidth <= 0 || detection.ObbHeight <= 0) return;

                var center   = new Point2f(detection.CenterX, detection.CenterY);
                var size     = new Size2f(detection.ObbWidth, detection.ObbHeight);
                var rotRect  = new RotatedRect(center, size, detection.Angle);

                Rect boundingRect = rotRect.BoundingRect();
                int x = Math.Max(0, boundingRect.X);
                int y = Math.Max(0, boundingRect.Y);
                int w = Math.Min(boundingRect.Width,  frame.Width  - x);
                int h = Math.Min(boundingRect.Height, frame.Height - y);
                if (w <= 0 || h <= 0) return;

                Rect safeRect = new Rect(x, y, w, h);
                using Mat region    = new Mat(frame, safeRect);
                using Mat effectMat = region.Clone();

                if (currentCensorType == CensorType.Mosaic)
                {
                    int smallW = Math.Max(1, w / strength), smallH = Math.Max(1, h / strength);
                    using Mat small = new Mat();
                    Cv2.Resize(region, small, new OpenCvSharp.Size(smallW, smallH), interpolation: InterpolationFlags.Linear);
                    Cv2.Resize(small, effectMat, new OpenCvSharp.Size(w, h), interpolation: InterpolationFlags.Nearest);
                }
                else if (currentCensorType == CensorType.Blur)
                {
                    int k = Math.Max(3, strength + 1); if (k % 2 == 0) k++;
                    Cv2.GaussianBlur(region, effectMat, new OpenCvSharp.Size(k, k), 0);
                }
                else if (currentCensorType == CensorType.BlackBox)
                    effectMat.SetTo(region.Channels() == 4 ? new Scalar(0, 0, 0, 255) : new Scalar(0, 0, 0));

                using Mat mask = new Mat(safeRect.Size, MatType.CV_8UC1, Scalar.All(0));
                var pts = rotRect.Points()
                    .Select(p => new OpenCvSharp.Point((int)Math.Round(p.X - x), (int)Math.Round(p.Y - y)))
                    .ToArray();
                Cv2.FillConvexPoly(mask, pts, Scalar.All(255));
                effectMat.CopyTo(region, mask);
            }
            else
            {
                if (detection.Width <= 0 || detection.Height <= 0) return;
                Rect roi = new Rect(detection.BBox[0], detection.BBox[1], detection.Width, detection.Height);
                using Mat region = new Mat(frame, roi);

                if (currentCensorType == CensorType.Mosaic)
                {
                    int w = region.Width, h = region.Height;
                    int smallW = Math.Max(1, w / strength), smallH = Math.Max(1, h / strength);
                    using Mat small = new Mat();
                    Cv2.Resize(region, small, new OpenCvSharp.Size(smallW, smallH), interpolation: InterpolationFlags.Linear);
                    Cv2.Resize(small, region, new OpenCvSharp.Size(w, h), interpolation: InterpolationFlags.Nearest);
                }
                else if (currentCensorType == CensorType.Blur)
                {
                    int k = Math.Max(3, strength + 1); if (k % 2 == 0) k++;
                    Cv2.GaussianBlur(region, region, new OpenCvSharp.Size(k, k), 0);
                }
                else if (currentCensorType == CensorType.BlackBox)
                    region.SetTo(region.Channels() == 4 ? new Scalar(0, 0, 0, 255) : new Scalar(0, 0, 0));
            }
        }

        public void SetTargets(List<string> targets)
        {
            Targets = targets ?? new List<string>();
            // 각 컨텍스트의 TargetCacheKey가 다음 추론 시 자동으로 캐시 미스→재빌드됨
        }

        public void SetStrength(int s)    => strength = Math.Max(5, Math.Min(50, s));
        public void SetCensorType(CensorType t) => currentCensorType = t;

        public void WarmUpModel()
        {
            _rwLock.EnterReadLock();
            try
            {
                if (_model == null) return;
                Console.WriteLine("🔥 모델 워밍업 시작...");
                int sz = _inputSize;
                var dummyInput = new DenseTensor<float>(new float[1 * 3 * sz * sz], new[] { 1, 3, sz, sz });
                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", dummyInput) };
                using (_model.Run(inputs)) { }
                Console.WriteLine("✅ 모델 워밍업 완료.");
            }
            catch (Exception ex) { Console.WriteLine($"⚠️ 워밍업 실패: {ex.Message}"); }
            finally { _rwLock.ExitReadLock(); }
        }

        public void Dispose()
        {
            _rwLock.EnterWriteLock();
            try
            {
                _model?.Dispose();
                _model = null;
                _instanceCtx?.Dispose();
            }
            finally
            {
                _rwLock.ExitWriteLock();
                _rwLock.Dispose();
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    public static class Rect2dExtensions
    {
        public static double DistanceTo(this Rect2d r1, Rect2d r2)
        {
            double dx = (r1.X + r1.Width  / 2) - (r2.X + r2.Width  / 2);
            double dy = (r1.Y + r1.Height / 2) - (r2.Y + r2.Height / 2);
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
