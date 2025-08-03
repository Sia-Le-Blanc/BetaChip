#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Threading;
using System.Windows.Forms;
using MosaicCensorSystem.Detection;

namespace MosaicCensorSystem.UI
{
    public class GuiController
    {
        // --- Events ---
        public event Action<int> FpsChanged;
        public event Action<bool> DetectionToggled;
        public event Action<bool> CensoringToggled;
        public event Action<bool> StickerToggled; // 스티커 토글 이벤트 추가
        public event Action<CensorType> CensorTypeChanged;
        public event Action<int> StrengthChanged;
        public event Action<float> ConfidenceChanged;
        public event Action StartClicked;
        public event Action StopClicked;
        public event Action TestCaptureClicked;
        public event Action<List<string>> TargetsChanged;

        // --- UI 컨트롤들을 멤버 변수로 선언 ---
        private readonly Form rootForm;
        private Label titleLabel;
        private ComboBox languageComboBox;
        private Label statusLabel;
        private TextBox logTextBox;
        private Button startButton, stopButton, testButton;
        private GroupBox controlGroup, settingsGroup, logGroup, targetsGroup;
        private Label gpuStatusLabel;
        private Label fpsLabel, strengthLabel, confidenceLabel;
        private CheckBox enableDetectionCheckBox, enableCensoringCheckBox, enableStickersCheckBox; // 스티커 체크박스 추가
        private RadioButton mosaicRadioButton, blurRadioButton;
        private readonly Dictionary<string, CheckBox> targetCheckBoxes = new Dictionary<string, CheckBox>();

        // 리소스 매니저
        private ResourceManager resourceManager;
        private string currentGpuStatus = "CPU"; // 현재 GPU 상태 저장

        public GuiController(Form mainForm)
        {
            rootForm = mainForm;
            // 리소스 매니저 초기화
            resourceManager = new ResourceManager("MosaicCensorSystem.Properties.Strings", typeof(GuiController).Assembly);
            CreateGui();
            // 프로그램 시작 시 기본 언어(한국어)로 UI 텍스트를 설정합니다.
            UpdateUIText(); 
        }

        private void CreateGui()
        {
            rootForm.SuspendLayout();
            
            titleLabel = new Label { 
                Font = new Font("Arial", 12, FontStyle.Bold), 
                BackColor = Color.LightSkyBlue, 
                BorderStyle = BorderStyle.FixedSingle, 
                TextAlign = ContentAlignment.MiddleCenter, 
                Height = 40, 
                Dock = DockStyle.Top 
            };
            
            // 언어 변경 콤보박스 생성 (더 직관적으로 개선)
            languageComboBox = new ComboBox { 
                Location = new Point(350, 5), 
                DropDownStyle = ComboBoxStyle.DropDownList,
                Size = new Size(100, 25),
                Font = new Font("Arial", 9, FontStyle.Bold)
            };
            languageComboBox.Items.AddRange(new string[] { "🇰🇷 한국어", "🇺🇸 English" });
            languageComboBox.SelectedIndex = 0; // 기본값을 '한국어'로 설정
            languageComboBox.SelectedIndexChanged += OnLanguageChanged;
            titleLabel.Controls.Add(languageComboBox);

            var scrollableContainer = new ScrollablePanel { Dock = DockStyle.Fill };
            rootForm.Controls.Add(scrollableContainer);
            rootForm.Controls.Add(titleLabel);
            
            CreateContent(scrollableContainer.ScrollableFrame);
            
            rootForm.ResumeLayout(false);
        }

