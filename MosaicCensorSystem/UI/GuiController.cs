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
        public event Action<bool> StickerToggled;
        public event Action<CensorType> CensorTypeChanged;
    
        public event Action<int> StrengthChanged;
        public event Action<float> ConfidenceChanged;
        public event Action StartClicked;
        public event Action StopClicked;
        public event Action CaptureAndSaveClicked; // ★★★ 변경: TestCaptureClicked → CaptureAndSaveClicked
        public event Action<List<string>> TargetsChanged;

        // --- UI 컨트롤들을 멤버 변수로 선언 ---
        private readonly Form rootForm;
        private Label titleLabel;
        private ComboBox languageComboBox;
        private Label statusLabel;
        private TextBox logTextBox;
        private Button startButton, stopButton, captureButton; // ★★★ 변경: testButton → captureButton
        private GroupBox controlGroup, settingsGroup, logGroup, targetsGroup;
        private Label gpuStatusLabel;
        private Label fpsLabel, strengthLabel, confidenceLabel;
        private CheckBox enableDetectionCheckBox, enableCensoringCheckBox;
        private RadioButton mosaicRadioButton, blurRadioButton, blackBoxRadioButton;
        private TrackBar fpsSlider, strengthSlider, confidenceSlider;
        private readonly Dictionary<string, CheckBox> targetCheckBoxes = new Dictionary<string, CheckBox>();

        // ★★★ 툴팁 컴포넌트 추가 ★★★
        private ToolTip toolTip;

        // ★★★ PATREON_VERSION이 정의된 경우에만 enableStickersCheckBox 변수를 선언합니다. ★★★
#if PATREON_VERSION
        private CheckBox enableStickersCheckBox;
