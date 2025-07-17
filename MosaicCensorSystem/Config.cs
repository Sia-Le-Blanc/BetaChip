using System.Collections.Generic;

namespace MosaicCensorSystem
{
    /// <summary>
    /// 애플리케이션의 기본 설정을 관리하는 단순화된 정적 클래스
    /// </summary>
    public static class Config
    {
        // --- Mosaic/Blur Settings ---
        public const int DefaultStrength = 20;
        public const float DefaultConfidence = 0.3f;
        public static readonly List<string> DefaultTargets = new() { "얼굴", "가슴", "보지", "팬티" };

        // --- Performance Settings ---
        public const int DefaultFps = 15;
        public const double DownscaleRatio = 1.0; // 1.0 = 원본 해상도
        
        // --- Overlay Settings ---
        public const bool ShowDebugInfo = false;
    }
}