        private void CreateContent(Panel parent)
        {
            int y = 10;
            statusLabel = new Label { 
                Font = new Font("Arial", 12, FontStyle.Bold), 
                ForeColor = Color.Red, 
                Location = new Point(10, y), 
                AutoSize = true 
            };
            parent.Controls.Add(statusLabel);
            y += 40;
            
            gpuStatusLabel = new Label { 
                Font = new Font("Arial", 10), 
                Location = new Point(10, y), 
                AutoSize = true 
            };
            parent.Controls.Add(gpuStatusLabel);
            y += 30;

            controlGroup = new GroupBox { Location = new Point(10, y), Size = new Size(460, 80) };
            startButton = new Button { 
                BackColor = Color.DarkGreen, 
                ForeColor = Color.White, 
                Font = new Font("Arial", 10, FontStyle.Bold), 
                Size = new Size(120, 40), 
                Location = new Point(20, 25) 
            };
            stopButton = new Button { 
                BackColor = Color.DarkRed, 
                ForeColor = Color.White, 
                Font = new Font("Arial", 10, FontStyle.Bold), 
                Size = new Size(120, 40), 
                Location = new Point(160, 25), 
                Enabled = false 
            };
            testButton = new Button { 
                BackColor = Color.DarkBlue, 
                ForeColor = Color.White, 
                Font = new Font("Arial", 10, FontStyle.Bold), 
                Size = new Size(120, 40), 
                Location = new Point(300, 25) 
            };
            startButton.Click += (s, e) => StartClicked?.Invoke();
            stopButton.Click += (s, e) => StopClicked?.Invoke();
            testButton.Click += (s, e) => TestCaptureClicked?.Invoke();
            controlGroup.Controls.AddRange(new Control[] { startButton, stopButton, testButton });
            parent.Controls.Add(controlGroup);
            y += 90;

            settingsGroup = new GroupBox { Location = new Point(10, y), Size = new Size(460, 380) }; // 높이 증가
            CreateSettingsContent(settingsGroup);
            parent.Controls.Add(settingsGroup);
            y += 390;

            logGroup = new GroupBox { Location = new Point(10, y), Size = new Size(460, 120) };
            logTextBox = new TextBox { 
                Multiline = true, 
                ScrollBars = ScrollBars.Vertical, 
                ReadOnly = true, 
                Location = new Point(10, 20), 
                Size = new Size(440, 90), 
                Font = new Font("Consolas", 8.25f) 
            };
            logGroup.Controls.Add(logTextBox);
            parent.Controls.Add(logGroup);
        }
        
