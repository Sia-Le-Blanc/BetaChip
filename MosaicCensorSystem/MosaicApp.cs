#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Windows.Forms;
using OpenCvSharp;
using MosaicCensorSystem.Capture;
using MosaicCensorSystem.Overlay;
using MosaicCensorSystem.UI;
using MosaicCensorSystem.Diagnostics;

// 네임스페이스 충돌 해결 - Detection 네임스페이스는 using하지 않음
// using MosaicCensorSystem.Detection;

namespace MosaicCensorSystem
{
    public class MosaicApp
    {
        // 1. 필드/프로퍼티 선언
        public Form Root { get; private set; }
        
        private ScreenCapturer capturer;
        private MosaicCensorSystem.Detection.MosaicProcessor mosaicProcessor; // 전체 경로 사용
        private FullscreenOverlay overlay;
        
        private ScrollablePanel scrollableContainer;
        private Label statusLabel;
        private Dictionary<string, CheckBox> targetCheckBoxes = new Dictionary<string, CheckBox>();
        private TrackBar strengthSlider;
        private Label strengthLabel;
        private TrackBar confidenceSlider;
        private Label confidenceLabel;
        private RadioButton mosaicRadioButton;
        private RadioButton blurRadioButton;
        private Label censorTypeLabel;
        private TextBox logTextBox;
        private Button startButton;
        private Button stopButton;
        private Button testButton;
        
        // 기능 레벨 컨트롤
        private ComboBox featureLevelCombo;
        private Label featureLevelLabel;
        private CheckBox enableDetectionCheckBox;
        private CheckBox enableCensoringCheckBox;
        private TrackBar fpsSlider;
        private Label fpsLabel;
        
        // 스레드 관리
        private readonly object isRunningLock = new object();
        private readonly object statsLock = new object();
        private volatile bool isRunning = false;
        private volatile bool isDisposing = false;
        private Thread processThread;
        
        // 설정값들
        private int targetFPS = 15;
        private float currentConfidence = 0.3f; // 더 낮은 기본값으로 설정
        private int currentStrength = 20;
        private bool enableDetection = false;
        private bool enableCensoring = false;
        
        private Dictionary<string, object> stats = new Dictionary<string, object>
        {
            ["frames_processed"] = 0,
            ["objects_detected"] = 0,
            ["censor_applied"] = 0,
            ["start_time"] = null,
            ["detection_time"] = 0.0,
            ["fps"] = 0.0
        };
        
        private bool isDragging = false;
        private System.Drawing.Point dragStartPoint;