#endif

        // 리소스 매니저
        private ResourceManager resourceManager;
        private string currentGpuStatus = "CPU";

        public GuiController(Form mainForm)
        {
            rootForm = mainForm;
            resourceManager = new ResourceManager("MosaicCensorSystem.Properties.Strings", typeof(GuiController).Assembly);
            
            // 툴팁 초기화
            toolTip = new ToolTip();
            toolTip.AutoPopDelay = 8000; // 8초 후 자동 사라짐
            toolTip.InitialDelay = 500;  // 0.5초 후 나타남
            toolTip.ReshowDelay = 200;   // 다른 컨트롤로 이동시 0.2초 후 나타남
            
            CreateGui();
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
            
            languageComboBox = new ComboBox {
                Location = new Point(350, 5),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Size = new Size(100, 25),
                Font = new Font("Arial", 9, FontStyle.Bold)
            };
            languageComboBox.Items.AddRange(new string[] { "🇰🇷 한국어", "🇺🇸 English" });
            languageComboBox.SelectedIndex = 0;
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
            // ★★★ 변경: testButton → captureButton, 색상도 변경 ★★★
            captureButton = new Button {
                BackColor = Color.DarkOrange,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Size = new Size(120, 40),
                Location = new Point(300, 25)
            };
            
            startButton.Click += (s, e) => StartClicked?.Invoke();
            stopButton.Click += (s, e) => StopClicked?.Invoke();
            captureButton.Click += (s, e) => CaptureAndSaveClicked?.Invoke(); // ★★★ 변경
            
            controlGroup.Controls.AddRange(new Control[] { startButton, stopButton, captureButton });
            parent.Controls.Add(controlGroup);
            y += 90;

            settingsGroup = new GroupBox { Location = new Point(10, y), Size = new Size(460, 380) };
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
            
            var fpsValueLabel = new Label { Text = "15", Location = new Point(390, y), AutoSize = true };
            fpsSlider = new TrackBar {
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

            // ★★★★★★★★★★★★ 수정된 부분 ★★★★★★★★★★★★
            // PATREON_VERSION이 정의된 경우(P=true로 빌드 시)에만
            // 스티커 활성화 체크박스를 생성하고 화면에 추가합니다.
#if PATREON_VERSION
            enableStickersCheckBox = new CheckBox { 
                Checked = false, 
                Location = new Point(10, y), 
                AutoSize = true,
                Text = "스티커 활성화"
            };
            enableStickersCheckBox.CheckedChanged += (s, e) => StickerToggled?.Invoke(enableStickersCheckBox.Checked);
            settingsGroup.Controls.Add(enableStickersCheckBox);
            y += 30;
#endif
            // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★

            mosaicRadioButton = new RadioButton {
                Checked = true,
                Location = new Point(10, y),
                AutoSize = true
            };
            blurRadioButton = new RadioButton {
                Location = new Point(100, y),
                AutoSize = true
            };
            blackBoxRadioButton = new RadioButton {
                Location = new Point(200, y),
                AutoSize = true
            };
            
            EventHandler censorTypeHandler = (s, e) => {
                if (s is RadioButton rb && rb.Checked) {
                    if (mosaicRadioButton.Checked) CensorTypeChanged?.Invoke(CensorType.Mosaic);
                    else if (blurRadioButton.Checked) CensorTypeChanged?.Invoke(CensorType.Blur);
                    else if (blackBoxRadioButton.Checked) CensorTypeChanged?.Invoke(CensorType.BlackBox);
                }
            };
            mosaicRadioButton.CheckedChanged += censorTypeHandler;
            blurRadioButton.CheckedChanged += censorTypeHandler;
            blackBoxRadioButton.CheckedChanged += censorTypeHandler;
            
            settingsGroup.Controls.AddRange(new Control[] { mosaicRadioButton, blurRadioButton, blackBoxRadioButton });
            y += 30;

            var strengthValueLabel = new Label { Text = "20", Location = new Point(390, y), AutoSize = true };
            strengthSlider = new TrackBar {
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

            var confidenceValueLabel = new Label { Text = "0.3", Location = new Point(390, y), AutoSize = true };
            confidenceSlider = new TrackBar {
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
            
            targetsGroup = new GroupBox { Location = new Point(10, y), Size = new Size(440, 130) };
            var allTargets = new[] { "얼굴", "가슴", "겨드랑이", "보지", "발", "몸 전체", "자지", "팬티", "눈", "손", "교미", "신발", "가슴_옷", "여성" };
            var defaultTargets = new[] { "얼굴", "가슴", "보지", "팬티" };
            for (int i = 0; i < allTargets.Length; i++)
            {
                var checkbox = new CheckBox {
                    Text = allTargets[i],
                    Checked = defaultTargets.Contains(allTargets[i]),
                    Location = new Point(15 + (i % 3) * 140, 25 + (i / 3) * 20),
                    AutoSize = true,
                    Tag = allTargets[i]
                };
                checkbox.CheckedChanged += OnTargetChanged;
                targetCheckBoxes[allTargets[i]] = checkbox;
                targetsGroup.Controls.Add(checkbox);
            }
            settingsGroup.Controls.Add(targetsGroup);
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            string culture = languageComboBox.SelectedIndex == 0 ? "ko-KR" : "en-US";
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
            UpdateUIText();
            UpdateToolTips(); // ★★★ 툴팁도 업데이트 ★★★
        }

        private void UpdateUIText()
        {
            try
            {
                rootForm.Text = GetLocalizedString("AppTitle");
                titleLabel.Text = GetLocalizedString("AppTitle");

                controlGroup.Text = GetLocalizedString("GroupControls");
                startButton.Text = GetLocalizedString("ButtonStart");
                stopButton.Text = GetLocalizedString("ButtonStop");
                captureButton.Text = GetLocalizedString("ButtonCaptureAndSave"); // ★★★ 변경
                
                settingsGroup.Text = GetLocalizedString("GroupSettings");
                fpsLabel.Text = GetLocalizedString("LabelFps");
                enableDetectionCheckBox.Text = GetLocalizedString("LabelDetection");
                enableCensoringCheckBox.Text = GetLocalizedString("LabelEffect");
#if PATREON_VERSION
                enableStickersCheckBox.Text = GetLocalizedString("LabelStickers");
#endif
                mosaicRadioButton.Text = GetLocalizedString("LabelCensorTypeMosaic");
                blurRadioButton.Text = GetLocalizedString("LabelCensorTypeBlur");
                blackBoxRadioButton.Text = GetLocalizedString("LabelCensorTypeBlackBox");
                strengthLabel.Text = GetLocalizedString("LabelCensorStrength");
                confidenceLabel.Text = GetLocalizedString("LabelConfidence");
                targetsGroup.Text = GetLocalizedString("GroupTargets");
                
                foreach (var kvp in targetCheckBoxes)
                {
                    var checkbox = kvp.Value;
                    var originalKey = (string)checkbox.Tag ?? kvp.Key;
                    checkbox.Text = GetLocalizedString($"Target_{originalKey}");
                }
                
                logGroup.Text = GetLocalizedString("GroupLog");

                if (!string.IsNullOrEmpty(currentGpuStatus))
                {
                    string executionModeText = GetLocalizedString("LabelExecutionMode");
                    string translatedStatus = TranslateGpuStatus(currentGpuStatus);
                    gpuStatusLabel.Text = $"{executionModeText} {translatedStatus}";
                    
                    if (IsGpuStatus(currentGpuStatus))
                    {
                        gpuStatusLabel.ForeColor = Color.Green;
                    }
                    else
                    {
                        gpuStatusLabel.ForeColor = Color.OrangeRed;
                    }
                }

                if (startButton.Enabled)
                {
                    UpdateStatus(GetLocalizedString("StatusReady"), Color.Red);
                }
                else
                {
                    UpdateStatus(GetLocalizedString("StatusRunning"), Color.Green);
                }
                
                // ★★★ 툴팁 업데이트 호출 ★★★
                UpdateToolTips();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"리소스 로드 실패: {ex.Message}");
            }
        }

        // ★★★ 툴팁 업데이트 메서드 추가 ★★★
        private void UpdateToolTips()
        {
            try
            {
                // 버튼 툴팁
                toolTip.SetToolTip(startButton, GetLocalizedString("TooltipStart"));
                toolTip.SetToolTip(stopButton, GetLocalizedString("TooltipStop"));
                toolTip.SetToolTip(captureButton, GetLocalizedString("TooltipCaptureAndSave")); // ★★★ 변경

                // 슬라이더 툴팁
                toolTip.SetToolTip(fpsSlider, GetLocalizedString("TooltipFps"));
                toolTip.SetToolTip(strengthSlider, GetLocalizedString("TooltipStrength"));
                toolTip.SetToolTip(confidenceSlider, GetLocalizedString("TooltipConfidence"));

                // 체크박스 툴팁
                toolTip.SetToolTip(enableDetectionCheckBox, GetLocalizedString("TooltipDetection"));
                toolTip.SetToolTip(enableCensoringCheckBox, GetLocalizedString("TooltipCensoring"));

#if PATREON_VERSION
                toolTip.SetToolTip(enableStickersCheckBox, GetLocalizedString("TooltipStickers"));
#endif

                // 라디오 버튼 툴팁
                toolTip.SetToolTip(mosaicRadioButton, GetLocalizedString("TooltipMosaic"));
                toolTip.SetToolTip(blurRadioButton, GetLocalizedString("TooltipBlur"));
                toolTip.SetToolTip(blackBoxRadioButton, GetLocalizedString("TooltipBlackBox"));

                // 타겟 체크박스 툴팁
                foreach (var kvp in targetCheckBoxes)
                {
                    var checkbox = kvp.Value;
                    var originalKey = (string)checkbox.Tag ?? kvp.Key;
                    toolTip.SetToolTip(checkbox, GetLocalizedString($"TooltipTarget_{originalKey}"));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"툴팁 업데이트 실패: {ex.Message}");
            }
        }

        private string GetLocalizedString(string key)
        {
            try
            {
                string result = resourceManager.GetString(key, Thread.CurrentThread.CurrentUICulture);
                return result ?? key;
            }
            catch
            {
                return key;
            }
        }
        
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
            
            currentGpuStatus = status;
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
            if (string.IsNullOrEmpty(originalStatus)) return GetLocalizedString("GPU_CPU");
            
            string status = originalStatus.ToLower().Trim();
            
            if (status.Contains("cuda")) return GetLocalizedString("GPU_CUDA");
            if (status.Contains("directml")) return GetLocalizedString("GPU_DirectML");
            if (status.Contains("gpu")) return GetLocalizedString("GPU_CUDA"); 
            
            return GetLocalizedString("GPU_CPU");
        }
        
        private bool IsGpuStatus(string status)
        {
            if (string.IsNullOrEmpty(status)) return false;
            
            string lowerStatus = status.ToLower().Trim();
            
            if (lowerStatus.Contains("cuda") || 
                lowerStatus.Contains("directml") || 
                lowerStatus.Contains("gpu"))
            {
                if (lowerStatus.Contains("cpu") && !lowerStatus.Contains("gpu"))
                {
                    return false;
                }
                return true;
            }
            
            return false;
        }

        // ★★★ 리소스 해제 메서드 추가 ★★★
        public void Dispose()
        {
            toolTip?.Dispose();
        }
    }
}