        private void CreateSettingsContent(GroupBox settingsGroup)
        {
            int y = 25;
            
            // FPS 설정
            var fpsValueLabel = new Label { Text = "15", Location = new Point(390, y), AutoSize = true };
            var fpsSlider = new TrackBar { 
                Minimum = 5, Maximum = 240, Value = 15, TickFrequency = 5, 
                Location = new Point(100, y - 5), Size = new Size(280, 45) 
            };
            fpsSlider.ValueChanged += (s, e) => { 
                fpsValueLabel.Text = fpsSlider.Value.ToString(); 
                FpsChanged?.Invoke(fpsSlider.Value); 
            };
            fpsLabel = new Label { Location = new Point(10, y), AutoSize = true };
            settingsGroup.Controls.AddRange(new Control[] { fpsLabel, fpsSlider, fpsValueLabel });
            y += 40;

            // 감지 및 검열 활성화 체크박스
            enableDetectionCheckBox = new CheckBox { 
                Checked = true, 
                Location = new Point(10, y), 
                AutoSize = true 
            };
            enableDetectionCheckBox.CheckedChanged += (s, e) => DetectionToggled?.Invoke(enableDetectionCheckBox.Checked);
            
            enableCensoringCheckBox = new CheckBox { 
                Checked = true, 
                Location = new Point(200, y), 
                AutoSize = true 
            };
            enableCensoringCheckBox.CheckedChanged += (s, e) => CensoringToggled?.Invoke(enableCensoringCheckBox.Checked);
            
            settingsGroup.Controls.AddRange(new Control[] { enableDetectionCheckBox, enableCensoringCheckBox });
            y += 30;

            // 스티커 활성화 체크박스 추가
            enableStickersCheckBox = new CheckBox { 
                Checked = false, 
                Location = new Point(10, y), 
                AutoSize = true,
                Text = "스티커 활성화" // 기본 텍스트, UpdateUIText에서 변경됨
            };
            enableStickersCheckBox.CheckedChanged += (s, e) => StickerToggled?.Invoke(enableStickersCheckBox.Checked);
            settingsGroup.Controls.Add(enableStickersCheckBox);
            y += 30;

            // 검열 타입 라디오 버튼
            mosaicRadioButton = new RadioButton { 
                Checked = true, 
                Location = new Point(10, y), 
                AutoSize = true 
            };
            blurRadioButton = new RadioButton { 
                Location = new Point(150, y), 
                AutoSize = true 
            };
            EventHandler censorTypeHandler = (s, e) => { 
                if (s is RadioButton rb && rb.Checked) { 
                    CensorTypeChanged?.Invoke(mosaicRadioButton.Checked ? CensorType.Mosaic : CensorType.Blur); 
                } 
            };
            mosaicRadioButton.CheckedChanged += censorTypeHandler;
            blurRadioButton.CheckedChanged += censorTypeHandler;
            settingsGroup.Controls.AddRange(new Control[] { mosaicRadioButton, blurRadioButton });
            y += 30;

            // 검열 강도 설정
            var strengthValueLabel = new Label { Text = "20", Location = new Point(390, y), AutoSize = true };
            var strengthSlider = new TrackBar { 
                Minimum = 10, Maximum = 40, Value = 20, TickFrequency = 5, 
                Location = new Point(100, y - 5), Size = new Size(280, 45) 
            };
            strengthSlider.ValueChanged += (s, e) => { 
                strengthValueLabel.Text = strengthSlider.Value.ToString(); 
                StrengthChanged?.Invoke(strengthSlider.Value); 
            };
            strengthLabel = new Label { Location = new Point(10, y), AutoSize = true };
            settingsGroup.Controls.AddRange(new Control[] { strengthLabel, strengthSlider, strengthValueLabel });
            y += 40;

            // 신뢰도 설정
            var confidenceValueLabel = new Label { Text = "0.3", Location = new Point(390, y), AutoSize = true };
            var confidenceSlider = new TrackBar { 
                Minimum = 10, Maximum = 90, Value = 30, TickFrequency = 10, 
                Location = new Point(100, y - 5), Size = new Size(280, 45) 
            };
            confidenceSlider.ValueChanged += (s, e) => { 
                float val = confidenceSlider.Value / 100.0f; 
                confidenceValueLabel.Text = val.ToString("F1"); 
                ConfidenceChanged?.Invoke(val); 
            };
            confidenceLabel = new Label { Location = new Point(10, y), AutoSize = true };
            settingsGroup.Controls.AddRange(new Control[] { confidenceLabel, confidenceSlider, confidenceValueLabel });
            y += 40;
            
            // 타겟 선택 그룹
            targetsGroup = new GroupBox { Location = new Point(10, y), Size = new Size(440, 130) };
            var allTargets = new[] { "얼굴", "가슴", "겨드랑이", "보지", "발", "몸 전체", "자지", "팬티", "눈", "손", "교미", "신발", "가슴_옷", "여성" };
            var defaultTargets = new[] { "얼굴", "가슴", "보지", "팬티" };
            for (int i = 0; i < allTargets.Length; i++)
            {
                var checkbox = new CheckBox { 
                    Text = allTargets[i], // 초기 텍스트 (UpdateUIText에서 번역됨)
                    Checked = defaultTargets.Contains(allTargets[i]), 
                    Location = new Point(15 + (i % 3) * 140, 25 + (i / 3) * 20), 
                    AutoSize = true,
                    Tag = allTargets[i] // 원본 키를 Tag에 저장
                };
                checkbox.CheckedChanged += OnTargetChanged;
                targetCheckBoxes[allTargets[i]] = checkbox;
                targetsGroup.Controls.Add(checkbox);
            }
            settingsGroup.Controls.Add(targetsGroup);
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            // 콤보박스에서 선택된 언어에 따라 문화권 정보를 변경합니다.
            string culture = languageComboBox.SelectedIndex == 0 ? "ko-KR" : "en-US";
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
            
            // 변경된 문화권에 맞춰 UI 텍스트를 다시 로드합니다.
            UpdateUIText();
        }