        // 2. 생성자
        public MosaicApp()
        {
            Root = new Form
            {
                Text = "ONNX 가이드 기반 화면 검열 시스템 v7.0 (MosaicProcessor)",
                Size = new System.Drawing.Size(500, 850),
                MinimumSize = new System.Drawing.Size(450, 650),
                StartPosition = FormStartPosition.CenterScreen
            };
            
            try
            {
                Console.WriteLine("🔧 ONNX 가이드 기반 MosaicProcessor로 컴포넌트 초기화 중...");
                
                InitializeComponents();
                CreateGui();
                
                Root.FormClosed += OnFormClosed;
                Root.FormClosing += OnFormClosing;
                
                Console.WriteLine("✅ MosaicProcessor 기반 MosaicApp 초기화 완료");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ MosaicApp 초기화 실패: {ex.Message}");
                MessageBox.Show($"초기화 실패: {ex.Message}\n\n프로그램을 종료합니다.", "치명적 오류", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

        // 3. Public 메서드
        public void Run()
        {
            Console.WriteLine("🔄 ONNX 가이드 기반 화면 검열 시스템 v7.0 시작");
            Console.WriteLine("=" + new string('=', 60));
            
            try
            {
                Application.Run(Root);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n🛑 MosaicProcessor 모드 오류 발생: {ex.Message}");
                LogMessage($"❌ 애플리케이션 오류: {ex.Message}");
            }
            finally
            {
                Cleanup();
            }
        }

        // 4. Private 초기화 메서드들
        private void InitializeComponents()
        {
            try
            {
                Console.WriteLine("1. ScreenCapturer 초기화 중...");
                capturer = new ScreenCapturer(Config.GetSection("capture"));
                Console.WriteLine("✅ ScreenCapturer 초기화 완료");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ScreenCapturer 초기화 실패: {ex.Message}");
                capturer = null;
            }

            try
            {
                Console.WriteLine("2. 진단 도구 실행 중...");
                OnnxDiagnostics.RunFullDiagnostics();
                
                Console.WriteLine("3. MosaicProcessor 초기화 중...");
                // MosaicProcessor 직접 사용 (전체 경로)
                mosaicProcessor = new MosaicCensorSystem.Detection.MosaicProcessor(null, Config.GetSection("mosaic"));
                
                Console.WriteLine($"🔍 프로세서 타입: {mosaicProcessor.GetType().FullName}");
                Console.WriteLine($"🔍 모델 로드 상태: {mosaicProcessor.IsModelLoaded()}");
                Console.WriteLine($"🔍 가속 모드: {mosaicProcessor.GetAccelerationMode()}");
                Console.WriteLine($"🔍 사용 가능한 클래스: [{string.Join(", ", mosaicProcessor.GetAvailableClasses())}]");
                
                Console.WriteLine("✅ MosaicProcessor 초기화 완료");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ MosaicProcessor 초기화 실패: {ex.Message}");
                mosaicProcessor = null;
            }

            try
            {
                Console.WriteLine("4. FullscreenOverlay 초기화 중...");
                overlay = new FullscreenOverlay(Config.GetSection("overlay"));
                Console.WriteLine("✅ FullscreenOverlay 초기화 완료");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ FullscreenOverlay 초기화 실패: {ex.Message}");
                overlay = null;
            }
        }

        private void CreateGui()
        {
            var titleLabel = new Label
            {
                Text = "🤖 ONNX 가이드 기반 화면 검열 시스템 v7.0 (MosaicProcessor)",
                Font = new Font("Arial", 12, FontStyle.Bold),
                BackColor = Color.LightGreen,
                BorderStyle = BorderStyle.Fixed3D,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 40,
                Dock = DockStyle.Top
            };
            
            SetupWindowDragging(titleLabel);
            
            var scrollInfo = new Label
            {
                Text = "🚀 완전한 ONNX 가이드 기반 프로세서 - GPU 가속 + 트래킹 + 캐싱",
                Font = new Font("Arial", 9),
                ForeColor = Color.DarkGreen,
                BackColor = Color.LightCyan,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 25,
                Dock = DockStyle.Top
            };
            
            scrollableContainer = new ScrollablePanel
            {
                Dock = DockStyle.Fill
            };
            
            Root.Controls.Add(scrollableContainer);
            Root.Controls.Add(scrollInfo);
            Root.Controls.Add(titleLabel);
            
            CreateContent(scrollableContainer.ScrollableFrame);
        }

        private void SetupWindowDragging(Control control)
        {
            control.MouseDown += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    isDragging = true;
                    dragStartPoint = e.Location;
                }
            };
            
            control.MouseMove += (sender, e) =>
            {
                if (isDragging)
                {
                    var p = Root.PointToScreen(e.Location);
                    Root.Location = new System.Drawing.Point(p.X - dragStartPoint.X, p.Y - dragStartPoint.Y);
                }
            };
            
            control.MouseUp += (sender, e) =>
            {
                isDragging = false;
            };
        }

        private void CreateContent(Panel parent)
        {
            int y = 10;
            
            var dragInfo = new Label
            {
                Text = "💡 초록색 제목을 드래그해서 창을 이동하세요",
                Font = new Font("Arial", 9),
                ForeColor = Color.Gray,
                Location = new System.Drawing.Point(10, y),
                AutoSize = true
            };
            parent.Controls.Add(dragInfo);
            y += 30;
            
            statusLabel = new Label
            {
                Text = "⭕ MosaicProcessor 대기 중",
                Font = new Font("Arial", 12),
                ForeColor = Color.Red,
                Location = new System.Drawing.Point(10, y),
                AutoSize = true
            };
            parent.Controls.Add(statusLabel);
            y += 40;
            
            // 기능 레벨 선택
            var featureLevelGroup = new GroupBox
            {
                Text = "🚀 기능 레벨 선택 (ONNX 가이드 기반)",
                Location = new System.Drawing.Point(10, y),
                Size = new System.Drawing.Size(460, 100)
            };
            
            featureLevelLabel = new Label
            {
                Text = "기능 레벨:",
                Location = new System.Drawing.Point(10, 25),
                AutoSize = true
            };
            featureLevelGroup.Controls.Add(featureLevelLabel);
            
            featureLevelCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new System.Drawing.Point(100, 22),
                Size = new System.Drawing.Size(340, 25)
            };
            featureLevelCombo.Items.AddRange(new string[]
            {
                "레벨 1: 화면 캡처만",
                "레벨 2: 캡처 + 성능 향상 (고fps)",
                "레벨 3: 캡처 + 객체 감지 (검열 없음)",
                "레벨 4: 캡처 + 감지 + 모자이크 검열",
                "레벨 5: 전체 기능 (감지 + 검열 + 트래킹)"
            });
            featureLevelCombo.SelectedIndex = 4; // 기본을 레벨 5로 설정
            featureLevelCombo.SelectedIndexChanged += OnFeatureLevelChanged;
            featureLevelGroup.Controls.Add(featureLevelCombo);
            
            var levelInfo = new Label
            {
                Text = "💡 MosaicProcessor는 모든 기능이 최적화되어 있습니다",
                ForeColor = Color.DarkGreen,
                Font = new Font("Arial", 9),
                Location = new System.Drawing.Point(10, 55),
                Size = new System.Drawing.Size(440, 35)
            };
            featureLevelGroup.Controls.Add(levelInfo);
            
            parent.Controls.Add(featureLevelGroup);
            y += 110;
            
            // 성능 설정
            var performanceGroup = new GroupBox
            {
                Text = "⚡ 성능 설정",
                Location = new System.Drawing.Point(10, y),
                Size = new System.Drawing.Size(460, 120)
            };
            
            var fpsTextLabel = new Label
            {
                Text = "목표 FPS:",
                Location = new System.Drawing.Point(10, 25),
                AutoSize = true
            };
            performanceGroup.Controls.Add(fpsTextLabel);
            
            fpsSlider = new TrackBar
            {
                Minimum = 5,
                Maximum = 60,
                Value = targetFPS,
                TickFrequency = 5,
                Location = new System.Drawing.Point(100, 20),
                Size = new System.Drawing.Size(280, 45)
            };
            fpsSlider.ValueChanged += OnFpsChanged;
            performanceGroup.Controls.Add(fpsSlider);
            
            fpsLabel = new Label
            {
                Text = $"{targetFPS} fps",
                Location = new System.Drawing.Point(390, 25),
                AutoSize = true
            };
            performanceGroup.Controls.Add(fpsLabel);
            
            enableDetectionCheckBox = new CheckBox
            {
                Text = "🔍 객체 감지 활성화",
                Checked = true, // 기본적으로 활성화
                Location = new System.Drawing.Point(10, 70),
                AutoSize = true
            };
            enableDetectionCheckBox.CheckedChanged += OnDetectionToggle;
            performanceGroup.Controls.Add(enableDetectionCheckBox);
            
            enableCensoringCheckBox = new CheckBox
            {
                Text = "🎨 검열 효과 활성화",
                Checked = true, // 기본적으로 활성화
                Location = new System.Drawing.Point(200, 70),
                AutoSize = true
            };
            enableCensoringCheckBox.CheckedChanged += OnCensoringToggle;
            performanceGroup.Controls.Add(enableCensoringCheckBox);
            
            parent.Controls.Add(performanceGroup);
            y += 130;

            // 검열 효과 타입 선택 그룹
            var censorTypeGroup = new GroupBox
            {
                Text = "🎨 검열 효과 타입 선택",
                Location = new System.Drawing.Point(10, y),
                Size = new System.Drawing.Size(460, 80)
            };

            mosaicRadioButton = new RadioButton
            {
                Text = "🟦 모자이크",
                Checked = true,
                Location = new System.Drawing.Point(20, 25),
                AutoSize = true
            };
            mosaicRadioButton.CheckedChanged += OnCensorTypeChanged;

            blurRadioButton = new RadioButton
            {
                Text = "🌀 블러",
                Location = new System.Drawing.Point(200, 25),
                AutoSize = true
            };
            blurRadioButton.CheckedChanged += OnCensorTypeChanged;

            censorTypeLabel = new Label
            {
                Text = "현재: 모자이크",
                Font = new Font("Arial", 10, FontStyle.Bold),
                ForeColor = Color.Blue,
                Location = new System.Drawing.Point(20, 50),
                AutoSize = true
            };

            censorTypeGroup.Controls.Add(mosaicRadioButton);
            censorTypeGroup.Controls.Add(blurRadioButton);
            censorTypeGroup.Controls.Add(censorTypeLabel);
            parent.Controls.Add(censorTypeGroup);
            y += 90;
            
            var targetsGroup = new GroupBox
            {
                Text = "🎯 검열 대상 선택 (ONNX 가이드 기반)",
                Location = new System.Drawing.Point(10, y),
                Size = new System.Drawing.Size(460, 150)
            };
            
            // MosaicProcessor의 전체 클래스 목록 사용
            var allTargets = new[]
            {
                "얼굴", "가슴", "겨드랑이", "보지", "발",
                "몸 전체", "자지", "팬티", "눈", "손",
                "교미", "신발", "가슴_옷", "여성"
            };
            
            var defaultTargets = new List<string> { "얼굴", "눈", "손" }; // 안전한 기본값
            
            for (int i = 0; i < allTargets.Length; i++)
            {
                var target = allTargets[i];
                var row = i / 3; // 3열로 배치
                var col = i % 3;
                
                var checkbox = new CheckBox
                {
                    Text = target,
                    Checked = defaultTargets.Contains(target),
                    Location = new System.Drawing.Point(15 + col * 140, 30 + row * 25),
                    Size = new System.Drawing.Size(130, 20),
                    AutoSize = false
                };
                
                targetCheckBoxes[target] = checkbox;
                targetsGroup.Controls.Add(checkbox);
            }
            
            var targetNote = new Label
            {
                Text = "💡 ONNX 가이드의 14개 클래스 모두 지원",
                ForeColor = Color.DarkGreen,
                Font = new Font("Arial", 9),
                Location = new System.Drawing.Point(15, 120),
                AutoSize = true
            };
            targetsGroup.Controls.Add(targetNote);
            
            parent.Controls.Add(targetsGroup);
            y += 160;
            
            var settingsGroup = new GroupBox
            {
                Text = "⚙️ 고급 설정",
                Location = new System.Drawing.Point(10, y),
                Size = new System.Drawing.Size(460, 120)
            };
            
            var strengthTextLabel = new Label
            {
                Text = "검열 강도:",
                Location = new System.Drawing.Point(10, 25),
                AutoSize = true
            };
            settingsGroup.Controls.Add(strengthTextLabel);
            
            strengthSlider = new TrackBar
            {
                Minimum = 10,
                Maximum = 40,
                Value = currentStrength,
                TickFrequency = 5,
                Location = new System.Drawing.Point(120, 20),
                Size = new System.Drawing.Size(280, 45)
            };
            strengthSlider.ValueChanged += OnStrengthChanged;
            settingsGroup.Controls.Add(strengthSlider);
            
            strengthLabel = new Label
            {
                Text = currentStrength.ToString(),
                Location = new System.Drawing.Point(410, 25),
                AutoSize = true
            };
            settingsGroup.Controls.Add(strengthLabel);
            
            var confidenceTextLabel = new Label
            {
                Text = "감지 신뢰도:",
                Location = new System.Drawing.Point(10, 65),
                AutoSize = true
            };
            settingsGroup.Controls.Add(confidenceTextLabel);
            
            confidenceSlider = new TrackBar
            {
                Minimum = 10, // 0.1로 설정
                Maximum = 90,
                Value = (int)(currentConfidence * 100),
                TickFrequency = 10,
                Location = new System.Drawing.Point(120, 60),
                Size = new System.Drawing.Size(280, 45)
            };
            confidenceSlider.ValueChanged += OnConfidenceChanged;
            settingsGroup.Controls.Add(confidenceSlider);
            
            confidenceLabel = new Label
            {
                Text = currentConfidence.ToString("F1"),
                Location = new System.Drawing.Point(410, 65),
                AutoSize = true
            };
            settingsGroup.Controls.Add(confidenceLabel);
            
            parent.Controls.Add(settingsGroup);
            y += 130;
            
            var controlPanel = new Panel
            {
                BackColor = Color.LightGray,
                BorderStyle = BorderStyle.Fixed3D,
                Location = new System.Drawing.Point(10, y),
                Size = new System.Drawing.Size(460, 100)
            };
            
            var buttonLabel = new Label
            {
                Text = "🎮 ONNX 가이드 기반 MosaicProcessor 컨트롤",
                Font = new Font("Arial", 12, FontStyle.Bold),
                BackColor = Color.LightGray,
                Location = new System.Drawing.Point(10, 10),
                Size = new System.Drawing.Size(440, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };
            controlPanel.Controls.Add(buttonLabel);
            
            startButton = new Button
            {
                Text = "🚀 시작",
                BackColor = Color.DarkGreen,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Size = new System.Drawing.Size(120, 50),
                Location = new System.Drawing.Point(20, 40)
            };
            startButton.Click += StartProcessing;
            controlPanel.Controls.Add(startButton);
            
            stopButton = new Button
            {
                Text = "🛑 중지",
                BackColor = Color.Red,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Size = new System.Drawing.Size(120, 50),
                Location = new System.Drawing.Point(170, 40),
                Enabled = false
            };
            stopButton.Click += StopProcessing;
            controlPanel.Controls.Add(stopButton);
            
            testButton = new Button
            {
                Text = "🔍 캡처 테스트",
                BackColor = Color.Blue,
                ForeColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Size = new System.Drawing.Size(120, 50),
                Location = new System.Drawing.Point(320, 40)
            };
            testButton.Click += TestCapture;
            controlPanel.Controls.Add(testButton);
            
            parent.Controls.Add(controlPanel);
            y += 110;
            
            var logGroup = new GroupBox
            {
                Text = "📝 MosaicProcessor 로그",
                Location = new System.Drawing.Point(10, y),
                Size = new System.Drawing.Size(460, 120)
            };
            
            logTextBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Location = new System.Drawing.Point(10, 20),
                Size = new System.Drawing.Size(440, 90)
            };
            logGroup.Controls.Add(logTextBox);
            parent.Controls.Add(logGroup);
        }

