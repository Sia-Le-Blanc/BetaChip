using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace MosaicCensorSystem.Detection
{
    /// <summary>
    /// 검열 처리기의 핵심 기능을 정의하는 단순화된 인터페이스
    /// </summary>
    public interface IProcessor : IDisposable
    {
        /// <summary>
        /// ONNX 모델이 성공적으로 로드되었는지 확인합니다.
        /// </summary>
        /// <returns>모델 로드 여부</returns>
        bool IsModelLoaded();

        /// <summary>
        /// 감지에 사용할 신뢰도 임계값을 설정하거나 가져옵니다.
        /// </summary>
        float ConfThreshold { get; set; }

        /// <summary>
        /// 검열할 객체 클래스 목록을 설정합니다.
        /// </summary>
        /// <param name="targets">클래스 이름 목록</param>
        void SetTargets(List<string> targets);

        /// <summary>
        /// 검열 강도를 설정합니다. (모자이크 크기 또는 블러 강도)
        /// </summary>
        /// <param name="strength">검열 강도</param>
        void SetStrength(int strength);

        /// <summary>
        /// 검열 타입을 설정합니다. (모자이크 또는 블러)
        /// </summary>
        /// <param name="censorType">검열 타입</param>
        void SetCensorType(CensorType censorType);

        /// <summary>
        /// 프레임에서 객체를 감지하여 결과 목록을 반환합니다.
        /// </summary>
        /// <param name="frame">입력 프레임 (Mat)</param>
        /// <returns>감지된 객체 목록</returns>
        List<Detection> DetectObjects(Mat frame);

        /// <summary>
        /// 감지된 단일 객체에 대해 검열 효과를 적용합니다.
        /// </summary>
        /// <param name="frame">검열을 적용할 프레임</param>
        /// <param name="detection">감지된 객체 정보</param>
        void ApplySingleCensorOptimized(Mat frame, Detection detection);
    }
}