#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization; // 다국어 지원을 위해 추가
using System.Linq;
using System.Threading;     // 다국어 지원을 위해 추가
using System.Windows.Forms;
using MosaicCensorSystem.Detection;
// 'Properties' 폴더에 리소스 파일을 만들었다는 가정 하에 네임스페이스를 지정합니다.
// 만약 다른 곳에 만들었다면, 이 네임스페이스를 실제 위치에 맞게 수정해야 합니다.
using MosaicCensorSystem.Properties; 

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
        private CheckBox enableDetectionCheckBox, enableCensoringCheckBox;
        private RadioButton mosaicRadioButton, blurRadioButton;
        private readonly Dictionary<string, CheckBox> targetCheckBoxes = new Dictionary<string, CheckBox>();

        public GuiController(Form mainForm)
        {
            rootForm = mainForm;
            CreateGui();
            // 프로그램 시작 시 기본 언어(한국어)로 UI 텍스트를 설정합니다.
            UpdateUIText(); 
        }

        private void CreateGui()
        {
            rootForm.SuspendLayout();
            
            titleLabel = new Label { Font = new Font("Arial", 12, FontStyle.Bold), BackColor = Color.LightSkyBlue, BorderStyle = BorderStyle.FixedSingle, TextAlign = ContentAlignment.MiddleCenter, Height = 40, Dock = DockStyle.Top };
            
            // 언어 변경 콤보박스 생성
            languageComboBox = new ComboBox { Location = new Point(380, 7), DropDownStyle = ComboBoxStyle.DropDownList };
            languageComboBox.Items.AddRange(new string[] { "한국어", "English" });
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
            statusLabel = new Label { Font = new Font("Arial", 12, FontStyle.Bold), ForeColor = Color.Red, Location = new Point(10, y), AutoSize = true };
            parent.Controls.Add(statusLabel);
            y += 40;
            
            gpuStatusLabel = new Label { Font = new Font("Arial", 10), Location = new Point(10, y), AutoSize = true };
            parent.Controls.Add(gpuStatusLabel);
            y += 30;

            controlGroup = new GroupBox { Location = new Point(10, y), Size = new Size(460, 80) };
            startButton = new Button { BackColor = Color.DarkGreen, ForeColor = Color.White, Font = new Font("Arial", 10, FontStyle.Bold), Size = new Size(120, 40), Location = new Point(20, 25) };
            stopButton = new Button { BackColor = Color.DarkRed, ForeColor = Color.White, Font = new Font("Arial", 10, FontStyle.Bold), Size = new Size(120, 40), Location = new Point(160, 25), Enabled = false };
            testButton = new Button { BackColor = Color.DarkBlue, ForeColor = Color.White, Font = new Font("Arial", 10, FontStyle.Bold), Size = new Size(120, 40), Location = new Point(300, 25) };
            startButton.Click += (s, e) => StartClicked?.Invoke();
            stopButton.Click += (s, e) => StopClicked?.Invoke();
            testButton.Click += (s, e) => TestCaptureClicked?.Invoke();
            controlGroup.Controls.AddRange(new Control[] { startButton, stopButton, testButton });
            parent.Controls.Add(controlGroup);
            y += 90;

            settingsGroup = new GroupBox { Location = new Point(10, y), Size = new Size(460, 330) };
            CreateSettingsContent(settingsGroup);
            parent.Controls.Add(settingsGroup);
            y += 340;

            logGroup = new GroupBox { Location = new Point(10, y), Size = new Size(460, 120) };
            logTextBox = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Location = new Point(10, 20), Size = new Size(440, 90), Font = new Font("Consolas", 8.25f) };
            logGroup.Controls.Add(logTextBox);
            parent.Controls.Add(logGroup);
        }
        
        private void CreateSettingsContent(GroupBox settingsGroup)
        {
            int y = 25;
            var fpsValueLabel = new Label { Text = "15", Location = new Point(390, y), AutoSize = true };
            var fpsSlider = new TrackBar { Minimum = 5, Maximum = 240, Value = 15, TickFrequency = 5, Location = new Point(100, y - 5), Size = new Size(280, 45) };
            fpsSlider.ValueChanged += (s, e) => { fpsValueLabel.Text = fpsSlider.Value.ToString(); FpsChanged?.Invoke(fpsSlider.Value); };
            fpsLabel = new Label { Location = new Point(10, y), AutoSize = true };
            settingsGroup.Controls.AddRange(new Control[] { fpsLabel, fpsSlider, fpsValueLabel });
            y += 40;

            enableDetectionCheckBox = new CheckBox { Checked = true, Location = new Point(10, y), AutoSize = true };
            enableDetectionCheckBox.CheckedChanged += (s, e) => DetectionToggled?.Invoke(enableDetectionCheckBox.Checked);
            enableCensoringCheckBox = new CheckBox { Checked = true, Location = new Point(150, y), AutoSize = true };
            enableCensoringCheckBox.CheckedChanged += (s, e) => CensoringToggled?.Invoke(enableCensoringCheckBox.Checked);
            settingsGroup.Controls.AddRange(new Control[] { enableDetectionCheckBox, enableCensoringCheckBox });
            y += 30;

            mosaicRadioButton = new RadioButton { Checked = true, Location = new Point(10, y), AutoSize = true };
            blurRadioButton = new RadioButton { Location = new Point(150, y), AutoSize = true };
            EventHandler censorTypeHandler = (s, e) => { if (s is RadioButton rb && rb.Checked) { CensorTypeChanged?.Invoke(mosaicRadioButton.Checked ? CensorType.Mosaic : CensorType.Blur); } };
            mosaicRadioButton.CheckedChanged += censorTypeHandler;
            blurRadioButton.CheckedChanged += censorTypeHandler;
            settingsGroup.Controls.AddRange(new Control[] { mosaicRadioButton, blurRadioButton });
            y += 30;

            var strengthValueLabel = new Label { Text = "20", Location = new Point(390, y), AutoSize = true };
            var strengthSlider = new TrackBar { Minimum = 10, Maximum = 40, Value = 20, TickFrequency = 5, Location = new Point(100, y - 5), Size = new Size(280, 45) };
            strengthSlider.ValueChanged += (s, e) => { strengthValueLabel.Text = strengthSlider.Value.ToString(); StrengthChanged?.Invoke(strengthSlider.Value); };
            strengthLabel = new Label { Location = new Point(10, y), AutoSize = true };
            settingsGroup.Controls.AddRange(new Control[] { strengthLabel, strengthSlider, strengthValueLabel });
            y += 40;

            var confidenceValueLabel = new Label { Text = "0.3", Location = new Point(390, y), AutoSize = true };
            var confidenceSlider = new TrackBar { Minimum = 10, Maximum = 90, Value = 30, TickFrequency = 10, Location = new Point(100, y - 5), Size = new Size(280, 45) };
            confidenceSlider.ValueChanged += (s, e) => { float val = confidenceSlider.Value / 100.0f; confidenceValueLabel.Text = val.ToString("F1"); ConfidenceChanged?.Invoke(val); };
            confidenceLabel = new Label { Location = new Point(10, y), AutoSize = true };
            settingsGroup.Controls.AddRange(new Control[] { confidenceLabel, confidenceSlider, confidenceValueLabel });
            y += 40;
            
            targetsGroup = new GroupBox { Location = new Point(10, y), Size = new Size(440, 130) };
            var allTargets = new[] { "얼굴", "가슴", "겨드랑이", "보지", "발", "몸 전체", "자지", "팬티", "눈", "손", "교미", "신발", "가슴_옷", "여성" };
            var defaultTargets = new[] { "얼굴", "가슴", "보지", "팬티" };
            for (int i = 0; i < allTargets.Length; i++)
            {
                var checkbox = new CheckBox { Text = allTargets[i], Checked = defaultTargets.Contains(allTargets[i]), Location = new Point(15 + (i % 3) * 140, 25 + (i / 3) * 20), AutoSize = true };
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
            // 하드코딩된 텍스트 대신 Resources.Strings.이름 형식으로 텍스트를 설정합니다.
            rootForm.Text = Resources.Strings.AppTitle;
            titleLabel.Text = Resources.Strings.AppTitle;

            controlGroup.Text = Resources.Strings.GroupControls;
            startButton.Text = Resources.Strings.ButtonStart;
            stopButton.Text = Resources.Strings.ButtonStop;
            testButton.Text = Resources.Strings.ButtonTest;
            
            settingsGroup.Text = Resources.Strings.GroupSettings;
            fpsLabel.Text = Resources.Strings.LabelFps;
            enableDetectionCheckBox.Text = Resources.Strings.LabelDetection;
            enableCensoringCheckBox.Text = Resources.Strings.LabelEffect;
            mosaicRadioButton.Text = Resources.Strings.LabelCensorTypeMosaic;
            blurRadioButton.Text = Resources.Strings.LabelCensorTypeBlur;
            strengthLabel.Text = Resources.Strings.LabelCensorStrength;
            confidenceLabel.Text = Resources.Strings.LabelConfidence;
            targetsGroup.Text = Resources.Strings.GroupTargets;
            
            logGroup.Text = Resources.Strings.GroupLog;

            // 상태 메시지도 현재 언어에 맞게 업데이트합니다.
            if (startButton.Enabled)
            {
                UpdateStatus(Resources.Strings.StatusReady, Color.Red);
            }
            else
            {
                UpdateStatus(Resources.Strings.StatusRunning, Color.Green);
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
            // gpuStatusLabel.Text = $"실행 모드: {status}";
            gpuStatusLabel.Text = $"{Resources.Strings.LabelExecutionMode} {status}";
            if (status.Contains("GPU"))
            {
                gpuStatusLabel.ForeColor = Color.Green;
            }
            else
            {
                gpuStatusLabel.ForeColor = Color.OrangeRed;
            }
        }
    }
}