        // 5. 이벤트 핸들러들
        private void OnFeatureLevelChanged(object sender, EventArgs e)
        {
            int level = featureLevelCombo.SelectedIndex + 1;
            LogMessage($"🔄 기능 레벨 변경: 레벨 {level}");
            
            switch (level)
            {
                case 1: // 캡처만
                    enableDetectionCheckBox.Enabled = false;
                    enableDetectionCheckBox.Checked = false;
                    enableCensoringCheckBox.Enabled = false;
                    enableCensoringCheckBox.Checked = false;
                    fpsSlider.Maximum = 30;
                    LogMessage("📋 레벨 1: 화면 캡처만 활성화");
                    break;
                    
                case 2: // 캡처 + 성능 향상
                    enableDetectionCheckBox.Enabled = false;
                    enableDetectionCheckBox.Checked = false;
                    enableCensoringCheckBox.Enabled = false;
                    enableCensoringCheckBox.Checked = false;
                    fpsSlider.Maximum = 60;
                    LogMessage("📋 레벨 2: 고성능 캡처 모드");
                    break;
                    
                case 3: // 캡처 + 감지
                    enableDetectionCheckBox.Enabled = true;
                    enableDetectionCheckBox.Checked = true;
                    enableCensoringCheckBox.Enabled = false;
                    enableCensoringCheckBox.Checked = false;
                    fpsSlider.Maximum = 40;
                    LogMessage("📋 레벨 3: 객체 감지 추가 (검열 없음)");
                    break;
                    
                case 4: // 캡처 + 감지 + 검열
                    enableDetectionCheckBox.Enabled = true;
                    enableDetectionCheckBox.Checked = true;
                    enableCensoringCheckBox.Enabled = true;
                    enableCensoringCheckBox.Checked = true;
                    fpsSlider.Maximum = 30;
                    LogMessage("📋 레벨 4: 기본 검열 기능 추가");
                    break;
                    
                case 5: // 전체 기능
                    enableDetectionCheckBox.Enabled = true;
                    enableDetectionCheckBox.Checked = true;
                    enableCensoringCheckBox.Enabled = true;
                    enableCensoringCheckBox.Checked = true;
                    fpsSlider.Maximum = 25;
                    LogMessage("📋 레벨 5: 전체 기능 활성화 (트래킹 + 캐싱)");
                    break;
            }
            
            enableDetection = enableDetectionCheckBox.Checked;
            enableCensoring = enableCensoringCheckBox.Checked;
        }

        private void OnFpsChanged(object sender, EventArgs e)
        {
            targetFPS = fpsSlider.Value;
            fpsLabel.Text = $"{targetFPS} fps";
            LogMessage($"⚡ 목표 FPS 변경: {targetFPS}");
        }