        // UI 텍스트를 리소스 파일에서 다시 로드하는 메서드
        private void UpdateUIText()
        {
            try
            {
                // 리소스에서 텍스트를 가져와서 UI에 적용
                rootForm.Text = GetLocalizedString("AppTitle");
                titleLabel.Text = GetLocalizedString("AppTitle");

                controlGroup.Text = GetLocalizedString("GroupControls");
                startButton.Text = GetLocalizedString("ButtonStart");
                stopButton.Text = GetLocalizedString("ButtonStop");
                testButton.Text = GetLocalizedString("ButtonTest");
                
                settingsGroup.Text = GetLocalizedString("GroupSettings");
                fpsLabel.Text = GetLocalizedString("LabelFps");
                enableDetectionCheckBox.Text = GetLocalizedString("LabelDetection");
                enableCensoringCheckBox.Text = GetLocalizedString("LabelEffect");
                enableStickersCheckBox.Text = GetLocalizedString("LabelStickers");
                mosaicRadioButton.Text = GetLocalizedString("LabelCensorTypeMosaic");
                blurRadioButton.Text = GetLocalizedString("LabelCensorTypeBlur");
                strengthLabel.Text = GetLocalizedString("LabelCensorStrength");
                confidenceLabel.Text = GetLocalizedString("LabelConfidence");
                targetsGroup.Text = GetLocalizedString("GroupTargets");
                
                // 체크박스 텍스트들도 번역
                foreach (var kvp in targetCheckBoxes)
                {
                    var checkbox = kvp.Value;
                    var originalKey = (string)checkbox.Tag ?? kvp.Key; // Tag에서 원본 키 가져오기
                    checkbox.Text = GetLocalizedString($"Target_{originalKey}");
                }
                
                logGroup.Text = GetLocalizedString("GroupLog");

                // GPU 상태도 현재 언어로 다시 업데이트 (더 안전하게)
                if (!string.IsNullOrEmpty(currentGpuStatus))
                {
#if DEBUG
                    Console.WriteLine($"[언어 변경] GPU 상태 재번역: '{currentGpuStatus}'");
#endif
                    string executionModeText = GetLocalizedString("LabelExecutionMode");
                    string translatedStatus = TranslateGpuStatus(currentGpuStatus);
                    gpuStatusLabel.Text = $"{executionModeText} {translatedStatus}";
                    
                    // 색상도 다시 설정 (동일한 로직 사용)
                    if (IsGpuStatus(currentGpuStatus))
                    {
                        gpuStatusLabel.ForeColor = Color.Green;
                    }
                    else
                    {
                        gpuStatusLabel.ForeColor = Color.OrangeRed;
                    }
                }

                // 상태 메시지도 현재 언어에 맞게 업데이트합니다.
                if (startButton.Enabled)
                {
                    UpdateStatus(GetLocalizedString("StatusReady"), Color.Red);
                }
                else
                {
                    UpdateStatus(GetLocalizedString("StatusRunning"), Color.Green);
                }
            }
            catch (Exception ex)
            {
                // 리소스 로드 실패 시 기본 텍스트 사용
                Console.WriteLine($"리소스 로드 실패: {ex.Message}");
            }
        }

        private string GetLocalizedString(string key)
        {
            try
            {
                string result = resourceManager.GetString(key, Thread.CurrentThread.CurrentUICulture);
                return result ?? key; // 키를 찾을 수 없으면 키 자체를 반환
            }
            catch
            {
                return key; // 오류 발생 시 키 자체를 반환
            }
        }
        
        // --- 기존 Public 메서드 ---
        private void OnTargetChanged(object sender, EventArgs e)
        {
            var selected = targetCheckBoxes.Where(kvp => kvp.Value.Checked).Select(kvp => kvp.Key).ToList();
            TargetsChanged?.Invoke(selected);
        }

        public void UpdateStatus(string message, Color color) 
        { 
            if (rootForm.InvokeRequired) 
            { 
                rootForm.BeginInvoke(new Action(() => UpdateStatus(message, color))); 
                return; 
            } 
            statusLabel.Text = message; 
            statusLabel.ForeColor = color; 
        }

        public void LogMessage(string message) 
        { 
            if (rootForm.InvokeRequired) 
            { 
                rootForm.BeginInvoke(new Action(() => LogMessage(message))); 
                return; 
            } 
            logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"); 
            logTextBox.SelectionStart = logTextBox.Text.Length; 
            logTextBox.ScrollToCaret(); 
        }

        public void SetRunningState(bool isRunning) 
        { 
            if (rootForm.InvokeRequired) 
            { 
                rootForm.BeginInvoke(new Action(() => SetRunningState(isRunning))); 
                return;
            } 
            startButton.Enabled = !isRunning; 
            stopButton.Enabled = isRunning; 
        }

