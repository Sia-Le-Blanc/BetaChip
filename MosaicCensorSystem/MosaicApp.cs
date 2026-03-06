#nullable disable
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks; 
using MosaicCensorSystem.UI;
using MosaicCensorSystem.Services; 
using MosaicCensorSystem.Models;   
using MosaicCensorSystem.Detection; // OBB 타겟 클래스 참조를 위해 추가

namespace MosaicCensorSystem
{
    public class MosaicApp
    {
        public readonly Form Root;
        private readonly GuiController uiController;
        private CensorService censorService;
        private readonly ApiService _apiService = new ApiService();
        private SubscriptionInfo _subInfo;

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
            _subInfo = await _apiService.GetSubscriptionAsync(userId);

            if (_subInfo == null)
            {
                _subInfo = new SubscriptionInfo { Tier = "free", Email = "Offline Mode" };
                uiController.LogMessage("⚠️ 서버 연결 실패. 무료 버전으로 시작합니다.");
            }
            else
            {
                uiController.LogMessage($"✅ 로그인 성공: {_subInfo.Email} ([{_subInfo.Tier.ToUpper()}] 등급)");
            }

            // 구독 정보를 전달하며 서비스 초기화
            censorService = new CensorService(uiController, _subInfo);

            ConnectEvents();
            uiController.UpdateGpuStatus(censorService.Processor.CurrentExecutionProvider);
            Root.Text = $"Mosaic Censor System - {_subInfo.Tier.ToUpper()} Edition";

            // OBB 모델 파일 존재 여부를 확인하여 라디오 버튼 활성/비활성화
            if (string.IsNullOrEmpty(Program.OBB_MODEL_PATH))
            {
                uiController.LogMessage("⚠️ OBB 모델 파일(bestobb.onnx)을 찾을 수 없습니다. 정밀 모드를 비활성화합니다.");
                uiController.SetObbModelAvailable(false);
            }
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
            
            // ★ 핵심: 모델 교체 시 런타임 핫스왑 처리 및 UI 타겟 체크박스 동적 재구성
            uiController.ModelTypeChanged += (isObb) =>
            {
                // ModelRegistry를 통해 모델 정의를 가져옵니다.
                var modelDef = isObb ? ModelRegistry.Oriented : ModelRegistry.Standard;
                string newModelPath = isObb ? Program.OBB_MODEL_PATH : Program.STANDARD_MODEL_PATH;

                // ── 진입 진단 로그 ──────────────────────────────────────────
                Console.WriteLine($"[SwitchModel] 진입: isObb={isObb}, path={newModelPath}");
                Console.WriteLine($"[SwitchModel] 구독 티어: {_subInfo?.Tier ?? "(null)"}");
#if DEBUG
                Console.WriteLine($"[SwitchModel] IsDevelopmentMode: {Config.IsDevelopmentMode}");
#endif
                // ────────────────────────────────────────────────────────────

                uiController.LogMessage($"🔄 모델 교체 중... ({(isObb ? "OBB 정밀 모델" : "표준 모델")})");

                // processor의 모델을 실시간으로 교체
                bool success = censorService.Processor.SwitchModel(newModelPath, isObb);

                if (success)
                {
                    uiController.LogMessage("✅ 모델 교체 완료!");
                    // ModelRegistry의 클래스 목록으로 UI 체크박스를 재구성합니다.
                    // 이름이 일치하는 항목은 체크 상태가 자동으로 유지됩니다.
                    uiController.RebuildTargetCheckboxes(modelDef.Classes);
                }
                else
                {
                    uiController.LogMessage("❌ 모델 교체 실패! 파일 경로를 확인하세요.");
                }
            };
        }

        public void Run()
        {
            Application.Run(Root);
        }
    }
}