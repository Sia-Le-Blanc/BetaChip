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
    /// </summary>
    public sealed class InferenceContext : IDisposable
    {
        internal float[] InputBuffer;
        internal float[] InputBufferRef;
        internal Mat     ResizedMat = new Mat();
        internal Mat     PaddedMat  = new Mat();
        internal Mat[]   Channels   = null;

        internal DenseTensor<float> InputTensor;
        internal List<NamedOnnxValue> Inputs;

        internal readonly List<Detection> DetectionBuffer = new(256);
        internal readonly List<Detection> NmsBuffer       = new(64);
        internal readonly List<Detection> FinalBuffer     = new(64);
        internal readonly List<Detection> RemainingBuffer = new(64);
        internal readonly List<Rect2d> TrackBoxes         = new(64);

        internal int[]  TargetClassIndices = Array.Empty<int>();
        internal string TargetCacheKey     = "";

        internal readonly SortTracker Tracker = new SortTracker();

        internal InferenceContext() { }

        internal void EnsureBufferSize(int size)
        {
            if (InputBuffer == null || InputBuffer.Length < size)
                InputBuffer = new float[size];
        }

        internal void EnsureInputTensor(int inputSize, string inputNodeName = "images")
        {
            int needed = 3 * inputSize * inputSize;
            if (InputTensor == null || InputTensor.Buffer.Length != needed || !ReferenceEquals(InputBufferRef, InputBuffer) || Inputs == null || (Inputs.Count > 0 && Inputs[0].Name != inputNodeName))
            {
                InputTensor = new DenseTensor<float>(InputBuffer, new[] { 1, 3, inputSize, inputSize });
                Inputs = new List<NamedOnnxValue>(1)
                {
                    NamedOnnxValue.CreateFromTensor(inputNodeName, InputTensor)
                };
                InputBufferRef = InputBuffer;
            }
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
            InputTensor = null;
            Inputs = null;
        }
    }

    public class MosaicProcessor : IDisposable
    {
        private InferenceSession _model;
        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();

        public bool isObbMode = false;
        private string _inputNodeName = "images";
        private string _outputNodeName = "output0";
        private int _inputSize = 640;
        private readonly InferenceContext _instanceCtx = new InferenceContext();

        public Action<string> LogCallback { get; set; }
        public float ConfThreshold { get; set; } = 0.05f;
        public List<string> Targets { get; private set; } = new List<string> { "얼굴", "가슴", "보지", "팬티" };
        private CensorType currentCensorType = CensorType.Mosaic;
        private int strength = 20;

        public string CurrentExecutionProvider { get; private set; } = "CPU";

        public static string[] HbbClasses        => ModelRegistry.Standard.Classes;
        public static string[] ObbUniqueTargets  => ModelRegistry.Oriented.Classes;

        public MosaicProcessor(string modelPath)
        {
            LoadModel(modelPath);
            _instanceCtx.EnsureBufferSize(3 * _inputSize * _inputSize);
            _instanceCtx.EnsureInputTensor(_inputSize);
        }

        private void LoadModel(string modelPath)
        {
            if (!File.Exists(modelPath)) return;
            try
            {
                int cpuCores = Environment.ProcessorCount;
                var sessionOptions = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    ExecutionMode          = ExecutionMode.ORT_PARALLEL,
                    IntraOpNumThreads      = cpuCores,
                    InterOpNumThreads      = Math.Max(1, cpuCores / 2),
                };

                string detectedProvider = "CPU";
                try { sessionOptions.AppendExecutionProvider_CUDA(); detectedProvider = "CUDA (GPU)"; }
                catch {
                    try { sessionOptions.AppendExecutionProvider_DML(); detectedProvider = "DirectML (GPU)"; }
                    catch { sessionOptions.AppendExecutionProvider_CPU(); detectedProvider = "CPU"; }
                }

                _model = new InferenceSession(modelPath, sessionOptions);
                CurrentExecutionProvider = detectedProvider;

                _inputNodeName = _model.InputMetadata.Keys.FirstOrDefault() ?? "images";
                _outputNodeName = _model.OutputMetadata.Keys.FirstOrDefault() ?? "output0";

                var inputDims = _model.InputMetadata[_inputNodeName].Dimensions;
                if (inputDims.Length >= 4 && inputDims[2] > 0) _inputSize = (int)inputDims[2];

                LogCallback?.Invoke($"🧠 [Model] 로드 완료: 입력={_inputNodeName}({_inputSize}x{_inputSize}), 출력={_outputNodeName}, 디바이스={detectedProvider}");
            }
            catch (Exception ex) { LogCallback?.Invoke($"❌ 모델 로드 오류: {ex.Message}"); }
        }

        public bool IsModelLoaded() => _model != null;

        public bool SwitchModel(string modelPath, bool obbMode)
        {
            _rwLock.EnterWriteLock();
            try {
                _model?.Dispose();
                _model = null;
                isObbMode = obbMode;
                LoadModel(modelPath);
                
                // 모든 컨텍스트의 캐시를 무효화하여 인덱스 재빌드 유도
                _instanceCtx.TargetCacheKey = "RESET_" + Guid.NewGuid();
                _instanceCtx.EnsureBufferSize(3 * _inputSize * _inputSize);
                _instanceCtx.EnsureInputTensor(_inputSize, _inputNodeName);
                return IsModelLoaded();
            }
            finally { _rwLock.ExitWriteLock(); }
        }

        public InferenceContext CreateContext()
        {
            var ctx = new InferenceContext();
            _rwLock.EnterReadLock();
            try {
                ctx.EnsureBufferSize(3 * _inputSize * _inputSize);
                ctx.EnsureInputTensor(_inputSize);
            }
            finally { _rwLock.ExitReadLock(); }
            return ctx;
        }

        public List<Detection> DetectObjects(Mat frame) => DetectObjects(frame, _instanceCtx);

        public List<Detection> DetectObjects(Mat frame, InferenceContext ctx)
        {
            if (frame == null || frame.Empty()) return new List<Detection>();
            _rwLock.EnterReadLock();
            try {
                if (_model == null) return new List<Detection>();
                ctx.EnsureBufferSize(3 * _inputSize * _inputSize);
                ctx.EnsureInputTensor(_inputSize, _inputNodeName);
                return DetectObjectsCore(frame, ctx, _model, _inputSize);
            }
            catch (Exception ex) { LogCallback?.Invoke($"🚨 추론 에러: {ex.Message}"); return new List<Detection>(); }
            finally { _rwLock.ExitReadLock(); }
        }

        private long _lastLumaLogTime = 0;
        private unsafe (float scale, int padX, int padY) Preprocess(Mat frame, InferenceContext ctx, int targetSize)
        {
            // [LOG] 프레임 휘도(밝기) 체크
            float luma = GetAverageLuma(frame);
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (now - _lastLumaLogTime > 1000) {
                LogCallback?.Invoke($"📸 [Capture] Luma: {luma:F1} {(luma < 5 ? "(⚠️ 블랙 프레임 의심)" : "")}");
                _lastLumaLogTime = now;
            }

            float scale = Math.Min((float)targetSize / frame.Width, (float)targetSize / frame.Height);
            int nW = (int)(frame.Width * scale); int nH = (int)(frame.Height * scale);
            int pX = (targetSize - nW) / 2; int pY = (targetSize - nH) / 2;
            Cv2.Resize(frame, ctx.ResizedMat, new Size(nW, nH));
            Cv2.CopyMakeBorder(ctx.ResizedMat, ctx.PaddedMat, pY, targetSize - nH - pY, pX, targetSize - nW - pX, BorderTypes.Constant, new Scalar(114, 114, 114));
            fixed (float* pBuf = ctx.InputBuffer) { PreprocessUnifiedSimd(ctx.PaddedMat, pBuf, targetSize); }
            return (scale, pX, pY);
        }

        private unsafe float GetAverageLuma(Mat frame)
        {
            if (frame.Empty()) return 0;
            byte* ptr = (byte*)frame.Data;
            long total = 0;
            int step = (int)frame.Step();
            int ch = frame.Channels();
            // 성능을 위해 16x16 그리드 샘플링
            int skipX = Math.Max(1, frame.Width / 16);
            int skipY = Math.Max(1, frame.Height / 16);
            int count = 0;
            for (int y = 0; y < frame.Height; y += skipY) {
                byte* row = ptr + (y * step);
                for (int x = 0; x < frame.Width; x += skipX) {
                    byte* p = row + (x * ch);
                    total += (p[0] + p[1] + p[2]) / 3;
                    count++;
                }
            }
            return count == 0 ? 0 : (float)total / count;
        }

        private List<Detection> DetectObjectsCore(Mat frame, InferenceContext ctx, InferenceSession m, int inputSize)
        {
            LogCallback?.Invoke("⏳ [Debug] AI Preprocess 시작...");
            var (scale, padX, padY) = Preprocess(frame, ctx, inputSize);
            
            LogCallback?.Invoke($"⏳ [Debug] AI 인퍼런스(m.Run) 시작... 코어 연산 중!");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var results = m.Run(ctx.Inputs);
            sw.Stop();
            LogCallback?.Invoke($"✨ [Debug] AI 인퍼런스 완료! (소요시간: {sw.ElapsedMilliseconds}ms)");

            var outputTensor = results.FirstOrDefault(r => r.Name == _outputNodeName)?.AsTensor<float>() ?? results.First().AsTensor<float>();
            
            var detections = Postprocess(outputTensor, scale, padX, padY, frame.Width, frame.Height, ctx, inputSize);
            
            var nmsDetections = ApplyNMS(detections, ctx);
            
            // [LOG] 탐지 통계 (의미 있는 변화 시에만 찍거나 주기적으로 찍으면 좋지만 우선 조건부 로깅)
            if (detections.Count > 0)
                LogCallback?.Invoke($"🔍 [AI] 탐지 시도: Raw={detections.Count}, Filtered={nmsDetections.Count}");

            ctx.TrackBoxes.Clear();
            foreach (var d in nmsDetections) ctx.TrackBoxes.Add(new Rect2d(d.BBox[0], d.BBox[1], d.Width, d.Height));
            var trackedResults = ctx.Tracker.Update(ctx.TrackBoxes);

            ctx.FinalBuffer.Clear();
            ctx.RemainingBuffer.Clear();
            ctx.RemainingBuffer.AddRange(nmsDetections);

            foreach (var track in trackedResults) {
                int bestIndex = -1; double bestDist = double.MaxValue;
                double trackCx = track.box.X + track.box.Width / 2.0;
                double trackCy = track.box.Y + track.box.Height / 2.0;

                for (int i = 0; i < ctx.RemainingBuffer.Count; i++) {
                    var det = ctx.RemainingBuffer[i];
                    double dx = (det.BBox[0] + det.Width / 2.0) - trackCx;
                    double dy = (det.BBox[1] + det.Height / 2.0) - trackCy;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < bestDist) { bestDist = dist; bestIndex = i; }
                }
                if (bestIndex >= 0 && bestDist < 50) {
                    var det = ctx.RemainingBuffer[bestIndex];
                    det.TrackId = track.id;
                    ctx.FinalBuffer.Add(det);
                    ctx.RemainingBuffer.RemoveAt(bestIndex);
                }
            }
            return ctx.FinalBuffer;
        }

        private static unsafe void PreprocessUnifiedSimd(Mat padded, float* dst, int targetSize)
        {
            int planeSize = targetSize * targetSize;
            int width = padded.Width; int height = padded.Height;
            int channels = padded.Channels();
            byte* srcPtr = (byte*)padded.Data; long step = padded.Step();
            float* dR = dst; float* dG = dst + planeSize; float* dB = dst + 2 * planeSize;
            const float inv255 = 1.0f / 255.0f;

            if (channels == 4) {
                for (int y = 0; y < height; y++) {
                    byte* row = srcPtr + y * step; int offset = y * width;
                    for (int x = 0; x < width; x++) {
                        byte* p = row + x * 4;
                        dR[offset + x] = p[2] * inv255; dG[offset + x] = p[1] * inv255; dB[offset + x] = p[0] * inv255;
                    }
                }
            } else {
                for (int y = 0; y < height; y++) {
                    byte* row = srcPtr + y * step; int offset = y * width;
                    for (int x = 0; x < width; x++) {
                        byte* p = row + x * channels;
                        dR[offset + x] = p[2] * inv255; dG[offset + x] = p[1] * inv255; dB[offset + x] = p[0] * inv255;
                    }
                }
            }
        }


        private List<Detection> Postprocess(Tensor<float> output, float scale, int padX, int padY, int oW, int oH, InferenceContext ctx, int inputSize)
        {
            ctx.DetectionBuffer.Clear();
            var dims = output.Dimensions;
            bool transposed = dims.Length == 3 && dims[1] > dims[2];
            int anchors = transposed ? dims[1] : dims[2];
            int features = transposed ? dims[2] : dims[1];
            int classes = isObbMode ? features - 5 : features - 4;

            RebuildTargetIndicesIfNeeded(classes, ctx);
            if (ctx.TargetClassIndices.Length == 0) return ctx.DetectionBuffer;

            ReadOnlySpan<float> flat = (output is DenseTensor<float> dt) ? dt.Buffer.Span : output.ToArray();
            const float R2D = 180.0f / MathF.PI;

            for (int i = 0; i < anchors; i++) {
                float maxS = 0; int maxC = -1;
                foreach (int c in ctx.TargetClassIndices) {
                    float s = transposed ? flat[i * features + 4 + c] : flat[(4 + c) * anchors + i];
                    if (s > maxS) { maxS = s; maxC = c; }
                }
                if (maxS <= ConfThreshold || maxC == -1) continue;

                float cx, cy, w, h;
                if (transposed) { int b = i * features; cx = flat[b]; cy = flat[b + 1]; w = flat[b + 2]; h = flat[b + 3]; }
                else { cx = flat[i]; cy = flat[anchors + i]; w = flat[2 * anchors + i]; h = flat[3 * anchors + i]; }

                float oCx = (cx - padX) / scale; float oCy = (cy - padY) / scale;
                float oW_det = w / scale; float oH_det = h / scale;
                float angle = 0; int x1, y1, x2, y2;

                if (isObbMode) {
                    int aIdx = features - 1; angle = (transposed ? flat[i * features + aIdx] : flat[aIdx * anchors + i]) * R2D;
                    var rr = new RotatedRect(new Point2f(oCx, oCy), new Size2f(oW_det, oH_det), angle);
                    var br = rr.BoundingRect(); x1 = br.Left; y1 = br.Top; x2 = br.Right; y2 = br.Bottom;
                } else {
                    x1 = (int)(oCx - oW_det / 2); y1 = (int)(oCy - oH_det / 2); x2 = (int)(oCx + oW_det / 2); y2 = (int)(oCy + oH_det / 2);
                }

                ctx.DetectionBuffer.Add(new Detection {
                    ClassName = isObbMode ? ModelRegistry.Oriented.GetClassName(maxC) : ModelRegistry.Standard.GetClassName(maxC),
                    Confidence = maxS, BBox = new[] { Math.Max(0, x1), Math.Max(0, y1), Math.Min(oW, x2), Math.Min(oH, y2) },
                    Angle = angle, CenterX = oCx, CenterY = oCy, ObbWidth = oW_det, ObbHeight = oH_det
                });
            }
            return ctx.DetectionBuffer;
        }

        private List<Detection> ApplyNMS(List<Detection> detections, InferenceContext ctx)
        {
            ctx.NmsBuffer.Clear();
            var thresholds = isObbMode ? ModelRegistry.Oriented.NmsThresholds : ModelRegistry.Standard.NmsThresholds;
            foreach (var group in detections.GroupBy(d => d.ClassName)) {
                var ordered = group.OrderByDescending(d => d.Confidence).ToList();
                float thr = thresholds.TryGetValue(group.Key, out float t) ? t : 0.4f;
                while (ordered.Count > 0) {
                    var best = ordered[0]; ctx.NmsBuffer.Add(best);
                    for (int i = ordered.Count - 1; i >= 1; i--)
                        if (CalculateIoU(best.BBox, ordered[i].BBox) >= thr) ordered.RemoveAt(i);
                    ordered.RemoveAt(0);
                }
            }
            return ctx.NmsBuffer;
        }

        private float CalculateIoU(int[] b1, int[] b2) {
            int xA = Math.Max(b1[0], b2[0]); int yA = Math.Max(b1[1], b2[1]);
            int xB = Math.Min(b1[2], b2[2]); int yB = Math.Min(b1[3], b2[3]);
            float inter = Math.Max(0, xB - xA) * Math.Max(0, yB - yA);
            float a1 = (b1[2] - b1[0]) * (b1[3] - b1[1]); float a2 = (b2[2] - b2[0]) * (b2[3] - b2[1]);
            return inter / (a1 + a2 - inter);
        }

        public unsafe void ApplySingleCensorOptimized(Mat frame, Detection det)
        {
            if (isObbMode) {
                var center = new Point2f(det.CenterX, det.CenterY);
                var size = new Size2f(det.ObbWidth, det.ObbHeight);
                var rr = new RotatedRect(center, size, det.Angle);
                Rect br = rr.BoundingRect();
                int x = Math.Max(0, br.X); int y = Math.Max(0, br.Y);
                int w = Math.Min(br.Width, frame.Width - x); int h = Math.Min(br.Height, frame.Height - y);
                if (w <= 0 || h <= 0) return;
                using var region = new Mat(frame, new Rect(x, y, w, h));
                using var effect = region.Clone();
                if (currentCensorType == CensorType.Mosaic) ApplyMosaicInPlace(effect, new Rect(0,0,w,h), strength);
                else if (currentCensorType == CensorType.Blur) Cv2.GaussianBlur(region, effect, new Size(Math.Max(3, strength|1), Math.Max(3, strength|1)), 0);
                else effect.SetTo(new Scalar(0,0,0,255));
                using var mask = new Mat(new Size(w, h), MatType.CV_8UC1, Scalar.All(0));
                var pts = rr.Points().Select(p => new Point((int)Math.Round(p.X - x), (int)Math.Round(p.Y - y))).ToArray();
                Cv2.FillConvexPoly(mask, pts, Scalar.All(255));
                effect.CopyTo(region, mask);
            } else {
                Rect roi = new Rect(det.BBox[0], det.BBox[1], det.Width, det.Height);
                if (currentCensorType == CensorType.Mosaic) ApplyMosaicInPlace(frame, roi, strength);
                else {
                    using var r = new Mat(frame, roi);
                    if (currentCensorType == CensorType.Blur) Cv2.GaussianBlur(r, r, new Size(Math.Max(3, strength|1), Math.Max(3, strength|1)), 0);
                    else r.SetTo(new Scalar(0,0,0,255));
                }
            }
        }

        private static unsafe void ApplyMosaicInPlace(Mat frame, Rect roi, int strength) {
            int x1 = Math.Max(0, roi.X); int y1 = Math.Max(0, roi.Y);
            int x2 = Math.Min(frame.Width, roi.X + roi.Width); int y2 = Math.Min(frame.Height, roi.Y + roi.Height);
            int ch = frame.Channels(); long step = frame.Step(); byte* data = (byte*)frame.Data;
            int bs = Math.Max(2, strength / 2);
            for (int y = y1; y < y2; y += bs) {
                int bh = Math.Min(bs, y2 - y);
                for (int x = x1; x < x2; x += bs) {
                    int bw = Math.Min(bs, x2 - x);
                    byte* f = data + y * step + x * ch;
                    byte b = f[0], g = f[1], r = f[2], a = ch == 4 ? f[3] : (byte)255;
                    for (int py = 0; py < bh; py++) {
                        byte* row = data + (y + py) * step + x * ch;
                        for (int px = 0; px < bw; px++) {
                            byte* p = row + px * ch; p[0] = b; p[1] = g; p[2] = r; if (ch == 4) p[3] = a;
                        }
                    }
                }
            }
        }

        private void RebuildTargetIndicesIfNeeded(int numClasses, InferenceContext ctx) {
            string key = string.Join(",", Targets) + (isObbMode ? ":obb" : ":hbb");
            if (key == ctx.TargetCacheKey) return;
            ctx.TargetCacheKey = key;
            var indices = new List<int>();
            for (int c = 0; c < numClasses; c++) {
                string n = isObbMode ? ModelRegistry.Oriented.GetClassName(c) : ModelRegistry.Standard.GetClassName(c);
                if (n != null && Targets.Contains(n)) indices.Add(c);
            }
            ctx.TargetClassIndices = indices.ToArray();
        }

        public void SetTargets(List<string> t) => Targets = t ?? new List<string>();
        public void SetStrength(int s) => strength = Math.Max(5, Math.Min(50, s));
        public void SetCensorType(CensorType t) => currentCensorType = t;

        public void WarmUpModel() {
            _rwLock.EnterReadLock();
            try {
                if (_model == null) return;
                var dummy = new DenseTensor<float>(new float[1 * 3 * _inputSize * _inputSize], new[] { 1, 3, _inputSize, _inputSize });
                using (_model.Run(new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", dummy) })) { }
            } catch {} finally { _rwLock.ExitReadLock(); }
        }

        public void Dispose() {
            _rwLock.EnterWriteLock();
            try { _model?.Dispose(); _model = null; _instanceCtx?.Dispose(); }
            finally { _rwLock.ExitWriteLock(); _rwLock.Dispose(); }
        }
    }

    public static class Rect2dExtensions {
        public static double DistanceTo(this Rect2d r1, Rect2d r2) {
            double dx = (r1.X + r1.Width/2) - (r2.X + r2.Width/2);
            double dy = (r1.Y + r1.Height/2) - (r2.Y + r2.Height/2);
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