        public void UpdateGpuStatus(string status)
        {
            if (rootForm.InvokeRequired)
            {
                rootForm.BeginInvoke(new Action(() => UpdateGpuStatus(status)));
                return;
            }
            
            string executionModeText = GetLocalizedString("LabelExecutionMode");
            string translatedStatus = TranslateGpuStatus(status);
            gpuStatusLabel.Text = $"{executionModeText} {translatedStatus}";
            
            if (status.Contains("GPU"))
            {
                gpuStatusLabel.ForeColor = Color.Green;
            }
            else
            {
                gpuStatusLabel.ForeColor = Color.OrangeRed;
            }
        }
        
        private string TranslateGpuStatus(string originalStatus)
        {
            // GPU 상태를 현재 언어로 번역 (더 관대한 GPU 감지)
            if (string.IsNullOrEmpty(originalStatus))
            {
                return GetLocalizedString("GPU_CPU");
            }
            
#if DEBUG
            // 디버깅용 로그
            Console.WriteLine($"[GPU 번역] 입력: '{originalStatus}'");
#endif
            
            // 대소문자 구분 없이 정확한 매칭
            string status = originalStatus.ToLower().Trim();
            
            // CUDA 관련 모든 경우 체크
            if (status.Contains("cuda"))
            {
#if DEBUG
                Console.WriteLine("[GPU 번역] CUDA로 매칭됨");
#endif
                return GetLocalizedString("GPU_CUDA");
            }
            
            // DirectML 관련 모든 경우 체크
            if (status.Contains("directml"))
            {
#if DEBUG
                Console.WriteLine("[GPU 번역] DirectML로 매칭됨");
#endif
                return GetLocalizedString("GPU_DirectML");
            }
            
            // GPU 단어가 포함된 모든 경우 (CPU가 함께 있어도 GPU 우선)
            if (status.Contains("gpu"))
            {
#if DEBUG
                Console.WriteLine("[GPU 번역] 일반 GPU로 매칭됨");
#endif
                // DirectML인지 CUDA인지 불분명한 경우, 일반적인 GPU로 표시
                return GetLocalizedString("GPU_CUDA"); 
            }
            
            // 명시적으로 CPU인 경우만 CPU로 표시
            if (status.Contains("cpu") && !status.Contains("gpu"))
            {
#if DEBUG
                Console.WriteLine("[GPU 번역] CPU로 매칭됨");
#endif
                return GetLocalizedString("GPU_CPU");
            }
            
            // 알 수 없는 경우, 원본 문자열에 따라 추측
            // "로드 실패", "Unknown" 등의 경우 CPU로 분류
            if (status.Contains("실패") || status.Contains("fail") || status.Contains("error"))
            {
#if DEBUG
                Console.WriteLine("[GPU 번역] 오류 상태로 CPU 선택");
#endif
                return GetLocalizedString("GPU_CPU");
            }
            
            // 그 외의 경우는 GPU로 가정 (더 관대하게)
#if DEBUG
            Console.WriteLine($"[GPU 번역] 알 수 없는 상태 '{originalStatus}' - GPU로 가정");
#endif
            return GetLocalizedString("GPU_CUDA");
        }
        
        private bool IsGpuStatus(string status)
        {
            // GPU 상태인지 판단하는 통합 메서드
            if (string.IsNullOrEmpty(status))
            {
                return false;
            }
            
            string lowerStatus = status.ToLower().Trim();
            
            // GPU 관련 키워드가 있으면 GPU로 판단
            if (lowerStatus.Contains("cuda") || 
                lowerStatus.Contains("directml") || 
                lowerStatus.Contains("gpu"))
            {
                // 단, 명시적으로 CPU만 있는 경우는 제외
                if (lowerStatus.Contains("cpu") && !lowerStatus.Contains("gpu"))
                {
                    return false;
                }
                return true;
            }
            
            // 오류나 실패 상태는 CPU로 간주
            if (lowerStatus.Contains("실패") || 
                lowerStatus.Contains("fail") || 
                lowerStatus.Contains("error") ||
                lowerStatus.Contains("cpu"))
            {
                return false;
            }
            
            // 알 수 없는 상태는 GPU로 가정 (더 관대하게)
            return true;
        }
    }
}