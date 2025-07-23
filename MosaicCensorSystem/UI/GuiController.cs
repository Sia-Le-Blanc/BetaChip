#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
        public event Action TestCaptureClicked;
        public event Action<List<string>> TargetsChanged;

        // --- UI Controls ---
        private readonly Form rootForm;
        private Label statusLabel;
        private TextBox logTextBox;
        private Button startButton;
        private Button stopButton;
        private Label gpuStatusLabel; // ★★★ 추가된 GPU 상태 레이블 ★★★
        private readonly Dictionary<string, CheckBox> targetCheckBoxes = new Dictionary<string, CheckBox>();

        public GuiController(Form mainForm)
        {
            rootForm = mainForm;
            rootForm.Text = "베타칩";
            CreateGui();
        }

        private void CreateGui()
        {
            rootForm.SuspendLayout();
            var titleLabel = new Label { Text = "베타칩", Font = new Font("Arial", 12, FontStyle.Bold), BackColor = Color.LightSkyBlue, BorderStyle = BorderStyle.FixedSingle, TextAlign = ContentAlignment.MiddleCenter, Height = 40, Dock = DockStyle.Top };
            var scrollableContainer = new ScrollablePanel { Dock = DockStyle.Fill };
            rootForm.Controls.Add(scrollableContainer);
            rootForm.Controls.Add(titleLabel);
            CreateContent(scrollableContainer.ScrollableFrame);
            rootForm.ResumeLayout(false);
        }

        private void CreateContent(Panel parent)
        {
            int y = 10;
            statusLabel = new Label { Text = "⭕ 시스템 대기 중", Font = new Font("Arial", 12, FontStyle.Bold), ForeColor = Color.Red, Location = new Point(10, y), AutoSize = true };
            parent.Controls.Add(statusLabel);
            y += 40;

            // ★★★ GPU 상태 레이블 추가 ★★★
            gpuStatusLabel = new Label { Text = "실행 모드: 로딩 중...", Font = new Font("Arial", 10), Location = new Point(10, y), AutoSize = true };
            parent.Controls.Add(gpuStatusLabel);
            y += 30; // 레이블 높이만큼 y 좌표 증가

            var controlGroup = new GroupBox { Text = "🎮 제어", Location = new Point(10, y), Size = new Size(460, 80) };
            startButton = new Button { Text = "🚀 시작", BackColor = Color.DarkGreen, ForeColor = Color.White, Font = new Font("Arial", 10, FontStyle.Bold), Size = new Size(120, 40), Location = new Point(20, 25) };
            stopButton = new Button { Text = "🛑 중지", BackColor = Color.DarkRed, ForeColor = Color.White, Font = new Font("Arial", 10, FontStyle.Bold), Size = new Size(120, 40), Location = new Point(160, 25), Enabled = false };
            var testButton = new Button { Text = "🔍 테스트", BackColor = Color.DarkBlue, ForeColor = Color.White, Font = new Font("Arial", 10, FontStyle.Bold), Size = new Size(120, 40), Location = new Point(300, 25) };
            startButton.Click += (s, e) => StartClicked?.Invoke();
            stopButton.Click += (s, e) => StopClicked?.Invoke();
            testButton.Click += (s, e) => TestCaptureClicked?.Invoke();
            controlGroup.Controls.AddRange(new Control[] { startButton, stopButton, testButton });
            parent.Controls.Add(controlGroup);
            y += 90;

            var settingsGroup = new GroupBox { Text = "⚙️ 설정", Location = new Point(10, y), Size = new Size(460, 330) };
            CreateSettingsContent(settingsGroup);
            parent.Controls.Add(settingsGroup);
            y += 340;

            var logGroup = new GroupBox { Text = "📝 로그", Location = new Point(10, y), Size = new Size(460, 120) };
            logTextBox = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Location = new Point(10, 20), Size = new Size(440, 90), Font = new Font("Consolas", 8.25f) };
            logGroup.Controls.Add(logTextBox);
            parent.Controls.Add(logGroup);
        }

        private void CreateSettingsContent(GroupBox settingsGroup)
        {
            int y = 25;
            var fpsValueLabel = new Label { Text = "15", Location = new Point(390, y), AutoSize = true };
            var fpsSlider = new TrackBar { Minimum = 5, Maximum = 60, Value = 15, TickFrequency = 5, Location = new Point(100, y - 5), Size = new Size(280, 45) };
            fpsSlider.ValueChanged += (s, e) => { fpsValueLabel.Text = fpsSlider.Value.ToString(); FpsChanged?.Invoke(fpsSlider.Value); };
            settingsGroup.Controls.AddRange(new Control[] { new Label { Text = "목표 FPS:", Location = new Point(10, y), AutoSize = true }, fpsSlider, fpsValueLabel });
            y += 40;

            var enableDetectionCheckBox = new CheckBox { Text = "🔍 객체 감지", Checked = true, Location = new Point(10, y), AutoSize = true };
            enableDetectionCheckBox.CheckedChanged += (s, e) => DetectionToggled?.Invoke(enableDetectionCheckBox.Checked);
            var enableCensoringCheckBox = new CheckBox { Text = "🎨 검열 효과", Checked = true, Location = new Point(150, y), AutoSize = true };
            enableCensoringCheckBox.CheckedChanged += (s, e) => CensoringToggled?.Invoke(enableCensoringCheckBox.Checked);
            
            #if PATREON_VERSION
            var enableStickerCheckBox = new CheckBox { Text = "✨ 스티커 표시", Checked = false, Location = new Point(290, y), AutoSize = true };
            enableStickerCheckBox.CheckedChanged += (s, e) => StickerToggled?.Invoke(enableStickerCheckBox.Checked);
            settingsGroup.Controls.AddRange(new Control[] { enableDetectionCheckBox, enableCensoringCheckBox, enableStickerCheckBox });
            #else
            settingsGroup.Controls.AddRange(new Control[] { enableDetectionCheckBox, enableCensoringCheckBox });
            #endif

            y += 30;

            var mosaicRadioButton = new RadioButton { Text = "🟦 모자이크", Checked = true, Location = new Point(10, y), AutoSize = true };
            var blurRadioButton = new RadioButton { Text = "🌀 블러", Location = new Point(150, y), AutoSize = true };
            EventHandler censorTypeHandler = (s, e) => { if (s is RadioButton rb && rb.Checked) { CensorTypeChanged?.Invoke(mosaicRadioButton.Checked ? CensorType.Mosaic : CensorType.Blur); } };
            mosaicRadioButton.CheckedChanged += censorTypeHandler;
            blurRadioButton.CheckedChanged += censorTypeHandler;
            settingsGroup.Controls.AddRange(new Control[] { mosaicRadioButton, blurRadioButton });
            y += 30;

            var strengthValueLabel = new Label { Text = "20", Location = new Point(390, y), AutoSize = true };
            var strengthSlider = new TrackBar { Minimum = 10, Maximum = 40, Value = 20, TickFrequency = 5, Location = new Point(100, y - 5), Size = new Size(280, 45) };
            strengthSlider.ValueChanged += (s, e) => { strengthValueLabel.Text = strengthSlider.Value.ToString(); StrengthChanged?.Invoke(strengthSlider.Value); };
            settingsGroup.Controls.AddRange(new Control[] { new Label { Text = "검열 강도:", Location = new Point(10, y), AutoSize = true }, strengthSlider, strengthValueLabel });
            y += 40;

            var confidenceValueLabel = new Label { Text = "0.3", Location = new Point(390, y), AutoSize = true };
            var confidenceSlider = new TrackBar { Minimum = 10, Maximum = 90, Value = 30, TickFrequency = 10, Location = new Point(100, y - 5), Size = new Size(280, 45) };
            confidenceSlider.ValueChanged += (s, e) => { float val = confidenceSlider.Value / 100.0f; confidenceValueLabel.Text = val.ToString("F1"); ConfidenceChanged?.Invoke(val); };
            settingsGroup.Controls.AddRange(new Control[] { new Label { Text = "감지 신뢰도:", Location = new Point(10, y), AutoSize = true }, confidenceSlider, confidenceValueLabel });
            y += 40;
            
            var targetsGroup = new GroupBox { Text = "🎯 검열 대상", Location = new Point(10, y), Size = new Size(440, 130) };
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

        private void OnTargetChanged(object sender, EventArgs e)
        {
            var selected = targetCheckBoxes.Where(kvp => kvp.Value.Checked).Select(kvp => kvp.Key).ToList();
            TargetsChanged?.Invoke(selected);
        }

        public void UpdateStatus(string message, Color color) { if (rootForm.InvokeRequired) { rootForm.BeginInvoke(new Action(() => UpdateStatus(message, color))); return; } statusLabel.Text = message; statusLabel.ForeColor = color; }
        public void LogMessage(string message) { if (rootForm.InvokeRequired) { rootForm.BeginInvoke(new Action(() => LogMessage(message))); return; } logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"); logTextBox.SelectionStart = logTextBox.Text.Length; logTextBox.ScrollToCaret(); }
        public void SetRunningState(bool isRunning) { if (rootForm.InvokeRequired) { rootForm.BeginInvoke(new Action(() => SetRunningState(isRunning))); return; return; } startButton.Enabled = !isRunning; stopButton.Enabled = isRunning; }

        // ★★★ 추가된 GPU 상태 업데이트 메서드 ★★★
        public void UpdateGpuStatus(string status)
        {
            if (rootForm.InvokeRequired)
            {
                rootForm.BeginInvoke(new Action(() => UpdateGpuStatus(status)));
                return;
            }
            gpuStatusLabel.Text = $"실행 모드: {status}";
            if (status.Contains("GPU"))
            {
                gpuStatusLabel.ForeColor = Color.Green;
            }
            else if (status.Contains("CPU"))
            {
                gpuStatusLabel.ForeColor = Color.OrangeRed;
            }
            else
            {
                gpuStatusLabel.ForeColor = Color.Black; // 기본 색상
            }
        }
    }
}