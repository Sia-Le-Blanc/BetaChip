using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MosaicCensorSystem.Helpers;

namespace MosaicCensorSystem.UI
{
    public class GpuSetupForm : Form
    {
        private readonly GpuDetector.DetectionResult detection;

        public GpuSetupForm(GpuDetector.DetectionResult result)
        {
            detection = result;
            InitializeForm();
            CreateContent();
        }

        private void InitializeForm()
        {
            Text = "GPU 설정 안내";
            Size = new Size(500, 400);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
        }

        private void CreateContent()
        {
            int y = 20;

            // 제목
            var titleLabel = new Label
            {
                Text = "🖥️ GPU 환경 감지 결과",
                Font = new Font("맑은 고딕", 14, FontStyle.Bold),
                Location = new Point(20, y),
                AutoSize = true
            };
            Controls.Add(titleLabel);
            y += 40;

            // 감지된 GPU 목록
            var gpuListLabel = new Label
            {
                Text = "감지된 GPU:",
                Font = new Font("맑은 고딕", 10, FontStyle.Bold),
                Location = new Point(20, y),
                AutoSize = true
            };
            Controls.Add(gpuListLabel);
            y += 25;

            if (detection.DetectedGpus.Count == 0)
            {
                var noGpuLabel = new Label
                {
                    Text = "  ❌ GPU를 찾을 수 없습니다",
                    Location = new Point(20, y),
                    AutoSize = true,
                    ForeColor = Color.Red
                };
                Controls.Add(noGpuLabel);
                y += 25;
            }
            else
            {
                foreach (var gpu in detection.DetectedGpus)
                {
                    string icon = gpu.Vendor switch
                    {
                        GpuDetector.GpuVendor.Nvidia => "🟢",
                        GpuDetector.GpuVendor.Amd => "🔴",
                        GpuDetector.GpuVendor.Intel => "🔵",
                        _ => "⚪"
                    };
                    var gpuLabel = new Label
                    {
                        Text = $"  {icon} {gpu.Name}",
                        Location = new Point(20, y),
                        AutoSize = true
                    };
                    Controls.Add(gpuLabel);
                    y += 22;
                }
            }
            y += 10;

            // 실행 모드
            string modeText = detection.Recommended switch
            {
                GpuDetector.RecommendedMode.CUDA => "✅ CUDA (NVIDIA GPU 가속)",
                GpuDetector.RecommendedMode.DirectML => "✅ DirectML (Windows GPU 가속)",
                _ => "⚠️ CPU (소프트웨어 처리)"
            };
            Color modeColor = detection.Recommended == GpuDetector.RecommendedMode.CPU ? Color.OrangeRed : Color.Green;

            var modeLabel = new Label
            {
                Text = $"실행 모드: {modeText}",
                Font = new Font("맑은 고딕", 10, FontStyle.Bold),
                Location = new Point(20, y),
                AutoSize = true,
                ForeColor = modeColor
            };
            Controls.Add(modeLabel);
            y += 35;

            // 문제가 있는 경우 안내
            if (!string.IsNullOrEmpty(detection.FailureReason))
            {
                var reasonLabel = new Label
                {
                    Text = $"⚠️ {detection.FailureReason}",
                    Location = new Point(20, y),
                    Size = new Size(440, 40),
                    ForeColor = Color.DarkOrange
                };
                Controls.Add(reasonLabel);
                y += 45;

                if (!string.IsNullOrEmpty(detection.DriverDownloadUrl))
                {
                    var downloadButton = new Button
                    {
                        Text = "🔗 드라이버 다운로드 페이지 열기",
                        Location = new Point(20, y),
                        Size = new Size(200, 30),
                        BackColor = Color.RoyalBlue,
                        ForeColor = Color.White
                    };
                    downloadButton.Click += (s, e) =>
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = detection.DriverDownloadUrl,
                            UseShellExecute = true
                        });
                    };
                    Controls.Add(downloadButton);
                    y += 40;
                }
            }

            // 확인 버튼
            var okButton = new Button
            {
                Text = "확인",
                Location = new Point(200, ClientSize.Height - 50),
                Size = new Size(80, 30),
                DialogResult = DialogResult.OK
            };
            Controls.Add(okButton);
            AcceptButton = okButton;
        }
    }
}