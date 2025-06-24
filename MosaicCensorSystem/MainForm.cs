using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using OpenCvSharp;
using MosaicCensorSystem.Capture;
using MosaicCensorSystem.Detection;
using MosaicCensorSystem.Overlay;
using MosaicCensorSystem.UI;

namespace MosaicCensorSystem
{
    /// <summary>
    /// 풀스크린 + 캡처 방지 실시간 화면 검열 애플리케이션
    /// </summary>
    public class MosaicApp
    {
        // 메인 윈도우
        public Form Root { get; private set; }
        
        // 컴포넌트
        private ScreenCapturer capturer;
        private MosaicProcessor processor;
        private FullscreenOverlay overlay;
        
        // UI 컴포넌트
        private ScrollablePanel scrollableContainer;
        private Label statusLabel;
        private Dictionary<string, CheckBox> targetCheckBoxes = new Dictionary<string, CheckBox>();
        private TrackBar strengthSlider;
        private TrackBar confidenceSlider;
        private TrackBar fpsSlider;
        private Label strengthLabel;
        private Label confidenceLabel;
        private Label fpsLabel;
        private TextBox logTextBox;
        private Dictionary<string, Label> statsLabels = new Dictionary<string, Label>();
        private CheckBox debugCheckBox;
        private CheckBox showDebugInfoCheckBox;
        private Button startButton;
        private Button stopButton;
        
        // 상태 변수
        private bool isRunning = false;
        private Thread processThread;
        private bool debugMode = false;
        
        // 통계 변수
        private Dictionary<string, object> stats = new Dictionary<string, object>
        {
            ["frames_processed"] = 0,
            ["objects_detected"] = 0,
            ["mosaic_applied"] = 0,
            ["start_time"] = null
        };
        
        // 드래그 관련
        private bool isDragging = false;
        private System.Drawing.Point dragStartPoint;

        public MosaicApp()
        {
            // 메인 윈도우 생성
            Root = new Form
            {
                Text = "실시간 화면 검열 시스템 v3.0 (풀스크린 + 캡처 방지)",
                Size = new System.Drawing.Size(500, 600),
                MinimumSize = new System.Drawing.Size(450, 400),
                StartPosition = FormStartPosition.CenterScreen
            };
            
            // 컴포넌트 초기화
            capturer = new ScreenCapturer(Config.GetSection("capture"));
            processor = new MosaicProcessor(null, Config.GetSection("mosaic"));
            overlay = new FullscreenOverlay(Config.GetSection("overlay"));
            
            // GUI 생성
            CreateGui();
            
            // 디버그 디렉토리 생성
            if (debugMode)
            {
                Directory.CreateDirectory("debug_detection");
            }
        }

