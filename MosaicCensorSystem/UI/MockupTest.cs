using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace MosaicCensorSystem.UI
{
    // ==========================================
    // 다국어 번역 엔진 (I18n)
    // ==========================================
    public static class I18n
    {
        public static string CurrentLanguage { get; private set; } = "한국어 🇰🇷";
        public static event Action LanguageChanged;

        private static readonly Dictionary<string, Dictionary<string, string>> dict = new()
        {
            { "English 🇺🇸", new() {
                { "Title", "Mosaic Censor System (Ultimate Responsive UI)" },
                { "Start", "Start" }, { "Stop", "Stop" }, { "Capture", "Capture" },
                { "Settings", "Global Settings" },
                { "ModelSelect", "AI Model" }, { "StdModel", "Standard" }, { "ObbModel", "Precision (OBB)" },
                { "GlobalSwitches", "Feature Toggles" },
                { "EnableOverlay", "Enable Detection Overlay" }, { "EnableSticker", "Enable Stickers" },
                { "FormatSelect", "Obfuscation Format" },
                { "FmtMosaic", "Mosaic" }, { "FmtBlur", "Blur" }, { "FmtBlackBox", "Black Box" },
                { "Sliders", "Threshold & Intensity" },
                { "Confidence", "AI Confidence Threshold" }, { "Strength", "Obfuscation Intensity" },
                { "Targets", "Censor Targets" },
                { "Logs", "System Terminal" },
                { "Cat_Head", "Head & Face" }, { "Cat_Torso", "Torso & Chest" }, { "Cat_Lower", "Lower Body" }, { "Cat_Rear", "Rear" }, { "Cat_Other", "Extremities" },
                { "Face_Female", "Female Face" }, { "Face_Male", "Male Face" }, { "Eyes", "Eyes" },
                { "Breast_Nude", "Nude Breast" }, { "Breast_Underwear", "Bra/Underwear" }, { "Breast_Clothed", "Clothed Chest" }, { "Armpit", "Armpit" }, { "Navel", "Navel" },
                { "Penis", "Penis" }, { "Vulva_Nude", "Vulva" }, { "Anus", "Anus" }, { "Panty", "Panties" }, { "Hpis", "Pelvis" },
                { "Butt_Nude", "Nude Butt" }, { "Butt_Clothed", "Clothed Butt" },
                { "Hands", "Hands" }, { "Feet", "Feet" }, { "Shoes", "Shoes" }, { "Body_Full", "Full Body" }, { "Sex_Act", "Sex Act" },
                { "StickerChk", "Sticker" }, { "StickerSetupBtn", "Configure Stickers..." }
            }},
            { "한국어 🇰🇷", new() {
                { "Title", "모자이크 검열 시스템 (풀 라인업 반응형 UI)" },
                { "Start", "작업 시작" }, { "Stop", "검열 중지" }, { "Capture", "화면 캡처" },
                { "Settings", "시스템 전역 설정" },
                { "ModelSelect", "AI 엔진 모델" }, { "StdModel", "일반 HBB (빠름)" }, { "ObbModel", "정밀 OBB (권장)" },
                { "GlobalSwitches", "핵심 기능 On/Off" },
                { "EnableOverlay", "박스 오버레이 표시" },
                { "FormatSelect", "기본 검열 및 필터 방식" },
                { "FmtMosaic", "모자이크" }, { "FmtBlur", "가우시안 블러" }, { "FmtBlackBox", "검은 박스" },
                { "Sliders", "엔진 민감도 및 검열 강도" },
                { "Confidence", "AI 인식 임계값 (Confidence)" }, { "Strength", "모자이크/블러 강도" },
                { "Targets", "AI 검열 타겟팅 부위" },
                { "Logs", "백그라운드 터미널 로그" },
                { "Cat_Head", "얼굴 및 머리" }, { "Cat_Torso", "상반신 및 흉부" }, { "Cat_Lower", "하반신 및 중요부위" }, { "Cat_Rear", "후면 및 둔부" }, { "Cat_Other", "팔다리 및 특수 행위" },
                { "Face_Female", "여성 얼굴" }, { "Face_Male", "남성 얼굴" }, { "Eyes", "눈" },
                { "Breast_Nude", "나체 가슴" }, { "Breast_Underwear", "속옷 가슴" }, { "Breast_Clothed", "착의 가슴" }, { "Armpit", "겨드랑이" }, { "Navel", "배꼽" },
                { "Penis", "남성기" }, { "Vulva_Nude", "여성기" }, { "Anus", "항문" }, { "Panty", "팬티" }, { "Hpis", "골반/사타구니" },
                { "Butt_Nude", "나체 엉덩이" }, { "Butt_Clothed", "착의 엉덩이" },
                { "Hands", "손" }, { "Feet", "발" }, { "Shoes", "신발" }, { "Body_Full", "전신" }, { "Sex_Act", "성행위" },
                { "StickerChk", "스티커" }, { "StickerSetupBtn", "스티커 매칭 설정..." }
            }}
        };

        public static string Get(string key) => 
            dict.TryGetValue(CurrentLanguage, out var d) && d.TryGetValue(key, out var v) ? v : 
            dict["English 🇺🇸"].TryGetValue(key, out var eng) ? eng : key;
        
        public static void SetLanguage(string lang)
        {
            if (dict.ContainsKey(lang))
            {
                CurrentLanguage = lang;
                LanguageChanged?.Invoke();
            }
        }
        public static string[] Languages => dict.Keys.ToArray();
    }

    // ==========================================
    // 커스텀 토글 스위치 (Modern Toggle)
    // ==========================================
    public class ToggleSwitch : Control
    {
        private bool _checked = false;
        public bool Checked 
        { 
            get => _checked; 
            set { _checked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); } 
        }
        public event EventHandler CheckedChanged;
        
        public ToggleSwitch()
        {
            this.Size = new Size(38, 20); // 크기 축소
            this.DoubleBuffered = true;
            this.Cursor = Cursors.Hand;
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent.BackColor);

            int arcSize = Height - 1;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, arcSize, arcSize, 90, 180);
            path.AddArc(rect.X + rect.Width - arcSize, rect.Y, arcSize, arcSize, -90, 180);
            path.CloseFigure();

            Color toggleColor = Checked ? Color.FromArgb(10, 132, 255) : Color.FromArgb(58, 58, 60);
            using (SolidBrush brush = new SolidBrush(toggleColor))
                e.Graphics.FillPath(brush, path);

            int headRadius = Height - 4;
            int headX = Checked ? Width - headRadius - 2 : 2;
            using (SolidBrush headBrush = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(headBrush, headX, 2, headRadius, headRadius);
        }
    }

    // ==========================================
    // 메인 목업 UI (Responsive Dark Theme Form)
    // ==========================================
    public class MockupUIForm : Form
    {
        private ComboBox langDropdown;
        private Label titleLabel;
        
        // Buttons
        private Button startButton, stopButton, captureButton;
        
        // Sections
        private Label settingsLabel, modelLabel, switchesLabel, formatLabel, slidersLabel, targetsLabel, logLabel;
        
        // Enums & Toggles
        private RadioButton stdRadio, obbRadio;
        private ToggleSwitch overlayToggle, stickerToggle;
        private Label overlayL, stickerL;
        private RadioButton mosRadio, blurRadio, boxRadio;
        
        // Sliders
        private Label confL, strL;
        private TrackBar confSlider, strSlider;
        
        // Target tracking
        private List<Control> dynamicLabels = new List<Control>();
        
        private TextBox logBox;

        public MockupUIForm()
        {
            this.Text = "BetaChip - Responsive Ultimate Mockup";
            this.Size = new Size(1100, 850);
            this.MinimumSize = new Size(800, 700);
            this.BackColor = Color.FromArgb(28, 28, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10F);
            this.StartPosition = FormStartPosition.CenterScreen;

            InitializeUI();
            
            I18n.LanguageChanged += UpdateTexts;
            UpdateTexts();
        }

        private void InitializeUI()
        {
            // 최상위 레이아웃 (3행: 헤더, 메인바디, 로그창)
            TableLayoutPanel rootP = new TableLayoutPanel { 
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0) 
            };
            rootP.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // Header
            rootP.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Body
            rootP.RowStyles.Add(new RowStyle(SizeType.Absolute, 140)); // Log
            this.Controls.Add(rootP);

            // ============================
            // 1. 헤더 영역 (Row 0)
            // ============================
            Panel headerPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(44, 44, 46), Margin = new Padding(0) };
            titleLabel = new Label { Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 16F, FontStyle.Bold) };
            headerPanel.Controls.Add(titleLabel);

            Label globeIcon = new Label { Text = "🌐", Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(this.ClientSize.Width - 190, 16), AutoSize = true, Font = new Font("Segoe UI", 12F) };
            langDropdown = new ComboBox { 
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(this.ClientSize.Width - 160, 15), Size = new Size(140, 30), 
                DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(58, 58, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat
            };
            langDropdown.Items.AddRange(I18n.Languages);
            langDropdown.SelectedItem = I18n.CurrentLanguage;
            langDropdown.SelectedIndexChanged += (s, e) => I18n.SetLanguage(langDropdown.SelectedItem.ToString());
            
            headerPanel.Controls.AddRange(new Control[] { globeIcon, langDropdown });
            rootP.Controls.Add(headerPanel, 0, 0);

            // ============================
            // 2. 메인 바디 영역 (Row 1)
            // ============================
            TableLayoutPanel bodyP = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(20) };
            bodyP.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220)); // 좌측 컨트롤
            bodyP.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // 우측 설정
            rootP.Controls.Add(bodyP, 0, 1);

            // 2-1. 좌측 컨트롤 버튼
            Panel controlPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = new Padding(0) };
            startButton = CreateBtn(Color.FromArgb(48, 209, 88), 0);
            stopButton = CreateBtn(Color.FromArgb(255, 69, 58), 70);
            captureButton = CreateBtn(Color.FromArgb(10, 132, 255), 140);
            controlPanel.Controls.AddRange(new Control[] { startButton, stopButton, captureButton });
            bodyP.Controls.Add(controlPanel, 0, 0);

            // 2-2. 우측 설정 영역
            Panel settingsPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(44, 44, 46), Margin = new Padding(0) };
            bodyP.Controls.Add(settingsPanel, 1, 0);

            TableLayoutPanel tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(20) };
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // Title
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // Top Row (Model & Switches)
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // Middle Row (Format)
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));  // Sliders
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));  // Target Label
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Targets FlowLayoutPanel
            settingsPanel.Controls.Add(tlp);

            settingsLabel = new Label { AutoSize = true, Font = new Font("Segoe UI", 12F, FontStyle.Bold) };
            tlp.Controls.Add(settingsLabel, 0, 0);

            // Row 1: Model & Toggles
            FlowLayoutPanel row1 = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            
            FlowLayoutPanel mdlP = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 30, 0) };
            modelLabel = new Label { AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(0, 5, 5, 0) };
            stdRadio = new RadioButton { AutoSize = true, Margin = new Padding(5, 3, 10, 3) };
            obbRadio = new RadioButton { AutoSize = true, Checked = true, Margin = new Padding(5, 3, 10, 3) };
            mdlP.Controls.AddRange(new Control[] { modelLabel, stdRadio, obbRadio });

            FlowLayoutPanel swP = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
            switchesLabel = new Label { AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(0, 5, 10, 0) };

            Panel tgl1 = new Panel { Size = new Size(180, 30) };
            overlayToggle = new ToggleSwitch { Location = new Point(0, 2), Checked = true };
            overlayL = new Label { Location = new Point(50, 2), AutoSize = true };
            overlayL.Tag = "EnableOverlay"; dynamicLabels.Add(overlayL);
            tgl1.Controls.AddRange(new Control[] { overlayToggle, overlayL });
            
            Button stickerSetupBtn = new Button { 
                Size = new Size(180, 30), BackColor = Color.FromArgb(58, 58, 60), ForeColor = Color.White, 
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            stickerSetupBtn.FlatAppearance.BorderSize = 0;
            stickerSetupBtn.Tag = "StickerSetupBtn"; dynamicLabels.Add(stickerSetupBtn);
            stickerSetupBtn.Click += (s, e) => { new StickerSetupForm().ShowDialog(); };

            swP.Controls.AddRange(new Control[] { switchesLabel, tgl1, stickerSetupBtn });
            row1.Controls.AddRange(new Control[] { mdlP, swP });
            tlp.Controls.Add(row1, 0, 1);

            // Row 2: Format
            FlowLayoutPanel fmtP = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            formatLabel = new Label { AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(0, 5, 10, 0) };
            mosRadio = new RadioButton { AutoSize = true, Checked = true, Margin = new Padding(5, 3, 10, 3) };
            blurRadio = new RadioButton { AutoSize = true, Margin = new Padding(5, 3, 10, 3) };
            boxRadio = new RadioButton { AutoSize = true, Margin = new Padding(5, 3, 10, 3) };
            fmtP.Controls.AddRange(new Control[] { formatLabel, mosRadio, blurRadio, boxRadio });
            tlp.Controls.Add(fmtP, 0, 2);

            // Row 3: Sliders
            FlowLayoutPanel row3 = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            
            Panel confP = new Panel { Size = new Size(320, 70), Margin = new Padding(0, 0, 20, 0) };
            confL = new Label { Location = new Point(0, 0), AutoSize = true, ForeColor = Color.Gray };
            confSlider = new TrackBar { Location = new Point(0, 25), Width = 300, Minimum = 1, Maximum = 100, Value = 40, TickFrequency = 10 };
            confP.Controls.AddRange(new Control[] { confL, confSlider });

            Panel strP = new Panel { Size = new Size(320, 70) };
            strL = new Label { Location = new Point(0, 0), AutoSize = true, ForeColor = Color.Gray };
            strSlider = new TrackBar { Location = new Point(0, 25), Width = 300, Minimum = 5, Maximum = 50, Value = 20, TickFrequency = 5 };
            strP.Controls.AddRange(new Control[] { strL, strSlider });

            row3.Controls.AddRange(new Control[] { confP, strP });
            tlp.Controls.Add(row3, 0, 3);

            // Row 4 & 5: Targets
            targetsLabel = new Label { AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 11F, FontStyle.Bold) };
            tlp.Controls.Add(targetsLabel, 0, 4);

            FlowLayoutPanel fp = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(34, 34, 36), Padding = new Padding(10) };
            
            var categories = new Dictionary<string, string[]> {
                { "Cat_Head", new[] { "Face_Female", "Face_Male", "Eyes" } },
                { "Cat_Torso", new[] { "Breast_Nude", "Breast_Underwear", "Breast_Clothed", "Armpit", "Navel" } },
                { "Cat_Lower", new[] { "Vulva_Nude", "Penis", "Anus", "Panty", "Hpis" } },
                { "Cat_Rear", new[] { "Butt_Nude", "Butt_Clothed" } },
                { "Cat_Other", new[] { "Hands", "Feet", "Shoes", "Body_Full", "Sex_Act" } }
            };

            foreach (var cat in categories)
            {
                // 불필요한 공백을 유발하던 중간 래핑 패널(groupBox) 완전 제거.
                // 사용자 요청으로 카테고리(파란 글씨) 삭제 -> 전체를 통짜 리스트로 병합
                /* Label catTitle = new Label { 
                    AutoSize = true, Margin = new Padding(3, 15, 0, 0),
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(10, 132, 255) 
                };
                catTitle.Tag = cat.Key; dynamicLabels.Add(catTitle);
                fp.Controls.Add(catTitle); */

                FlowLayoutPanel innerFp = new FlowLayoutPanel { 
                    AutoSize = true, WrapContents = true, Margin = new Padding(0, 5, 0, 0)
                };
                
                foreach (var itemKey in cat.Value)
                {
                    // 고정된 폭으로 인한 공백 부조화를 막기 위해 개별 버튼들도 자동 크기조정 플로우로 전환
                    FlowLayoutPanel itemP = new FlowLayoutPanel { 
                        AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, 
                        Margin = new Padding(0, 0, 15, 4), Height = 24
                    };
                    
                    var tg = new ToggleSwitch { Margin = new Padding(0, 2, 5, 0), Checked = true };
                    
                    var lbl = new Label { 
                        Margin = new Padding(0, 5, 2, 0), AutoSize = true, 
                        ForeColor = Color.LightGray, MinimumSize = new Size(0, 18)
                    };
                    lbl.Tag = itemKey; dynamicLabels.Add(lbl);

                    var stickerChk = new CheckBox { 
                        Margin = new Padding(3, 3, 5, 0), AutoSize = true, 
                        Font = new Font("Segoe UI", 9F), ForeColor = Color.LightGray
                    };
                    stickerChk.Tag = "StickerChk"; dynamicLabels.Add(stickerChk);

                    itemP.Controls.AddRange(new Control[] { tg, lbl, stickerChk });
                    innerFp.Controls.Add(itemP);
                }

                fp.Controls.Add(innerFp);

                fp.SizeChanged += (s, e) => {
                    innerFp.Width = fp.Width - 30; // 가로 폭 동기화는 그대로 유지하여 Wrap 발동
                };
            }

            tlp.Controls.Add(fp, 0, 5);

            // ============================
            // 3. 로그 영역 (Row 2)
            // ============================
            Panel logPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(20, 5, 20, 20), Margin = new Padding(0) };
            logLabel = new Label { Dock = DockStyle.Top, Height = 25, Font = new Font("Segoe UI", 11F, FontStyle.Bold) };
            logBox = new TextBox { 
                Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(20, 20, 22), ForeColor = Color.FromArgb(48, 209, 88), Font = new Font("Consolas", 9F)
            };
            logBox.Text = "[System] Fully Responsive UI Initialized.\r\n[System] Absolute Dock Z-Order overlap bug squashed.\r\n";
            logPanel.Controls.Add(logBox);
            logPanel.Controls.Add(logLabel);
            rootP.Controls.Add(logPanel, 0, 2);
        }

        private Button CreateBtn(Color bg, int y)
        {
            var b = new Button { Location = new Point(0, y), Size = new Size(200, 55), BackColor = bg, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F, FontStyle.Bold), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private void UpdateTexts()
        {
            this.Text = I18n.Get("Title");
            titleLabel.Text = I18n.Get("Title");
            startButton.Text = I18n.Get("Start");
            stopButton.Text = I18n.Get("Stop");
            captureButton.Text = I18n.Get("Capture");
            
            settingsLabel.Text = I18n.Get("Settings");
            modelLabel.Text = I18n.Get("ModelSelect");
            stdRadio.Text = I18n.Get("StdModel");
            obbRadio.Text = I18n.Get("ObbModel");
            
            switchesLabel.Text = I18n.Get("GlobalSwitches");
            formatLabel.Text = I18n.Get("FormatSelect");
            mosRadio.Text = I18n.Get("FmtMosaic");
            blurRadio.Text = I18n.Get("FmtBlur");
            boxRadio.Text = I18n.Get("FmtBlackBox");

            confL.Text = I18n.Get("Confidence");
            strL.Text = I18n.Get("Strength");

            targetsLabel.Text = I18n.Get("Targets");
            logLabel.Text = I18n.Get("Logs");

            foreach (var lbl in dynamicLabels)
                if (lbl.Tag != null) lbl.Text = I18n.Get(lbl.Tag.ToString());

            logBox.AppendText($"[System] Language updated to {I18n.CurrentLanguage}\r\n");
        }
    }

    // ==========================================
    // 스티커 매칭 설정 전용 팝업 창 (Mockup)
    // ==========================================
    public class StickerSetupForm : Form
    {
        public StickerSetupForm()
        {
            this.Text = "Sticker Mapping Configuration";
            this.Size = new Size(500, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(30, 30, 32);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10F);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            Label title = new Label { 
                Text = "대상 부위별 커스텀 스티커 매칭 (Per-Target Sticker Map)", 
                Dock = DockStyle.Top, Height = 60, TextAlign = ContentAlignment.MiddleCenter, 
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            };
            this.Controls.Add(title);

            FlowLayoutPanel fp = new FlowLayoutPanel { 
                Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20) 
            };
            this.Controls.Add(fp);

            Panel btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.FromArgb(44, 44, 46) };
            Button saveBtn = new Button { Text = "저장 및 적용 (Save)", Size = new Size(140, 40), Location = new Point(170, 10), BackColor = Color.FromArgb(10, 132, 255), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += (s, e) => this.Close();
            btnPanel.Controls.Add(saveBtn);
            this.Controls.Add(btnPanel);
            
            this.Controls.SetChildIndex(btnPanel, 0); 
            this.Controls.SetChildIndex(fp, 1);
            this.Controls.SetChildIndex(title, 2);

            string[] targets = { 
                "Face_Female", "Face_Male", "Eyes", "Breast_Nude", "Breast_Underwear", "Breast_Clothed", 
                "Armpit", "Navel", "Penis", "Vulva_Nude", "Anus", "Panty", "Hpis", 
                "Butt_Nude", "Butt_Clothed", "Hands", "Feet", "Shoes", "Body_Full", "Sex_Act" 
            };

            foreach (var t in targets)
            {
                Panel row = new Panel { Size = new Size(420, 35), Margin = new Padding(0, 0, 0, 5) };
                
                Label lbl = new Label { 
                    Text = I18n.Get(t), Location = new Point(10, 8), AutoSize = true, 
                    ForeColor = Color.LightGray, Font = new Font("Segoe UI", 10F, FontStyle.Bold)
                };
                
                Button pickBtn = new Button { 
                    Text = "스티커(PNG) 선택하기... [0개]", Location = new Point(200, 2), Size = new Size(200, 30), 
                    BackColor = Color.FromArgb(58, 58, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat
                };
                pickBtn.FlatAppearance.BorderSize = 0;
                pickBtn.Click += (s, e) => {
                    using (var picker = new StickerPickerForm(t)) {
                        picker.ShowDialog();
                        pickBtn.Text = $"선택된 스티커 [{picker.SelectedCount}개]";
                        if (picker.SelectedCount > 0) pickBtn.BackColor = Color.FromArgb(10, 132, 255);
                        else pickBtn.BackColor = Color.FromArgb(58, 58, 60);
                    }
                };

                row.Controls.AddRange(new Control[] { lbl, pickBtn });
                fp.Controls.Add(row);
            }
        }
    }

    // ==========================================
    // 다중 Png 이미지 선택 팝업 UI (Sticker Picker)
    // ==========================================
    public class StickerPickerForm : Form
    {
        public int SelectedCount { get; private set; } = 0;

        public StickerPickerForm(string targetName)
        {
            this.Text = $"Sticker Picker - {I18n.Get(targetName)}";
            this.Size = new Size(700, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(30, 30, 32);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9F);

            Label title = new Label { 
                Text = $"'{I18n.Get(targetName)}' 부위에 반영될 실제 이미지 중복 선택", 
                Dock = DockStyle.Top, Height = 50, TextAlign = ContentAlignment.MiddleCenter, 
                Font = new Font("Segoe UI", 11F, FontStyle.Bold)
            };
            this.Controls.Add(title);

            FlowLayoutPanel fp = new FlowLayoutPanel { 
                Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(15), BackColor = Color.FromArgb(20, 20, 22)
            };
            this.Controls.Add(fp);

            Panel btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = Color.FromArgb(44, 44, 46) };
            Button saveBtn = new Button { Text = "적용 (Apply)", Size = new Size(120, 35), Location = new Point(280, 12), BackColor = Color.FromArgb(48, 209, 88), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            saveBtn.FlatAppearance.BorderSize = 0;
            saveBtn.Click += (s, e) => {
                SelectedCount = fp.Controls.OfType<Panel>().Count(p => p.Controls.OfType<CheckBox>().FirstOrDefault()?.Checked == true);
                this.Close();
            };
            btnPanel.Controls.Add(saveBtn);
            this.Controls.Add(btnPanel);
            
            this.Controls.SetChildIndex(btnPanel, 0); 
            this.Controls.SetChildIndex(fp, 1);
            this.Controls.SetChildIndex(title, 2);

            try
            {
                string dir = System.IO.Path.Combine(Application.StartupPath, "Resources", "Stickers");
                if (!System.IO.Directory.Exists(dir))
                    dir = @"C:\Users\Win\Desktop\BetaChip\Resources\Stickers"; // Fallback for dev

                if (System.IO.Directory.Exists(dir))
                {
                    string[] files = System.IO.Directory.GetFiles(dir, "*.png");
                    foreach (var file in files)
                    {
                        Panel container = new Panel { Size = new Size(100, 120), Margin = new Padding(10, 10, 10, 15) };
                        
                        PictureBox pb = new PictureBox {
                            ImageLocation = file, SizeMode = PictureBoxSizeMode.Zoom, 
                            Dock = DockStyle.Top, Height = 90, BackColor = Color.Black
                        };
                        
                        CheckBox chk = new CheckBox {
                            Text = System.IO.Path.GetFileNameWithoutExtension(file),
                            Dock = DockStyle.Bottom, Height = 30, AutoEllipsis = true, 
                            TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.LightGray
                        };
                        
                        container.Controls.Add(pb);
                        container.Controls.Add(chk);
                        container.Click += (s, e) => { chk.Checked = !chk.Checked; };
                        pb.Click += (s, e) => { chk.Checked = !chk.Checked; };

                        fp.Controls.Add(container);
                    }
                }
                else
                {
                    fp.Controls.Add(new Label { Text = "Stickers directory not found.", ForeColor = Color.Red, AutoSize = true });
                }
            }
            catch (Exception ex)
            {
                fp.Controls.Add(new Label { Text = "Error loading images: " + ex.Message, ForeColor = Color.Red, AutoSize = true });
            }
        }
    }
}
