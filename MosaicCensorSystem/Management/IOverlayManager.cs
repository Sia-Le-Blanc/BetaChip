// IOverlayManager.cs
using MosaicCensorSystem.UI;
using OpenCvSharp;
using System;

namespace MosaicCensorSystem.Management
{
    /// <summary>
    /// 무료/후원자 버전의 오버레이 관리를 위한 공통 인터페이스
    /// </summary>
    public interface IOverlayManager : IDisposable
    {
        /// <summary>
        /// 관리자를 초기화합니다.
        /// </summary>
        void Initialize(GuiController ui);

        /// <summary>
        /// 오버레이 및 프레임 처리를 시작합니다.
        /// </summary>
        /// <param name="frameProcessor">프레임을 받아 처리 후 반환하는 함수</param>
        void Start(Func<Mat, Mat> frameProcessor);

        /// <summary>
        /// 오버레이 및 프레임 처리를 중지합니다.
        /// </summary>
        void Stop();

        /// <summary>
        /// 실시간 설정을 업데이트합니다.
        /// </summary>
        void UpdateSettings(CensorSettings settings);
    }

    /// <summary>
    /// 실시간 설정을 전달하기 위한 데이터 구조
    /// </summary>
    public record CensorSettings(bool EnableDetection, bool EnableCensoring, bool EnableStickers, int TargetFPS);
}