        private void CreateGui()
        {
            // 제목 (드래그 가능) - 고정 영역
            var titleLabel = new Label
            {
                Text = "🛡️ 풀스크린 화면 검열 시스템",
                Font = new Font("Arial", 14, FontStyle.Bold),
                BackColor = Color.LightBlue,
                BorderStyle = BorderStyle.Fixed3D,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 40,
                Dock = DockStyle.Top
            };
            
            // 제목 라벨에 드래그 기능 바인딩
            SetupWindowDragging(titleLabel);
            
            // 스크롤 안내 - 고정 영역
            var scrollInfo = new Label
            {
                Text = "📜 마우스 휠로 스크롤하여 모든 설정을 확인하세요",
                Font = new Font("Arial", 9),
                ForeColor = Color.Blue,
                BackColor = Color.LightYellow,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 25,
                Dock = DockStyle.Top
            };
            
            // 스크롤 가능한 메인 영역
            scrollableContainer = new ScrollablePanel
            {
                Dock = DockStyle.Fill
            };
            
            // 컨트롤 추가 (아래서부터)
            Root.Controls.Add(scrollableContainer);
            Root.Controls.Add(scrollInfo);
            Root.Controls.Add(titleLabel);
            
            // 실제 내용을 스크롤 가능한 프레임에 추가
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
            
            // 드래그 안내
            var dragInfo = new Label
            {
                Text = "💡 파란색 제목을 드래그해서 창을 이동하세요",
                Font = new Font("Arial", 9),
                ForeColor = Color.Gray,
                Location = new System.Drawing.Point(10, y),
                AutoSize = true
            };
            parent.Controls.Add(dragInfo);
            y += 30;
            
            // 상태 표시
            statusLabel = new Label
            {
                Text = "⭕ 대기 중",
                Font = new Font("Arial", 12),
                ForeColor = Color.Red,
                Location = new System.Drawing.Point(10, y),
                AutoSize = true
            };
            parent.Controls.Add(statusLabel);
            y += 40;
            
            // 개선 안내
            var infoGroup = new GroupBox
            {
                Text = "🚀 최종 완성 버전!",
                Location = new System.Drawing.Point(10, y),
                Size = new System.Drawing.Size(460, 130)
            };
            
            var infoText = @"🛡️ 화면 캡처에서 완전 제외로 피드백 루프 방지
🖥️ 전체 화면 매끄러운 모자이크 표시
🖱️ 클릭 투과로 바탕화면 상호작용 가능
📌 Windows Hook으로 창 활성화 즉시 차단
⚡ 플리커링 없는 안정적인 검열 시스템
✅ 실시간 객체 감지 및 모자이크 적용";
            
            var infoLabel = new Label
            {
                Text = infoText,
                ForeColor = Color.Green,
                Location = new System.Drawing.Point(10, 20),
                Size = new System.Drawing.Size(440, 100)
            };
            infoGroup.Controls.Add(infoLabel);
            parent.Controls.Add(infoGroup);
            y += 140;
            
            // 중요 안내
            var warningGroup = new GroupBox
            {
                Text = "⚠️ 중요 안내",
                Location = new System.Drawing.Point(10, y),
                Size = new System.Drawing.Size(460, 80)
            };
            
            var warningText = @"풀스크린 모드에서는 모든 화면이 덮어집니다.
ESC 키를 눌러 종료하거나, Ctrl+Alt+Del로 강제 종료하세요.
F1 키로 디버그 정보를 켜고 끌 수 있습니다.";
            
            var warningLabel = new Label
            {
                Text = warningText,
                ForeColor = Color.Red,
                Location = new System.Drawing.Point(10, 20),
                Size = new System.Drawing.Size(440, 50)
            };
            warningGroup.Controls.Add(warningLabel);
            parent.Controls.Add(warningGroup);
            y += 90;
            
            // 모자이크 대상 선택
            var targetsGroup = new GroupBox
            {
                Text = "🎯 모자이크 대상 선택",
                Location = new System.Drawing.Point(10, y),
                Size = new System.Drawing.Size(460, 180)
            };
            
            var availableTargets = new[]
            {
                "얼굴", "가슴", "겨드랑이", "보지", "발", "몸 전체",
                "자지", "팬티", "눈", "손", "교미", "신발",
                "가슴_옷", "보지_옷", "여성"
            };
            
            var defaultTargets = Config.Get<List<string>>("mosaic", "default_targets", new List<string>());
            
            // 2열로 배치
            for (int i = 0; i < availableTargets.Length; i++)
            {
                var target = availableTargets[i];
                var row = i / 2;
                var col = i % 2;
                
                var checkbox = new CheckBox
                {
                    Text = target,
                    Checked = defaultTargets.Contains(target),
                    Location = new System.Drawing.Point(10 + col * 220, 25 + row * 25),
                    AutoSize = true
                };
                
                targetCheckBoxes[target] = checkbox;
                targetsGroup.Controls.Add(checkbox);
            }
            parent.Controls.Add(targetsGroup);
            y += 190;
            
            // 모자이크 설정
            var settingsGroup = new GroupBox
            {
                Text = "⚙️ 모자이크 설정",
                Location = new System.Drawing.Point(10, y),
                Size = new System.Drawing.Size(460, 150)
            };
            
            // 모자이크 강도
            var strengthTextLabel = new Label
            {
                Text = "모자이크 강도:",
                Location = new System.Drawing.Point(10, 25),
                AutoSize = true
            };
            settingsGroup.Controls.Add(strengthTextLabel);
            
            strengthSlider = new TrackBar
            {
                Minimum = 5,
                Maximum = 50,
                Value = Config.Get<int>("mosaic", "default_strength", 15),
                TickFrequency = 5,
                Location = new System.Drawing.Point(120, 20),
                Size = new System.Drawing.Size(280, 45)
            };
            strengthSlider.ValueChanged += UpdateStrengthLabel;
            settingsGroup.Controls.Add(strengthSlider);
            
            strengthLabel = new Label
            {
                Text = strengthSlider.Value.ToString(),
                Location = new System.Drawing.Point(410, 25),
                AutoSize = true
            };
            settingsGroup.Controls.Add(strengthLabel);
            
            // 신뢰도 임계값
            var confidenceTextLabel = new Label
            {
                Text = "감지 신뢰도:",
                Location = new System.Drawing.Point(10, 65),
                AutoSize = true
            };
            settingsGroup.Controls.Add(confidenceTextLabel);
            
            confidenceSlider = new TrackBar
            {
                Minimum = 1,
                Maximum = 9,
                Value = (int)(Config.Get<double>("mosaic", "conf_threshold", 0.1) * 10),
                TickFrequency = 1,
                Location = new System.Drawing.Point(120, 60),
                Size = new System.Drawing.Size(280, 45)
            };
            confidenceSlider.ValueChanged += UpdateConfidenceLabel;
            settingsGroup.Controls.Add(confidenceSlider);
            
            confidenceLabel = new Label
            {
                Text = "0.1",
                Location = new System.Drawing.Point(410, 65),
                AutoSize = true
            };
            settingsGroup.Controls.Add(confidenceLabel);
            
            // FPS 제한
            var fpsTextLabel = new Label
            {
                Text = "FPS 제한:",
                Location = new System.Drawing.Point(10, 105),
                AutoSize = true
            };
            settingsGroup.Controls.Add(fpsTextLabel);
            
            fpsSlider = new TrackBar
            {
                Minimum = 15,
                Maximum = 60,
                Value = 30,
                TickFrequency = 5,
                Location = new System.Drawing.Point(120, 100),
                Size = new System.Drawing.Size(280, 45)
            };
            fpsSlider.ValueChanged += UpdateFpsLabel;
            settingsGroup.Controls.Add(fpsSlider);
            
            fpsLabel = new Label
            {
                Text = "30",
                Location = new System.Drawing.Point(410, 105),
                AutoSize = true
            };
            settingsGroup.Controls.Add(fpsLabel);
            
            parent.Controls.Add(settingsGroup);
            y += 160;
            
            // 컨트롤 버튼
            var controlPanel = new Panel
            {
                BackColor = Color.LightGray,
                BorderStyle = BorderStyle.Fixed3D,
                Location = new System.Drawing.Point(10, y),
                Size = new System.Drawing.Size(460, 100)
            };
            
            var buttonLabel = new Label
            {
                Text = "🎮 메인 컨트롤",
                Font = new Font("Arial", 12, FontStyle.Bold),
                BackColor = Color.LightGray,
                Location = new System.Drawing.Point(10, 10),
                Size = new System.Drawing.Size(440, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };
            controlPanel.Controls.Add(buttonLabel);
            
            startButton = new Button
            {
                Text = "🚀 풀스크린 시작",
                BackColor = Color.Green,
                ForeColor = Color.White,
                Font = new Font("Arial", 12, FontStyle.Bold),
                Size = new System.Drawing.Size(180, 50),
                Location = new System.Drawing.Point(50, 40)
            };
            startButton.Click += StartCensoring;
            controlPanel.Controls.Add(startButton);
            
            stopButton = new Button
            {
                Text = "🛑 검열 중지",
                BackColor = Color.Red,
                ForeColor = Color.White,
                Font = new Font("Arial", 12, FontStyle.Bold),
                Size = new System.Drawing.Size(180, 50),
                Location = new System.Drawing.Point(230, 40),
                Enabled = false
            };
            stopButton.Click += StopCensoring;
            controlPanel.Controls.Add(stopButton);
            
            parent.Controls.Add(controlPanel);
            y += 110;
            
            // 통계 표시
            var statsGroup = new GroupBox
            {
                Text = "📊 실시간 통계",
                Location = new System.Drawing.Point(10, y),
                Size = new System.Drawing.Size(460, 100)
            };
            
            var statsItems = new[]
            {
                ("처리된 프레임", "frames_processed"),
                ("감지된 객체", "objects_detected"),
                ("모자이크 적용", "mosaic_applied"),
                ("실행 시간", "runtime")
            };
            
            for (int i = 0; i < statsItems.Length; i++)
            {
                var (name, key) = statsItems[i];
                
                var nameLabel = new Label
                {
                    Text = $"{name}:",
                    Location = new System.Drawing.Point(10 + (i % 2) * 230, 25 + (i / 2) * 30),
                    AutoSize = true
                };
                statsGroup.Controls.Add(nameLabel);
                
                var valueLabel = new Label
                {
                    Text = "0",
                    Font = new Font("Arial", 10, FontStyle.Bold),
                    Location = new System.Drawing.Point(120 + (i % 2) * 230, 25 + (i / 2) * 30),
                    AutoSize = true
                };
                statsLabels[key] = valueLabel;
                statsGroup.Controls.Add(valueLabel);
            }
            parent.Controls.Add(statsGroup);
            y += 110;
            
            // 로그 표시
            var logGroup = new GroupBox
            {
                Text = "📝 실시간 로그",
                Location = new System.Drawing.Point(10, y),
                Size = new System.Drawing.Size(460, 100)
            };
            
            logTextBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Location = new System.Drawing.Point(10, 20),
                Size = new System.Drawing.Size(440, 70)
            };
            logGroup.Controls.Add(logTextBox);
            parent.Controls.Add(logGroup);
            y += 110;
            
            // 디버그 설정
            var debugGroup = new GroupBox
            {
                Text = "🐛 디버그 옵션",
                Location = new System.Drawing.Point(10, y),
                Size = new System.Drawing.Size(460, 60)
            };
            
            debugCheckBox = new CheckBox
            {
                Text = "🐛 디버그 모드",
                Location = new System.Drawing.Point(10, 25),
                AutoSize = true
            };
            debugGroup.Controls.Add(debugCheckBox);
            
            showDebugInfoCheckBox = new CheckBox
            {
                Text = "🔍 풀스크린 디버그 정보",
                Location = new System.Drawing.Point(230, 25),
                AutoSize = true
            };
            debugGroup.Controls.Add(showDebugInfoCheckBox);
            parent.Controls.Add(debugGroup);
            y += 70;
            
            // 스크롤 테스트 확인
            var testGroup = new GroupBox
            {
                Text = "✅ 스크롤 테스트",
                Location = new System.Drawing.Point(10, y),
                Size = new System.Drawing.Size(460, 60)
            };
            
            var testLabel = new Label
            {
                Text = "여기까지 스크롤이 되었다면 성공! 위로 올라가서 버튼을 클릭하세요.",
                ForeColor = Color.Green,
                Font = new Font("Arial", 10, FontStyle.Bold),
                Location = new System.Drawing.Point(10, 25),
                Size = new System.Drawing.Size(440, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };
            testGroup.Controls.Add(testLabel);
            parent.Controls.Add(testGroup);
        }

        private void UpdateStrengthLabel(object sender, EventArgs e)
        {
            strengthLabel.Text = strengthSlider.Value.ToString();
        }

        private void UpdateConfidenceLabel(object sender, EventArgs e)
        {
            confidenceLabel.Text = $"{confidenceSlider.Value / 10.0:F1}";
        }

        private void UpdateFpsLabel(object sender, EventArgs e)
        {
            fpsLabel.Text = fpsSlider.Value.ToString();
        }

        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var fullMessage = $"[{timestamp}] {message}";
            
            // GUI 로그
            if (Root.InvokeRequired)
            {
                Root.Invoke(new Action(() =>
                {
                    logTextBox.AppendText(fullMessage + Environment.NewLine);
                    logTextBox.SelectionStart = logTextBox.Text.Length;
                    logTextBox.ScrollToCaret();
                }));
            }
            else
            {
                logTextBox.AppendText(fullMessage + Environment.NewLine);
                logTextBox.SelectionStart = logTextBox.Text.Length;
                logTextBox.ScrollToCaret();
            }
            
            // 콘솔 로그
            Console.WriteLine(fullMessage);
        }