        private void OnDetectionToggle(object sender, EventArgs e)
        {
            enableDetection = enableDetectionCheckBox.Checked;
            LogMessage($"🔍 객체 감지: {(enableDetection ? "활성화" : "비활성화")}");
        }

        private void OnCensoringToggle(object sender, EventArgs e)
        {
            enableCensoring = enableCensoringCheckBox.Checked;
            LogMessage($"🎨 검열 효과: {(enableCensoring ? "활성화" : "비활성화")}");
        }

        private void OnCensorTypeChanged(object sender, EventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.Checked)
            {
                try
                {
                    MosaicCensorSystem.Detection.CensorType newType = mosaicRadioButton.Checked ? 
                        MosaicCensorSystem.Detection.CensorType.Mosaic : 
                        MosaicCensorSystem.Detection.CensorType.Blur;
                    mosaicProcessor?.SetCensorType(newType);
                    
                    string typeText = newType == MosaicCensorSystem.Detection.CensorType.Mosaic ? "모자이크" : "블러";
                    censorTypeLabel.Text = $"현재: {typeText}";
                    
                    LogMessage($"🎨 검열 타입 변경: {typeText}");
                }
                catch (Exception ex)
                {
                    LogMessage($"❌ 검열 타입 변경 오류: {ex.Message}");
                }
            }
        }

        private void OnStrengthChanged(object sender, EventArgs e)
        {
            currentStrength = strengthSlider.Value;
            strengthLabel.Text = currentStrength.ToString();
            mosaicProcessor?.SetStrength(currentStrength);
            LogMessage($"💪 검열 강도 변경: {currentStrength}");
        }

