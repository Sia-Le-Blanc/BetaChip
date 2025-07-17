#nullable disable
using System;
using System.Drawing;
using System.Windows.Forms;
using MosaicCensorSystem.UI;

namespace MosaicCensorSystem
{
    /// <summary>
    /// 애플리케이션의 메인 클래스. 컴포넌트들을 초기화하고 연결합니다.
    /// </summary>
    public class MosaicApp
    {
        public readonly Form Root;
        private readonly GuiController uiController;
        private readonly CensorService censorService;

        public MosaicApp()
        {
            // 1. 메인 폼 생성
            Root = new Form
            {
                Text = "Mosaic Censor System (Refactored)",
                Size = new Size(500, 750),
                MinimumSize = new Size(480, 600),
                StartPosition = FormStartPosition.CenterScreen
            };
            Root.FormClosing += (s, e) => censorService.Dispose();

            // 2. UI 컨트롤러와 서비스 초기화
            uiController = new GuiController(Root);
            censorService = new CensorService(uiController);

            // 3. UI 이벤트와 서비스 로직 연결
            ConnectEvents();

            uiController.LogMessage("✅ 시스템 초기화 완료. 시작 버튼을 누르세요.");
        }

        private void ConnectEvents()
        {
            uiController.StartClicked += censorService.Start;
            uiController.StopClicked += censorService.Stop;
            uiController.TestCaptureClicked += censorService.TestCapture;

            uiController.FpsChanged += (fps) => censorService.UpdateSetting("TargetFPS", fps);
            uiController.DetectionToggled += (val) => censorService.UpdateSetting("EnableDetection", val);
            uiController.CensoringToggled += (val) => censorService.UpdateSetting("EnableCensoring", val);
            uiController.CensorTypeChanged += (type) => censorService.UpdateSetting("CensorType", type);
            uiController.StrengthChanged += (val) => censorService.UpdateSetting("Strength", val);
            uiController.ConfidenceChanged += (val) => censorService.UpdateSetting("Confidence", val);
            uiController.TargetsChanged += (targets) => censorService.UpdateSetting("Targets", targets);
        }

        public void Run()
        {
            Application.Run(Root);
        }
    }
}