        private void UpdateStats()
        {
            if (stats["start_time"] != null)
            {
                var runtime = (int)(DateTime.Now - (DateTime)stats["start_time"]).TotalSeconds;
                var minutes = runtime / 60;
                var seconds = runtime % 60;
                
                if (Root.InvokeRequired)
                {
                    Root.Invoke(new Action(() =>
                    {
                        statsLabels["runtime"].Text = $"{minutes:D2}:{seconds:D2}";
                        statsLabels["frames_processed"].Text = stats["frames_processed"].ToString();
                        statsLabels["objects_detected"].Text = stats["objects_detected"].ToString();
                        statsLabels["mosaic_applied"].Text = stats["mosaic_applied"].ToString();
                    }));
                }
            }
        }

        private void StartCensoring(object sender, EventArgs e)
        {
            if (isRunning)
                return;
            
            // 선택된 타겟 확인
            var selectedTargets = new List<string>();
            foreach (var kvp in targetCheckBoxes)
            {
                if (kvp.Value.Checked)
                    selectedTargets.Add(kvp.Key);
            }
            
            if (selectedTargets.Count == 0)
            {
                MessageBox.Show("최소 하나의 모자이크 대상을 선택해주세요!", "경고", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            // 최종 확인
            var result = MessageBox.Show(
                "화면 검열 시스템을 시작하시겠습니까?\n\n" +
                "• 전체 화면에 모자이크가 적용됩니다\n" +
                "• 바탕화면을 자유롭게 사용할 수 있습니다\n" +
                "• ESC 키로 언제든 종료할 수 있습니다\n\n" +
                "계속하시겠습니까?",
                "화면 검열 시작 확인",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            
            if (result != DialogResult.Yes)
                return;
            
            // 설정 적용
            processor.SetTargets(selectedTargets);
            processor.SetStrength(strengthSlider.Value);
            processor.ConfThreshold = confidenceSlider.Value / 10.0f;
            debugMode = debugCheckBox.Checked;
            
            // 오버레이 설정
            overlay.ShowDebugInfo = showDebugInfoCheckBox.Checked;
            overlay.SetFpsLimit(fpsSlider.Value);
            
            // 상태 변경
            isRunning = true;
            stats["start_time"] = DateTime.Now;
            stats["frames_processed"] = 0;
            stats["objects_detected"] = 0;
            stats["mosaic_applied"] = 0;
            
            // GUI 업데이트
            statusLabel.Text = "✅ 풀스크린 검열 중";
            statusLabel.ForeColor = Color.Green;
            startButton.Enabled = false;
            stopButton.Enabled = true;
            
            // 풀스크린 오버레이 표시
            if (!overlay.Show())
            {
                LogMessage("❌ 풀스크린 오버레이 시작 실패");
                StopCensoring(null, null);
                return;
            }
            
            // 처리 스레드 시작
            processThread = new Thread(ProcessingLoop)
            {
                Name = "ProcessingThread",
                IsBackground = true
            };
            processThread.Start();
            
            // 로그 메시지
            LogMessage($"🚀 화면 검열 시작! 대상: {string.Join(", ", selectedTargets)}");
            LogMessage($"⚙️ 설정: 강도={strengthSlider.Value}, 신뢰도={confidenceSlider.Value / 10.0:F2}, FPS={fpsSlider.Value}");
            
            // Windows 기능 테스트
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(3000);
                
                if (overlay.TestCaptureProtection())
                {
                    LogMessage("🛡️ 캡처 방지 기능 활성화됨");
                }
                
                if (overlay.TestClickThrough())
                {
                    LogMessage("🖱️ 클릭 투과 기능 활성화됨");
                    LogMessage("💡 바탕화면을 자유롭게 사용할 수 있습니다");
                }
            });
        }

        private void StopCensoring(object sender, EventArgs e)
        {
            if (!isRunning)
                return;
            
            LogMessage("🛑 화면 검열 중지 중...");
            
            isRunning = false;
            
            // 오버레이 숨기기
            overlay.Hide();
            
            // 스레드 종료 대기
            if (processThread != null && processThread.IsAlive)
            {
                processThread.Join(1000);
            }
            
            // GUI 업데이트
            if (Root.InvokeRequired)
            {
                Root.Invoke(new Action(() =>
                {
                    statusLabel.Text = "⭕ 대기 중";
                    statusLabel.ForeColor = Color.Red;
                    startButton.Enabled = true;
                    stopButton.Enabled = false;
                }));
            }
            else
            {
                statusLabel.Text = "⭕ 대기 중";
                statusLabel.ForeColor = Color.Red;
                startButton.Enabled = true;
                stopButton.Enabled = false;
            }
            
            LogMessage("✅ 화면 검열 중지됨");
        }

        private void ProcessingLoop()
        {
            LogMessage("🔄 전체 화면 모자이크 처리 루프 시작");
            int frameCount = 0;
            
            try
            {
                while (isRunning)
                {
                    // 원본 화면 캡처
                    var originalFrame = capturer.GetFrame();
                    if (originalFrame == null || originalFrame.Empty())
                    {
                        Thread.Sleep(10);
                        continue;
                    }
                    
                    frameCount++;
                    stats["frames_processed"] = frameCount;
                    
                    // 전체 화면 복사
                    var processedFrame = originalFrame.Clone();
                    
                    // 객체 감지는 원본 프레임에서 수행
                    var detections = processor.DetectObjects(originalFrame);
                    
                    // 모자이크 적용
                    if (detections != null && detections.Count > 0)
                    {
                        foreach (var detection in detections)
                        {
                            var className = detection.ClassName;
                            var confidence = detection.Confidence;
                            var bbox = detection.BBox;
                            int x1 = bbox[0], y1 = bbox[1], x2 = bbox[2], y2 = bbox[3];
                            
                            stats["objects_detected"] = (int)stats["objects_detected"] + 1;
                            
                            // 타겟인지 확인
                            if (processor.Targets.Contains(className))
                            {
                                stats["mosaic_applied"] = (int)stats["mosaic_applied"] + 1;
                                
                                // 전체 화면에서 해당 영역에 모자이크 적용
                                using (var region = new Mat(processedFrame, new Rect(x1, y1, x2 - x1, y2 - y1)))
                                {
                                    if (!region.Empty())
                                    {
                                        var mosaicRegion = processor.ApplyMosaic(region, strengthSlider.Value);
                                        mosaicRegion.CopyTo(region);
                                        mosaicRegion.Dispose();
                                        
                                        // 너무 많은 로그 출력 방지
                                        if (frameCount % 30 == 0)
                                        {
                                            LogMessage($"🎯 모자이크 적용: {className}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    // 풀스크린에 전체 처리된 화면 표시
                    overlay.UpdateFrame(processedFrame);
                    
                    // 디버그 이미지 저장
                    if (debugMode && (int)stats["mosaic_applied"] > 0)
                    {
                        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                        
                        var originalPath = $"debug_detection/original_{timestamp}.jpg";
                        var processedPath = $"debug_detection/processed_{timestamp}.jpg";
                        
                        Cv2.ImWrite(originalPath, originalFrame);
                        Cv2.ImWrite(processedPath, processedFrame);
                    }
                    
                    // 통계 업데이트 (매 30프레임마다)
                    if (frameCount % 30 == 0)
                    {
                        UpdateStats();
                    }
                    
                    // 오버레이가 종료되었는지 확인 (ESC 키 등으로)
                    if (!overlay.IsWindowVisible())
                    {
                        isRunning = false;
                        break;
                    }
                    
                    // 리소스 정리
                    originalFrame.Dispose();
                    processedFrame.Dispose();
                    
                    // FPS 제한
                    Thread.Sleep(1000 / fpsSlider.Value);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ 처리 오류: {ex.Message}");
            }
            finally
            {
                // 메인 스레드에서 정리 작업 수행
                if (Root.InvokeRequired)
                {
                    Root.Invoke(new Action(() => StopCensoring(null, null)));
                }
                else
                {
                    StopCensoring(null, null);
                }
            }
        }

        public void Run()
        {
            Console.WriteLine("🛡️ 화면 검열 시스템 시작");
            Console.WriteLine("=" + new string('=', 39));
            
            try
            {
                Root.FormClosed += OnFormClosed;
                Application.Run(Root);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n🛑 오류 발생: {ex.Message}");
            }
            finally
            {
                Cleanup();
            }
        }

        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            if (isRunning)
            {
                StopCensoring(null, null);
            }
            
            Cleanup();
        }

        private void Cleanup()
        {
            Console.WriteLine("🧹 리소스 정리 중...");
            
            if (isRunning)
            {
                isRunning = false;
            }
            
            if (processThread != null && processThread.IsAlive)
            {
                processThread.Join(1000);
            }
            
            overlay?.Dispose();
            capturer?.Dispose();
            processor?.Dispose();
            
            Console.WriteLine("✅ 리소스 정리 완료");
        }
    }
}