        private void OnConfidenceChanged(object sender, EventArgs e)
        {
            currentConfidence = confidenceSlider.Value / 100.0f;
            confidenceLabel.Text = currentConfidence.ToString("F1");
            if (mosaicProcessor != null)
                mosaicProcessor.ConfThreshold = currentConfidence;
            LogMessage($"🔍 신뢰도 변경: {currentConfidence:F1}");
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                isDisposing = true;
                
                lock (isRunningLock)
                {
                    if (isRunning)
                    {
                        StopProcessing(null, null);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 폼 종료 중 오류: {ex.Message}");
            }
        }

        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                isDisposing = true;
                Cleanup();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 폼 종료 후 오류: {ex.Message}");
            }
        }

        // 6. 기능 메서드들
        private void TestCapture(object sender, EventArgs e)
        {
            try
            {
                LogMessage("🔍 화면 캡처 테스트 시작");
                
                if (capturer == null)
                {
                    LogMessage("❌ ScreenCapturer가 초기화되지 않았습니다");
                    MessageBox.Show("ScreenCapturer가 초기화되지 않았습니다!", "테스트 실패", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                Mat testFrame = null;
                
                try
                {
                    testFrame = capturer.GetFrame();
                    
                    if (testFrame != null && !testFrame.Empty())
                    {
                        LogMessage($"✅ 캡처 성공! 크기: {testFrame.Width}x{testFrame.Height}");
                        
                        // MosaicProcessor로 테스트 감지
                        if (mosaicProcessor != null && mosaicProcessor.IsModelLoaded())
                        {
                            LogMessage("🔍 객체 감지 테스트 실행...");
                            var detections = mosaicProcessor.DetectObjects(testFrame);
                            LogMessage($"🎯 감지 결과: {detections.Count}개 객체");
                            
                            if (detections.Count > 0)
                            {
                                for (int i = 0; i < Math.Min(3, detections.Count); i++)
                                {
                                    var det = detections[i];
                                    LogMessage($"  {i+1}. {det.ClassName} (신뢰도: {det.Confidence:F3})");
                                }
                            }
                        }
                        
                        string testPath = Path.Combine(Environment.CurrentDirectory, "capture_test.jpg");
                        testFrame.SaveImage(testPath);
                        LogMessage($"💾 테스트 이미지 저장됨: {testPath}");
                        
                        MessageBox.Show($"캡처 테스트 성공!\n\n크기: {testFrame.Width}x{testFrame.Height}\n감지: {(mosaicProcessor?.IsModelLoaded() == true ? "가능" : "불가능")}\n저장: {testPath}", 
                                      "테스트 성공", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        LogMessage("❌ 캡처 실패");
                        MessageBox.Show("캡처 실패!", "테스트 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                finally
                {
                    testFrame?.Dispose();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 테스트 오류: {ex.Message}");
                MessageBox.Show($"테스트 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

// MosaicApp.cs의 StartProcessing 메서드만 크래시 방지 버전으로 교체

private void StartProcessing(object sender, EventArgs e)
{
    try
    {
        Console.WriteLine("🚀 크래시 방지 MosaicProcessor StartProcessing 시작");
        
        // 전역 예외 핸들러 설정
        AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
        {
            Console.WriteLine($"💥 처리되지 않은 예외: {ex.ExceptionObject}");
            LogMessage($"💥 치명적 오류: {ex.ExceptionObject}");
        };
        
        Application.ThreadException += (s, ex) =>
        {
            Console.WriteLine($"💥 스레드 예외: {ex.Exception}");
            LogMessage($"💥 스레드 오류: {ex.Exception.Message}");
        };
        
        lock (isRunningLock)
        {
            if (isRunning)
            {
                LogMessage("⚠️ 이미 실행 중");
                return;
            }
            
            if (isDisposing)
            {
                LogMessage("⚠️ 종료 중이므로 시작할 수 없음");
                return;
            }
        }

        // 기본 상태 체크 (강화)
        Console.WriteLine("🔍 1단계: 기본 컴포넌트 상태 체크");
        
        if (capturer == null)
        {
            var errorMsg = "화면 캡처 모듈이 초기화되지 않았습니다!";
            Console.WriteLine($"❌ {errorMsg}");
            MessageBox.Show(errorMsg, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Console.WriteLine("✅ 캡처러 상태 OK");
        
        if (overlay == null)
        {
            var errorMsg = "오버레이가 초기화되지 않았습니다!";
            Console.WriteLine($"❌ {errorMsg}");
            MessageBox.Show(errorMsg, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Console.WriteLine("✅ 오버레이 상태 OK");

        // 선택된 기능 레벨 확인
        int level = featureLevelCombo.SelectedIndex + 1;
        string levelDescription = featureLevelCombo.SelectedItem.ToString();
        
        Console.WriteLine($"🔍 2단계: 기능 레벨 체크 - 레벨 {level}");
        
        var selectedTargets = new List<string>();
        foreach (var kvp in targetCheckBoxes)
        {
            if (kvp.Value.Checked)
                selectedTargets.Add(kvp.Key);
        }

        if (selectedTargets.Count == 0)
            selectedTargets.Add("얼굴"); // 기본값

        Console.WriteLine($"🎯 선택된 타겟들: {string.Join(", ", selectedTargets)}");

        // MosaicProcessor 상태 체크 (강화)
        Console.WriteLine("🔍 3단계: MosaicProcessor 상태 체크");
        
        if (mosaicProcessor == null)
        {
            var errorMsg = "MosaicProcessor가 초기화되지 않았습니다!";
            Console.WriteLine($"❌ {errorMsg}");
            MessageBox.Show(errorMsg, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Console.WriteLine("✅ MosaicProcessor 인스턴스 OK");
        
        // 모델 로드 상태 체크
        bool modelLoaded = false;
        try
        {
            modelLoaded = mosaicProcessor.IsModelLoaded();
            Console.WriteLine($"✅ 모델 로드 상태: {modelLoaded}");
        }
        catch (Exception modelEx)
        {
            Console.WriteLine($"❌ 모델 상태 체크 오류: {modelEx.Message}");
            modelLoaded = false;
        }
        
        if (enableDetection && !modelLoaded)
        {
            var errorMsg = "객체 감지가 활성화되었지만 모델이 로드되지 않았습니다!\n\n" +
                "옵션:\n" +
                "1. 레벨을 1-2로 낮춰서 캡처만 테스트\n" +
                "2. best.onnx 파일이 Resources 폴더에 있는지 확인\n" +
                "3. 프로그램을 다시 시작";
            
            Console.WriteLine($"❌ {errorMsg}");
            MessageBox.Show(errorMsg, "모델 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 사용자 확인 (간소화)
        Console.WriteLine("🔍 4단계: 사용자 확인");
        
        var result = MessageBox.Show(
            $"안전 모드로 시작하시겠습니까?\n\n" +
            $"• {levelDescription}\n" +
            $"• 객체 감지: {(enableDetection ? "활성화" : "비활성화")}\n" +
            $"• 검열 효과: {(enableCensoring ? "활성화" : "비활성화")}\n" +
            $"• 모델 상태: {(modelLoaded ? "로드됨" : "로드 안됨")}\n\n" +
            "계속하시겠습니까?",
            "안전 모드 시작 확인",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        
        if (result != DialogResult.Yes)
        {
            Console.WriteLine("🛑 사용자가 취소함");
            return;
        }
        
        Console.WriteLine("🔍 5단계: 프로세서 설정 적용");
        
        // 프로세서 설정 (안전하게)
        if (mosaicProcessor != null && enableDetection && modelLoaded)
        {
            try
            {
                Console.WriteLine("⚙️ 타겟 설정 중...");
                mosaicProcessor.SetTargets(selectedTargets);
                
                Console.WriteLine("⚙️ 강도 설정 중...");
                mosaicProcessor.SetStrength(currentStrength);
                
                Console.WriteLine("⚙️ 신뢰도 설정 중...");
                mosaicProcessor.ConfThreshold = currentConfidence;
                
                Console.WriteLine("⚙️ 검열 타입 설정 중...");
                mosaicProcessor.SetCensorType(mosaicRadioButton.Checked ? 
                    MosaicCensorSystem.Detection.CensorType.Mosaic : 
                    MosaicCensorSystem.Detection.CensorType.Blur);
                
                Console.WriteLine("✅ 프로세서 설정 완료");
            }
            catch (Exception settingEx)
            {
                Console.WriteLine($"❌ 프로세서 설정 오류: {settingEx.Message}");
                MessageBox.Show($"프로세서 설정 오류: {settingEx.Message}", "설정 오류", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
        
        Console.WriteLine("🔍 6단계: 실행 상태 설정");
        
        // 실행 상태 설정
        lock (isRunningLock)
        {
            isRunning = true;
        }
        
        lock (statsLock)
        {
            stats["start_time"] = DateTime.Now;
            stats["frames_processed"] = 0;
            stats["objects_detected"] = 0;
            stats["censor_applied"] = 0;
            stats["detection_time"] = 0.0;
            stats["fps"] = 0.0;
        }
        
        // UI 상태 업데이트
        try
        {
            statusLabel.Text = $"✅ 레벨 {level} 준비 중...";
            statusLabel.ForeColor = Color.Orange;
            startButton.Enabled = false;
            stopButton.Enabled = true;
            featureLevelCombo.Enabled = false;
            
            Console.WriteLine("✅ UI 상태 업데이트 완료");
        }
        catch (Exception uiEx)
        {
            Console.WriteLine($"❌ UI 업데이트 오류: {uiEx.Message}");
        }
        
        // 캡처 테스트 먼저 실행
        Console.WriteLine("🔍 7단계: 캡처 기능 테스트");
        
        try
        {
            Mat testFrame = capturer.GetFrame();
            if (testFrame == null || testFrame.Empty())
            {
                throw new Exception("캡처 테스트 실패 - null 또는 빈 프레임");
            }
            
            Console.WriteLine($"✅ 캡처 테스트 성공: {testFrame.Width}x{testFrame.Height}");
            testFrame.Dispose();
        }
        catch (Exception captureTestEx)
        {
            Console.WriteLine($"❌ 캡처 테스트 실패: {captureTestEx.Message}");
            MessageBox.Show($"캡처 테스트 실패: {captureTestEx.Message}\n\n" +
                "화면 캡처에 문제가 있습니다. 프로그램을 다시 시작해주세요.", 
                "캡처 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            
            StopProcessing(null, null);
            return;
        }
        
        // 오버레이 시작 (안전하게)
        Console.WriteLine("🔍 8단계: 오버레이 시작");
        
        try
        {
            Console.WriteLine("🖼️ 오버레이 Show() 호출...");
            bool overlayResult = overlay.Show();
            
            if (!overlayResult)
            {
                throw new Exception("overlay.Show() 반환값이 false");
            }
            
            Console.WriteLine("✅ 오버레이 시작 성공");
            
            // 오버레이 상태 확인
            System.Threading.Thread.Sleep(500); // 0.5초 대기
            
            if (!overlay.IsWindowVisible())
            {
                throw new Exception("오버레이 창이 보이지 않음");
            }
            
            Console.WriteLine("✅ 오버레이 가시성 확인 완료");
        }
        catch (Exception overlayEx)
        {
            Console.WriteLine($"❌ 오버레이 시작 실패: {overlayEx.Message}");
            MessageBox.Show($"오버레이 시작 실패: {overlayEx.Message}\n\n" +
                "화면 오버레이에 문제가 있습니다.", "오버레이 오류", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            
            StopProcessing(null, null);
            return;
        }
        
        // 처리 스레드 시작 (매우 안전하게)
        Console.WriteLine("🔍 9단계: 처리 스레드 시작");
        
        try
        {
            Console.WriteLine("🧵 스레드 생성 중...");
            
            processThread = new Thread(() => {
                try
                {
                    Console.WriteLine("🧵 ProcessingLoop 스레드 시작됨");
                    SafeProcessingLoop();
                }
                catch (Exception threadEx)
                {
                    Console.WriteLine($"💥 ProcessingLoop 스레드 예외: {threadEx}");
                    LogMessage($"💥 스레드 치명적 오류: {threadEx.Message}");
                }
                finally
                {
                    Console.WriteLine("🧵 ProcessingLoop 스레드 종료됨");
                }
            })
            {
                Name = "SafeMosaicProcessorThread",
                IsBackground = true,
                Priority = ThreadPriority.Normal
            };
            
            processThread.SetApartmentState(ApartmentState.MTA);
            
            Console.WriteLine("🧵 스레드 시작...");
            processThread.Start();
            
            // 스레드 시작 확인
            System.Threading.Thread.Sleep(100);
            
            if (!processThread.IsAlive)
            {
                throw new Exception("스레드가 시작되지 않았음");
            }
            
            Console.WriteLine("✅ 처리 스레드 시작 성공");
        }
        catch (Exception threadEx)
        {
            Console.WriteLine($"❌ 처리 스레드 시작 실패: {threadEx.Message}");
            MessageBox.Show($"처리 스레드 시작 실패: {threadEx.Message}", "스레드 오류", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            
            StopProcessing(null, null);
            return;
        }
        
        // 최종 UI 업데이트
        try
        {
            statusLabel.Text = $"✅ 레벨 {level} 실행 중 (안전 모드)";
            statusLabel.ForeColor = Color.DarkGreen;
            Console.WriteLine("✅ 최종 UI 업데이트 완료");
        }
        catch (Exception finalUiEx)
        {
            Console.WriteLine($"❌ 최종 UI 업데이트 오류: {finalUiEx.Message}");
        }
        
        LogMessage($"🚀 안전 모드 시작 완료! 레벨={level}, 감지={enableDetection}, 검열={enableCensoring}");
        Console.WriteLine("🎉 StartProcessing 성공적으로 완료!");
        
    }
    catch (Exception ex)
    {
        Console.WriteLine($"💥 StartProcessing 최상위 예외: {ex}");
        LogMessage($"💥 시작 치명적 오류: {ex.Message}");
        
        MessageBox.Show($"시작 중 치명적 오류가 발생했습니다:\n\n{ex.Message}\n\n" +
            "스택 트레이스:\n{ex.StackTrace}", "치명적 오류", 
            MessageBoxButtons.OK, MessageBoxIcon.Error);
        
        try
        {
            StopProcessing(null, null);
        }
        catch (Exception stopEx)
        {
            Console.WriteLine($"💥 정리 중에도 오류: {stopEx.Message}");
        }
    }
}

// 새로운 안전한 처리 루프
private void SafeProcessingLoop()
{
    Console.WriteLine("🛡️ 안전한 ProcessingLoop 시작");
    int frameCount = 0;
    DateTime lastLogTime = DateTime.Now;
    var frameTimes = new List<double>();
    
    try
    {
        while (true)
        {
            var frameStartTime = DateTime.Now;
            
            try
            {
                // 실행 상태 체크
                bool shouldRun;
                lock (isRunningLock)
                {
                    shouldRun = isRunning && !isDisposing;
                }
                
                if (!shouldRun)
                {
                    Console.WriteLine("🛑 안전한 ProcessingLoop 정상 종료");
                    break;
                }
                
                frameCount++;
                
                // 간단한 캡처만 (크래시 방지)
                Mat capturedFrame = null;
                Mat processedFrame = null;
                
                try
                {
                    // 캡처 시도
                    if (capturer != null)
                    {
                        capturedFrame = capturer.GetFrame();
                        
                        if (capturedFrame != null && !capturedFrame.Empty())
                        {
                            processedFrame = capturedFrame.Clone();
                            
                            // 10프레임마다 로그
                            if (frameCount % 10 == 0)
                            {
                                Console.WriteLine($"📸 안전 모드 프레임 #{frameCount}: {capturedFrame.Width}x{capturedFrame.Height}");
                            }
                        }
                        else
                        {
                            if (frameCount % 30 == 0)
                            {
                                Console.WriteLine($"⚠️ 프레임 #{frameCount}: 캡처 실패");
                            }
                            Thread.Sleep(50);
                            continue;
                        }
                    }
                    else
                    {
                        Console.WriteLine("❌ capturer가 null");
                        Thread.Sleep(100);
                        continue;
                    }
                    
                    // 객체 감지 (매우 안전하게)
                    if (enableDetection && mosaicProcessor != null)
                    {
                        try
                        {
                            if (frameCount % 10 == 0)
                            {
                                Console.WriteLine($"🔍 프레임 #{frameCount}: 안전 모드 객체 감지 시도");
                            }
                            
                            var detections = mosaicProcessor.DetectObjects(capturedFrame);
                            
                            if (frameCount % 10 == 0 || (detections != null && detections.Count > 0))
                            {
                                Console.WriteLine($"✅ 프레임 #{frameCount}: 감지 완료 - {detections?.Count ?? 0}개");
                            }
                            
                            // 검열 적용 (안전하게)
                            if (enableCensoring && detections != null && detections.Count > 0)
                            {
                                try
                                {
                                    int applied = 0;
                                    foreach (var detection in detections.Take(3)) // 최대 3개만
                                    {
                                        mosaicProcessor.ApplySingleCensorOptimized(processedFrame, detection);
                                        applied++;
                                    }
                                    
                                    if (applied > 0 && frameCount % 10 == 0)
                                    {
                                        Console.WriteLine($"🎨 프레임 #{frameCount}: 검열 적용 - {applied}개");
                                    }
                                }
                                catch (Exception censorEx)
                                {
                                    if (frameCount % 30 == 0)
                                    {
                                        Console.WriteLine($"⚠️ 검열 오류 (무시됨): {censorEx.Message}");
                                    }
                                }
                            }
                        }
                        catch (Exception detectEx)
                        {
                            if (frameCount % 30 == 0)
                            {
                                Console.WriteLine($"⚠️ 감지 오류 (무시됨): {detectEx.Message}");
                            }
                        }
                    }
                    
                    // 오버레이 업데이트 (안전하게)
                    try
                    {
                        if (overlay != null && overlay.IsWindowVisible() && processedFrame != null)
                        {
                            overlay.UpdateFrame(processedFrame);
                        }
                    }
                    catch (Exception overlayEx)
                    {
                        if (frameCount % 30 == 0)
                        {
                            Console.WriteLine($"⚠️ 오버레이 오류 (무시됨): {overlayEx.Message}");
                        }
                    }
                    
                }
                catch (Exception frameEx)
                {
                    Console.WriteLine($"⚠️ 프레임 #{frameCount} 처리 오류 (무시됨): {frameEx.Message}");
                    Thread.Sleep(100);
                }
                finally
                {
                    // 안전한 리소스 정리
                    try
                    {
                        capturedFrame?.Dispose();
                        processedFrame?.Dispose();
                    }
                    catch { }
                }
                
                // 프레임 시간 기록
                var frameTime = (DateTime.Now - frameStartTime).TotalMilliseconds;
                frameTimes.Add(frameTime);
                if (frameTimes.Count > 50)
                    frameTimes.RemoveRange(0, 25);
                
                // 주기적 상태 로그 (30초마다)
                var now = DateTime.Now;
                if ((now - lastLogTime).TotalSeconds >= 30)
                {
                    lastLogTime = now;
                    
                    var avgFrameTime = frameTimes.Count > 0 ? frameTimes.Average() : 0;
                    Console.WriteLine($"🛡️ 안전 모드 상태: 프레임 {frameCount}, 평균 시간 {avgFrameTime:F1}ms");
                    LogMessage($"🛡️ 안전 모드 실행 중: {frameCount}프레임 처리됨");
                }
                
                // 오버레이 상태 체크
                try
                {
                    if (overlay != null && !overlay.IsWindowVisible())
                    {
                        Console.WriteLine("🛑 오버레이 창 닫힘 - 루프 종료");
                        lock (isRunningLock)
                        {
                            isRunning = false;
                        }
                        break;
                    }
                }
                catch { }
                
                // 적당한 대기 (FPS 조절)
                Thread.Sleep(Math.Max(1, 1000 / targetFPS));
                
            }
            catch (Exception loopEx)
            {
                Console.WriteLine($"⚠️ 루프 반복 오류 (복구 시도): {loopEx.Message}");
                Thread.Sleep(1000); // 1초 대기 후 복구 시도
            }
        }
    }
    catch (Exception fatalEx)
    {
        Console.WriteLine($"💥 안전한 ProcessingLoop 치명적 오류: {fatalEx}");
        LogMessage($"💥 안전 모드 치명적 오류: {fatalEx.Message}");
    }
    finally
    {
        Console.WriteLine("🧹 안전한 ProcessingLoop 정리 시작");
        
        try
        {
            if (!isDisposing && Root?.IsHandleCreated == true && !Root.IsDisposed)
            {
                Root.BeginInvoke(new Action(() => {
                    try
                    {
                        StopProcessing(null, null);
                    }
                    catch (Exception stopEx)
                    {
                        Console.WriteLine($"⚠️ 정리 중 오류: {stopEx.Message}");
                    }
                }));
            }
        }
        catch (Exception cleanupEx)
        {
            Console.WriteLine($"⚠️ 최종 정리 오류: {cleanupEx.Message}");
        }
        
        Console.WriteLine("🏁 안전한 ProcessingLoop 완전 종료");
    }
}
        private void StopProcessing(object sender, EventArgs e)
        {
            try
            {
                lock (isRunningLock)
                {
                    if (!isRunning)
                        return;
                    
                    isRunning = false;
                }
                
                LogMessage("🛑 MosaicProcessor 중지 중...");
                
                try
                {
                    overlay?.Hide();
                }
                catch (Exception ex)
                {
                    LogMessage($"❌ 오버레이 숨기기 오류: {ex.Message}");
                }
                
                if (processThread != null && processThread.IsAlive)
                {
                    processThread.Join(3000);
                }
                
                if (!isDisposing && Root?.IsHandleCreated == true && !Root.IsDisposed)
                {
                    if (Root.InvokeRequired)
                    {
                        Root.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (!isDisposing && !Root.IsDisposed)
                                {
                                    statusLabel.Text = "⭕ MosaicProcessor 대기 중";
                                    statusLabel.ForeColor = Color.Red;
                                    startButton.Enabled = true;
                                    stopButton.Enabled = false;
                                    featureLevelCombo.Enabled = true;
                                }
                            }
                            catch { }
                        }));
                    }
                }
                
                LogMessage("✅ MosaicProcessor 중지됨");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ StopProcessing 오류: {ex.Message}");
            }
        }

        // 7. 메인 처리 루프 (MosaicProcessor 최적화 버전)
        private void ProcessingLoop()
        {
            LogMessage("🚀 MosaicProcessor ProcessingLoop 시작");
            int frameCount = 0;
            DateTime lastLogTime = DateTime.Now;
            var frameTimes = new List<double>();
            var detectionTimes = new List<double>();
            
            int frameskip = Math.Max(1, 60 / targetFPS);
            
            try
            {
                LogMessage($"🚀 MosaicProcessor 처리 루프 진입 - 목표 FPS: {targetFPS}, 프레임 스킵: {frameskip}");
                LogMessage($"🚀 초기 설정: 감지={enableDetection}, 검열={enableCensoring}");
                LogMessage($"🚀 프로세서 상태: {mosaicProcessor?.GetType().Name}, 모델 로드={mosaicProcessor?.IsModelLoaded()}");
                
                while (true)
                {
                    var frameStartTime = DateTime.Now;
                    
                    try
                    {
                        bool shouldRun;
                        lock (isRunningLock)
                        {
                            shouldRun = isRunning && !isDisposing;
                        }
                        
                        if (!shouldRun)
                        {
                            LogMessage("🛑 MosaicProcessor ProcessingLoop 정상 종료");
                            break;
                        }
                        
                        frameCount++;
                        
                        Mat capturedFrame = null;
                        Mat processedFrame = null;
                        
                        try
                        {
                            if (frameCount % frameskip == 0)
                            {
                                try
                                {
                                    if (capturer != null)
                                    {
                                        capturedFrame = capturer.GetFrame();
                                        
                                        if (capturedFrame != null && !capturedFrame.Empty())
                                        {
                                            processedFrame = capturedFrame.Clone();
                                            
                                            if (frameCount <= 5 || frameCount % 60 == 0)
                                            {
                                                LogMessage($"📸 프레임 #{frameCount}: 캡처 성공 {capturedFrame.Width}x{capturedFrame.Height}");
                                            }
                                        }
                                    }
                                }
                                catch (Exception captureEx)
                                {
                                    LogMessage($"❌ 캡처 오류: {captureEx.Message}");
                                    Thread.Sleep(100);
                                    continue;
                                }
                                
                                if (processedFrame == null || processedFrame.Empty())
                                {
                                    Thread.Sleep(50);
                                    continue;
                                }
                                
                                // STEP 2: 객체 감지
                                List<MosaicCensorSystem.Detection.Detection> detections = null;
                                if (enableDetection && mosaicProcessor != null && mosaicProcessor.IsModelLoaded())
                                {
                                    var detectionStart = DateTime.Now;
                                    try
                                    {
                                        // MosaicProcessor의 DetectObjects 호출
                                        if (frameCount <= 10 || frameCount % 60 == 0)
                                        {
                                            LogMessage($"🔍 프레임 #{frameCount}: MosaicProcessor.DetectObjects 호출");
                                        }
                                        
                                        detections = mosaicProcessor.DetectObjects(capturedFrame);
                                        
                                        var detectionTime = (DateTime.Now - detectionStart).TotalMilliseconds;
                                        detectionTimes.Add(detectionTime);
                                        if (detectionTimes.Count > 50)
                                            detectionTimes.RemoveRange(0, 25);
                                        
                                        if (frameCount <= 10 || frameCount % 60 == 0 || detections.Count > 0)
                                        {
                                            LogMessage($"✅ 프레임 #{frameCount}: MosaicProcessor 감지 완료 - {detections?.Count ?? 0}개 객체, {detectionTime:F1}ms");
                                        }
                                        
                                        // 감지 결과 상세 로그
                                        if (detections != null && detections.Count > 0)
                                        {
                                            if (frameCount <= 5 || frameCount % 30 == 0)
                                            {
                                                LogMessage($"🎯 감지된 객체들:");
                                                for (int i = 0; i < Math.Min(detections.Count, 3); i++)
                                                {
                                                    var det = detections[i];
                                                    LogMessage($"  - {det.ClassName} (신뢰도: {det.Confidence:F3}, 크기: {det.Width}x{det.Height})");
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception detectEx)
                                    {
                                        LogMessage($"❌ MosaicProcessor 감지 오류: {detectEx.Message}");
                                    }
                                }
                                
                                // STEP 3: 검열 효과 적용
                                if (enableCensoring && detections != null && detections.Count > 0)
                                {
                                    try
                                    {
                                        int appliedCount = 0;
                                        
                                        foreach (var detection in detections.Take(5)) // 최대 5개 처리
                                        {
                                            if (mosaicProcessor != null)
                                            {
                                                mosaicProcessor.ApplySingleCensorOptimized(processedFrame, detection);
                                                appliedCount++;
                                            }
                                        }
                                        
                                        if (appliedCount > 0)
                                        {
                                            lock (statsLock)
                                            {
                                                stats["censor_applied"] = (int)stats["censor_applied"] + appliedCount;
                                                stats["objects_detected"] = (int)stats["objects_detected"] + detections.Count;
                                            }
                                            
                                            if (frameCount <= 10 || frameCount % 60 == 0)
                                            {
                                                LogMessage($"🎨 프레임 #{frameCount}: 검열 완료 - {appliedCount}개 적용");
                                            }
                                        }
                                    }
                                    catch (Exception censorEx)
                                    {
                                        LogMessage($"❌ 검열 오류: {censorEx.Message}");
                                    }
                                }
                                
                                // STEP 4: 오버레이 업데이트
                                try
                                {
                                    if (overlay != null && overlay.IsWindowVisible())
                                    {
                                        overlay.UpdateFrame(processedFrame);
                                    }
                                }
                                catch (Exception overlayEx)
                                {
                                    LogMessage($"❌ 오버레이 오류: {overlayEx.Message}");
                                }
                                
                                // 통계 업데이트
                                lock (statsLock)
                                {
                                    stats["frames_processed"] = frameCount;
                                }
                            }
                            
                            // 프레임 시간 기록
                            var frameTime = (DateTime.Now - frameStartTime).TotalMilliseconds;
                            frameTimes.Add(frameTime);
                            if (frameTimes.Count > 100)
                                frameTimes.RemoveRange(0, 50);
                            
                            // 성능 로그 출력 (20초마다)
                            var now = DateTime.Now;
                            if ((now - lastLogTime).TotalSeconds >= 20)
                            {
                                lastLogTime = now;
                                
                                lock (statsLock)
                                {
                                    if (stats["start_time"] != null)
                                    {
                                        var totalSeconds = (now - (DateTime)stats["start_time"]).TotalSeconds;
                                        var actualFps = frameCount / totalSeconds;
                                        var avgFrameTime = frameTimes.Count > 0 ? frameTimes.Average() : 0;
                                        var avgDetectionTime = detectionTimes.Count > 0 ? detectionTimes.Average() : 0;
                                        
                                        stats["fps"] = actualFps;
                                        stats["detection_time"] = avgDetectionTime;
                                        
                                        // MosaicProcessor 성능 통계
                                        var perfStats = mosaicProcessor?.GetPerformanceStats();
                                        
                                        LogMessage($"🚀 MosaicProcessor 성능: {actualFps:F1}fps (목표:{targetFPS}), 프레임:{avgFrameTime:F1}ms, 감지:{avgDetectionTime:F1}ms");
                                        LogMessage($"📊 통계: 프레임:{frameCount}, 감지:{stats["objects_detected"]}, 검열:{stats["censor_applied"]}");
                                        if (perfStats != null)
                                        {
                                            LogMessage($"🎯 캐시: 히트={perfStats.CacheHits}, 미스={perfStats.CacheMisses}, 트래킹={perfStats.TrackedObjects}");
                                        }
                                    }
                                }
                            }
                            
                            // 오버레이 상태 체크
                            try
                            {
                                if (overlay != null && !overlay.IsWindowVisible())
                                {
                                    LogMessage("🛑 오버레이 창 닫힘 - 루프 종료");
                                    lock (isRunningLock)
                                    {
                                        isRunning = false;
                                    }
                                    break;
                                }
                            }
                            catch { }
                            
                            // 목표 FPS에 맞춘 대기
                            int targetDelay = 1000 / targetFPS;
                            int actualDelay = Math.Max(1, targetDelay - (int)frameTime);
                            Thread.Sleep(actualDelay);
                        }
                        catch (Exception frameEx)
                        {
                            LogMessage($"❌ 프레임 처리 오류: {frameEx.Message}");
                            Thread.Sleep(100);
                        }
                        finally
                        {
                            try
                            {
                                capturedFrame?.Dispose();
                                processedFrame?.Dispose();
                            }
                            catch { }
                        }
                        
                        // 강제 GC (300프레임마다)
                        if (frameCount % 300 == 0)
                        {
                            try
                            {
                                GC.Collect();
                                GC.WaitForPendingFinalizers();
                                GC.Collect();
                            }
                            catch { }
                        }
                    }
                    catch (Exception loopEx)
                    {
                        LogMessage($"❌ 루프 오류 (복구됨): {loopEx.Message}");
                        Thread.Sleep(500);
                    }
                }
            }
            catch (Exception fatalEx)
            {
                LogMessage($"💥 MosaicProcessor ProcessingLoop 치명적 오류: {fatalEx.Message}");
                
                try
                {
                    File.AppendAllText("mosaic_processor_error.log", 
                        $"{DateTime.Now}: MOSAIC PROCESSOR FATAL - {fatalEx}\n================\n");
                }
                catch { }
            }
            finally
            {
                LogMessage("🧹 MosaicProcessor ProcessingLoop 정리");
                
                try
                {
                    if (!isDisposing && Root?.IsHandleCreated == true && !Root.IsDisposed)
                    {
                        Root.BeginInvoke(new Action(() => StopProcessing(null, null)));
                    }
                }
                catch { }
                
                LogMessage("🏁 MosaicProcessor ProcessingLoop 완전 종료");
            }
        }

        // 8. 유틸리티 메서드들
        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var fullMessage = $"[{timestamp}] {message}";
            
            Console.WriteLine(fullMessage);
            
            try
            {
                if (!isDisposing && Root?.IsHandleCreated == true && !Root.IsDisposed)
                {
                    if (Root.InvokeRequired)
                    {
                        Root.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (!isDisposing && logTextBox != null && !logTextBox.IsDisposed)
                                {
                                    logTextBox.AppendText(fullMessage + Environment.NewLine);
                                    
                                    if (logTextBox.Lines.Length > 30)
                                    {
                                        var lines = logTextBox.Lines.Skip(15).ToArray();
                                        logTextBox.Lines = lines;
                                    }
                                    
                                    logTextBox.SelectionStart = logTextBox.Text.Length;
                                    logTextBox.ScrollToCaret();
                                }
                            }
                            catch { }
                        }));
                    }
                }
            }
            catch { }
        }

        private void Cleanup()
        {
            Console.WriteLine("🧹 MosaicProcessor 기반 리소스 정리 중...");
            
            try
            {
                isDisposing = true;
                
                lock (isRunningLock)
                {
                    isRunning = false;
                }
                
                if (processThread != null && processThread.IsAlive)
                {
                    processThread.Join(5000);
                }
                
                try
                {
                    overlay?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 오버레이 정리 오류: {ex.Message}");
                }
                
                try
                {
                    capturer?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 캡처러 정리 오류: {ex.Message}");
                }
                
                try
                {
                    mosaicProcessor?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ MosaicProcessor 정리 오류: {ex.Message}");
                }
                
                try
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
                catch { }
                
                Console.WriteLine("✅ MosaicProcessor 기반 리소스 정리 완료");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 정리 중 오류: {ex.Message}");
            }
        }
    }
}