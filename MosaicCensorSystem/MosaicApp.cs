#nullable disable
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks; // 추가됨
using MosaicCensorSystem.UI;
using MosaicCensorSystem.Services; // 추가됨
using MosaicCensorSystem.Models;   // 추가됨
using MosaicCensorSystem.Detection; // 추가됨

namespace MosaicCensorSystem
{
    public class MosaicApp
    {
        public readonly Form Root;
        private readonly GuiController uiController;
        private CensorService censorService; // readonly 제거
        private readonly ApiService _apiService = new ApiService(); // 추가됨

        public MosaicApp()
        {
            Root = new Form
            {
                Text = "Mosaic Censor System (Checking License...)",
                Size = new Size(500, 850),
                MinimumSize = new Size(480, 700),
                StartPosition = FormStartPosition.CenterScreen
            };

            uiController = new GuiController(Root);

            // 앱 로드 시 서버에서 라이선스 정보 가져오기
            Root.Load += async (s, e) => await InitializeLicenseAndService();

            Root.FormClosing += (s, e) =>
            {
                censorService?.Stop();
                censorService?.Dispose();
                uiController.Dispose();
            };
        }

        private async Task InitializeLicenseAndService()
        {
            uiController.LogMessage("🔍 라이선스 정보를 확인 중입니다...");

            // 실제로는 로그인한 유저의 ID를 사용해야 하지만, 현재 테스트용 UID를 사용합니다.
            var userId = "4e222613-7a83-4063-b717-d7e06bed0122"; 
            var subInfo = await _apiService.GetSubscriptionAsync(userId);

            if (subInfo == null)
            {
                subInfo = new SubscriptionInfo { Tier = "free", Email = "Offline Mode" };
                uiController.LogMessage("⚠️ 서버 연결 실패. 무료 버전으로 시작합니다.");
            }
            else
            {
                uiController.LogMessage($"✅ 로그인 성공: {subInfo.Email} ([{subInfo.Tier.ToUpper()}] 등급)");
            }

            // 구독 정보를 전달하며 서비스 초기화
            censorService = new CensorService(uiController, subInfo);
            
            ConnectEvents();
            uiController.UpdateGpuStatus(censorService.Processor.CurrentExecutionProvider);
            Root.Text = $"Mosaic Censor System - {subInfo.Tier.ToUpper()} Edition";
        }

        private void ConnectEvents()
        {
            uiController.StartClicked += censorService.Start;
            uiController.StopClicked += censorService.Stop;
            uiController.CaptureAndSaveClicked += censorService.CaptureAndSave;
            
            // 등급 정보에 따라 이벤트 연결 (등급별 기능 제한은 CensorService 내부 로직에서 처리함)
            uiController.StickerToggled += (val) => censorService.UpdateSetting("EnableStickers", val);
            uiController.CaptionToggled += (val) => censorService.UpdateSetting("EnableCaptions", val);
            
            uiController.FpsChanged += (fps) => censorService.UpdateSetting("TargetFPS", fps);
            uiController.DetectionToggled += (val) => censorService.UpdateSetting("EnableDetection", val);
            uiController.CensoringToggled += (val) => censorService.UpdateSetting("EnableCensoring", val);
            uiController.CensorTypeChanged += (type) => censorService.UpdateSetting("CensorType", type);
            uiController.StrengthChanged += (val) => censorService.UpdateSetting("Strength", val);
            uiController.ConfidenceChanged += (val) => censorService.UpdateSetting("Confidence", val);
            uiController.TargetsChanged += (targets) => censorService.UpdateSetting("Targets", targets);
            uiController.GpuSetupClicked += () =>
            {
                var gpuResult = Helpers.GpuDetector.Detect();
                using var gpuForm = new UI.GpuSetupForm(gpuResult);
                gpuForm.ShowDialog();
            };
            uiController.ModelTypeChanged += (isObb) =>
            {
                string newModelPath = isObb ? Program.OBB_MODEL_PATH : Program.STANDARD_MODEL_PATH;
                uiController.LogMessage($"🔄 모델 교체 중... ({(isObb ? "OBB 정밀 모델" : "표준 모델")})");

                bool success = censorService.Processor.SwitchModel(newModelPath, isObb);

                if (success)
                {
                    uiController.LogMessage("✅ 모델 교체 완료!");
                    // UI에 OBB용/HBB용 클래스 리스트를 전달하여 체크박스를 동적으로 재생성함
                    uiController.RebuildTargetCheckboxes(isObb ? MosaicProcessor.ObbUniqueTargets : MosaicProcessor.HbbClasses);
                }
                else uiController.LogMessage("❌ 모델 교체 실패! 경로를 확인하세요.");
            };

            // 앱 시작 시 초기 HBB 타겟 목록으로 체크박스를 구성하고 processor.Targets에 동기화
            uiController.RebuildTargetCheckboxes(MosaicProcessor.HbbClasses);
        }

        public void Run()
        {
            Application.Run(Root);
        }
    }
}