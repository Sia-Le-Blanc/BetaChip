using System;
using OpenCvSharp;

namespace MosaicCensorSystem.Overlay
{
    /// <summary>
    /// 오버레이 윈도우의 핵심 기능을 정의하는 인터페이스 (단순화 버전)
    /// </summary>
    public interface IOverlay : IDisposable
    {
        /// <summary>
        /// 오버레이 윈도우를 화면에 표시합니다.
        /// </summary>
        void Show();

        /// <summary>
        /// 오버레이 윈도우를 화면에서 숨깁니다.
        /// </summary>
        void Hide();

        /// <summary>
        /// 오버레이에 표시할 새 프레임(이미지)을 업데이트합니다.
        /// </summary>
        /// <param name="processedFrame">화면에 그릴 Mat 이미지</param>
        void UpdateFrame(Mat processedFrame);
    }
}