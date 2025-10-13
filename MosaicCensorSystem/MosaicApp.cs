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
            Root = new Form { Text = "Mosaic Censor System (Sticker-Ready)", Size = new Size(500, 800), MinimumSize = new Size(480, 650), StartPosition = FormStartPosition.CenterScreen };
            
            // ★★★ 수정된 부분 ★★★
            Root.FormClosing += (s, e) => {
                // 리소스를 해제하기 전에, 모든 백그라운드 동작을 명시적으로 중지시킵니다.
                // 이렇게 하면 스레드가 안전하게 종료된 후 리소스 해제가 진행됩니다.
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
            uiController.StickerToggled += (val) => censorService.UpdateSetting("EnableStickers", val);
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