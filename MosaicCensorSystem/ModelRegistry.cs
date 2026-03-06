using System.Collections.Generic;

namespace MosaicCensorSystem
{
    /// <summary>
    /// 단일 AI 모델의 메타데이터를 정의합니다.
    /// </summary>
    public sealed class ModelDefinition
    {
        public string FileName { get; init; }
        public bool IsObb { get; init; }

        /// <summary>
        /// 모델이 출력하는 클래스 이름 목록 (인덱스 순서 엄수).
        /// 인덱스 → 이름 매핑의 단일 진실 공급원입니다.
        /// </summary>
        public string[] Classes { get; init; }

        /// <summary>
        /// 프로그램 최초 실행 시 기본으로 체크되는 타겟 목록.
        /// </summary>
        public string[] DefaultTargets { get; init; }

        /// <summary>
        /// 클래스별 NMS IoU 임계값 (미지정 클래스는 0.45f 기본값 사용).
        /// </summary>
        public IReadOnlyDictionary<string, float> NmsThresholds { get; init; }

        /// <summary>
        /// 주어진 인덱스에 해당하는 클래스 이름을 반환합니다.
        /// 범위를 벗어나면 null을 반환합니다.
        /// </summary>
        public string GetClassName(int index) =>
            index >= 0 && index < Classes.Length ? Classes[index] : null;
    }

    /// <summary>
    /// 프로젝트에서 사용하는 AI 모델 정의의 레지스트리.
    /// 인덱스 기반 클래스 매핑의 단일 진실 공급원이므로, 클래스 순서를 절대 변경하지 마십시오.
    /// </summary>
    public static class ModelRegistry
    {
        /// <summary>
        /// 표준 HBB 모델 (best.onnx) — 14개 클래스, 한국어 내부명.
        /// </summary>
        public static readonly ModelDefinition Standard = new()
        {
            FileName = @"Resources\best.onnx",
            IsObb = false,
            Classes = new[]
            {
                "얼굴",     // 0
                "가슴",     // 1
                "겨드랑이", // 2
                "보지",     // 3
                "발",       // 4
                "몸 전체",  // 5
                "자지",     // 6
                "팬티",     // 7
                "눈",       // 8
                "손",       // 9
                "교미",     // 10
                "신발",     // 11
                "가슴_옷",  // 12
                "여성"      // 13
            },
            DefaultTargets = new[] { "얼굴", "가슴", "보지", "팬티" },
            NmsThresholds = new Dictionary<string, float>
            {
                ["얼굴"] = 0.4f,
                ["가슴"] = 0.4f,
                ["보지"] = 0.4f
            }
        };

        /// <summary>
        /// 정밀 OBB 모델 (bestobb.onnx) — 20개 클래스, 영어 정규명 (순서 엄수).
        /// </summary>
        public static readonly ModelDefinition Oriented = new()
        {
            FileName = @"Resources\bestobb.onnx",
            IsObb = true,
            Classes = new[]
            {
                "Face_Female",      // 0
                "Face_Male",        // 1
                "Eyes",             // 2
                "Breast_Nude",      // 3
                "Breast_Underwear", // 4
                "Breast_Clothed",   // 5
                "Armpit",           // 6
                "Navel",            // 7
                "Penis",            // 8
                "Vulva_Nude",       // 9
                "Butt_Nude",        // 10
                "Panty",            // 11
                "Butt_Clothed",     // 12
                "Hands",            // 13
                "Feet",             // 14
                "Shoes",            // 15
                "Body_Full",        // 16
                "Anus",             // 17
                "Sex_Act",          // 18
                "Hpis"              // 19
            },
            DefaultTargets = new[] { "Face_Female", "Breast_Nude", "Vulva_Nude", "Panty" },
            NmsThresholds = new Dictionary<string, float>
            {
                ["Face_Female"] = 0.4f,
                ["Face_Male"]   = 0.4f,
                ["Breast_Nude"] = 0.4f,
                ["Vulva_Nude"]  = 0.4f
            }
        };
    }
}
