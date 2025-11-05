#nullable disable
using System;
using System.Drawing;
using System.Windows.Forms;
using MosaicCensorSystem.UI;

namespace MosaicCensorSystem
{
    public class MosaicApp
    {
        public readonly Form Root;
        private readonly GuiController uiController;
        private readonly CensorService censorService;

        public MosaicApp()
        {
            Root = new Form 
            { 
                Text = "Mosaic Censor System", 
                Size = new Size(500, 850),  // ★ 높이 증가 (캡션 체크박스 추가로)
                MinimumSize = new Size(480, 700), 
                StartPosition = FormStartPosition.CenterScreen 
            };
            
            Root.FormClosing += (s, e) => 
            {
                censorService.Stop(); 
                censorService.Dispose();
                uiController.Dispose();
            };

            uiController = new GuiController(Root);
            censorService = new CensorService(uiController);
            ConnectEvents();
            uiController.LogMessage("✅ 시스템 초기화 완료. 시작 버튼을 누르세요.");
            uiController.UpdateGpuStatus(censorService.Processor.CurrentExecutionProvider);
        }

        private void ConnectEvents()
        {
            uiController.StartClicked += censorService.Start;
            uiController.StopClicked += censorService.Stop;
            uiController.CaptureAndSaveClicked += censorService.CaptureAndSave;
            
#if PATREON_VERSION
            uiController.StickerToggled += (val) => censorService.UpdateSetting("EnableStickers", val);
#endif

#if PATREON_PLUS_VERSION
            // ★ 캡션 토글 이벤트 연결
            uiController.CaptionToggled += (val) => censorService.UpdateSetting("EnableCaptions", val);
#endif
            
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