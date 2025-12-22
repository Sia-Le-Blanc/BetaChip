#pragma warning disable CS8600
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MosaicCensorSystem.Helpers;

namespace MosaicCensorSystem.UI
{
    public class GpuSetupForm : Form
    {
        private readonly GpuDetector.DetectionResult detection;
        private Panel contentPanel;
        private Button cudnnCopyButton;
        private Label cudnnCopyStatusLabel;

        public GpuSetupForm(GpuDetector.DetectionResult result)
        {
            detection = result;
            InitializeForm();
            CreateContent();
        }

        private void InitializeForm()
        {
            Text = "GPU 가속 설정 가이드";
            Size = new Size(580, 750);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;
        }

        private void CreateContent()
        {
            contentPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(565, 660),
                AutoScroll = true
            };
            Controls.Add(contentPanel);

            int y = 20;

            // 제목
            var titleLabel = new Label
            {
                Text = "🚀 GPU 가속 설정 가이드",
                Font = new Font("맑은 고딕", 16, FontStyle.Bold),
                Location = new Point(20, y),
                AutoSize = true,
                ForeColor = Color.White
            };
            contentPanel.Controls.Add(titleLabel);
            y += 50;

            // GPU 감지 결과
            y = CreateGpuSection(y);
            y += 20;

            // 구분선
            y = CreateSeparator(y);
            y += 20;

            // NVIDIA GPU가 있는 경우에만 CUDA 관련 체크리스트 표시
            bool hasNvidia = detection.DetectedGpus.Any(g => g.Vendor == GpuDetector.GpuVendor.Nvidia);

            if (hasNvidia)
            {
                // 체크리스트 제목
                var checklistTitle = new Label
                {
                    Text = "📋 CUDA 가속 요구사항 체크리스트",
                    Font = new Font("맑은 고딕", 12, FontStyle.Bold),
                    Location = new Point(20, y),
                    AutoSize = true,
                    ForeColor = Color.White
                };
                contentPanel.Controls.Add(checklistTitle);
                y += 35;

                // 각 컴포넌트 상태
                y = CreateComponentRow(y, detection.NvidiaDriver);
                y = CreateComponentRow(y, detection.CudaToolkit);
                y = CreateCuDnnSection(y);
                y += 10;

                // ONNX CUDA 런타임
                y = CreateOnnxStatusRow(y);
            }
            else
            {
                // AMD/Intel GPU인 경우
                var directmlLabel = new Label
                {
                    Text = detection.CanUseDirectML
                        ? "✅ DirectML (Windows GPU 가속) 사용 가능"
                        : "⚠️ DirectML을 사용할 수 없습니다. 최신 Windows 업데이트를 확인하세요.",
                    Font = new Font("맑은 고딕", 10),
                    Location = new Point(20, y),
                    AutoSize = true,
                    ForeColor = detection.CanUseDirectML ? Color.LimeGreen : Color.Orange
                };
                contentPanel.Controls.Add(directmlLabel);
                y += 30;
            }

            // 최종 상태
            y = CreateFinalStatus(y);

            // 확인 버튼
            var okButton = new Button
            {
                Text = "확인",
                Location = new Point(240, 700),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                DialogResult = DialogResult.OK
            };
            Controls.Add(okButton);
            AcceptButton = okButton;
        }

        private int CreateGpuSection(int y)
        {
            var gpuTitle = new Label
            {
                Text = "🖥️ 감지된 GPU",
                Font = new Font("맑은 고딕", 12, FontStyle.Bold),
                Location = new Point(20, y),
                AutoSize = true,
                ForeColor = Color.White
            };
            contentPanel.Controls.Add(gpuTitle);
            y += 30;

            if (detection.DetectedGpus.Count == 0)
            {
                var noGpu = new Label
                {
                    Text = "   ❌ GPU를 찾을 수 없습니다",
                    Font = new Font("맑은 고딕", 10),
                    Location = new Point(20, y),
                    AutoSize = true,
                    ForeColor = Color.Red
                };
                contentPanel.Controls.Add(noGpu);
                y += 25;
            }
            else
            {
                foreach (var gpu in detection.DetectedGpus)
                {
                    string icon = gpu.Vendor switch
                    {
                        GpuDetector.GpuVendor.Nvidia => "🟢 [NVIDIA]",
                        GpuDetector.GpuVendor.Amd => "🔴 [AMD]",
                        GpuDetector.GpuVendor.Intel => "🔵 [Intel]",
                        _ => "⚪ [Unknown]"
                    };

                    var gpuLabel = new Label
                    {
                        Text = $"   {icon} {gpu.Name}",
                        Font = new Font("맑은 고딕", 10),
                        Location = new Point(20, y),
                        AutoSize = true,
                        ForeColor = Color.LightGray
                    };
                    contentPanel.Controls.Add(gpuLabel);
                    y += 25;
                }
            }

            return y;
        }

        private int CreateSeparator(int y)
        {
            var separator = new Label
            {
                BorderStyle = BorderStyle.Fixed3D,
                Location = new Point(20, y),
                Size = new Size(520, 2)
            };
            contentPanel.Controls.Add(separator);
            return y + 5;
        }

        private int CreateComponentRow(int y, GpuDetector.ComponentStatus component)
        {
            // 상태 아이콘 + 이름
            string statusIcon = component.IsInstalled ? "✅" : "❌";
            Color statusColor = component.IsInstalled ? Color.LimeGreen : Color.Red;

            var nameLabel = new Label
            {
                Text = $"{statusIcon} {component.Name}",
                Font = new Font("맑은 고딕", 10, FontStyle.Bold),
                Location = new Point(30, y),
                AutoSize = true,
                ForeColor = statusColor
            };
            contentPanel.Controls.Add(nameLabel);

            // 버전 정보
            string versionText = component.IsInstalled
                ? $"설치됨: {component.InstalledVersion}"
                : $"필요: {component.RequiredVersion}";

            var versionLabel = new Label
            {
                Text = versionText,
                Font = new Font("맑은 고딕", 9),
                Location = new Point(280, y + 2),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            contentPanel.Controls.Add(versionLabel);
            y += 25;

            // 설치되지 않은 경우 가이드 표시
            if (!component.IsInstalled)
            {
                // 설치 가이드
                var guideLabel = new Label
                {
                    Text = component.InstallGuide,
                    Font = new Font("맑은 고딕", 9),
                    Location = new Point(50, y),
                    Size = new Size(480, 80),
                    ForeColor = Color.LightGray
                };
                contentPanel.Controls.Add(guideLabel);
                y += 85;

                // 다운로드 버튼
                var downloadBtn = new Button
                {
                    Text = $"📥 {component.Name} 다운로드",
                    Location = new Point(50, y),
                    Size = new Size(200, 28),
                    BackColor = Color.FromArgb(0, 120, 215),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                downloadBtn.Click += (s, e) => OpenUrl(component.DownloadUrl);
                contentPanel.Controls.Add(downloadBtn);
                y += 35;
            }

            return y + 10;
        }

        private int CreateCuDnnSection(int y)
        {
            var component = detection.CuDnn;

            // 상태 아이콘 + 이름
            string statusIcon = component.IsInstalled ? "✅" : "❌";
            Color statusColor = component.IsInstalled ? Color.LimeGreen : Color.Red;

            var nameLabel = new Label
            {
                Text = $"{statusIcon} {component.Name}",
                Font = new Font("맑은 고딕", 10, FontStyle.Bold),
                Location = new Point(30, y),
                AutoSize = true,
                ForeColor = statusColor
            };
            contentPanel.Controls.Add(nameLabel);

            // 버전 정보
            string versionText = component.IsInstalled
                ? $"설치됨: {component.InstalledVersion}"
                : $"필요: {component.RequiredVersion}";

            var versionLabel = new Label
            {
                Text = versionText,
                Font = new Font("맑은 고딕", 9),
                Location = new Point(280, y + 2),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            contentPanel.Controls.Add(versionLabel);
            y += 25;

            // cuDNN 미설치 시
            if (!component.IsInstalled)
            {
                // CUDA가 설치되어 있는지 확인
                bool cudaInstalled = detection.CudaToolkit.IsInstalled;

                // 다운로드 폴더에서 cuDNN 압축 해제 폴더 찾기
                string cudnnExtractedPath = FindCuDnnExtractedFolder();
                bool cudnnDownloaded = !string.IsNullOrEmpty(cudnnExtractedPath);

                if (!cudaInstalled)
                {
                    // CUDA 먼저 설치 안내
                    var guideLabel = new Label
                    {
                        Text = "⚠️ CUDA Toolkit을 먼저 설치해주세요.",
                        Font = new Font("맑은 고딕", 9),
                        Location = new Point(50, y),
                        AutoSize = true,
                        ForeColor = Color.Orange
                    };
                    contentPanel.Controls.Add(guideLabel);
                    y += 30;
                }
                else if (!cudnnDownloaded)
                {
                    // cuDNN 다운로드 및 압축 해제 안내
                    var guideLabel = new Label
                    {
                        Text = "1. 아래 버튼 클릭 (NVIDIA 로그인 필요)\n" +
                               "2. cudnn-windows-x86_64-8.9.7.29_cuda11-archive.zip 다운로드\n" +
                               "3. 다운로드 폴더에서 압축 해제 (폴더가 생성됨)\n" +
                               "4. 이 창을 닫고 'GPU 설정 확인' 버튼을 다시 클릭",
                        Font = new Font("맑은 고딕", 9),
                        Location = new Point(50, y),
                        Size = new Size(480, 70),
                        ForeColor = Color.LightGray
                    };
                    contentPanel.Controls.Add(guideLabel);
                    y += 75;

                    // 다운로드 버튼
                    var downloadBtn = new Button
                    {
                        Text = "📥 cuDNN 다운로드 (로그인 필요)",
                        Location = new Point(50, y),
                        Size = new Size(220, 28),
                        BackColor = Color.FromArgb(0, 120, 215),
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat,
                        Cursor = Cursors.Hand
                    };
                    downloadBtn.Click += (s, e) => OpenUrl(component.DownloadUrl);
                    contentPanel.Controls.Add(downloadBtn);
                    y += 35;
                }
                else
                {
                    // cuDNN 다운로드 완료 - 자동 복사 버튼 활성화
                    var guideLabel = new Label
                    {
                        Text = $"✅ cuDNN 파일 발견: {Path.GetFileName(cudnnExtractedPath)}\n" +
                               "아래 버튼을 클릭하면 자동으로 CUDA 폴더에 복사됩니다.",
                        Font = new Font("맑은 고딕", 9),
                        Location = new Point(50, y),
                        Size = new Size(480, 40),
                        ForeColor = Color.LimeGreen
                    };
                    contentPanel.Controls.Add(guideLabel);
                    y += 45;

                    // 자동 복사 버튼
                    cudnnCopyButton = new Button
                    {
                        Text = "📁 cuDNN 자동 복사 (CUDA 폴더로)",
                        Location = new Point(50, y),
                        Size = new Size(250, 32),
                        BackColor = Color.FromArgb(0, 150, 0),
                        ForeColor = Color.White,
                        FlatStyle = FlatStyle.Flat,
                        Cursor = Cursors.Hand,
                        Font = new Font("맑은 고딕", 9, FontStyle.Bold)
                    };
                    cudnnCopyButton.Click += (s, e) => CopyCuDnnFiles(cudnnExtractedPath);
                    contentPanel.Controls.Add(cudnnCopyButton);

                    // 복사 상태 라벨
                    cudnnCopyStatusLabel = new Label
                    {
                        Text = "",
                        Font = new Font("맑은 고딕", 9),
                        Location = new Point(310, y + 8),
                        AutoSize = true,
                        ForeColor = Color.White
                    };
                    contentPanel.Controls.Add(cudnnCopyStatusLabel);
                    y += 40;
                }
            }

            return y + 10;
        }

        private string? FindCuDnnExtractedFolder()
        {
            try
            {
                string downloadsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads"
                );

                if (!Directory.Exists(downloadsPath))
                    return null;

                // cudnn으로 시작하는 폴더 찾기
                var cudnnFolders = Directory.GetDirectories(downloadsPath, "cudnn*")
                    .Where(d => Directory.Exists(Path.Combine(d, "bin")) ||
                                Directory.Exists(Path.Combine(d, "include")) ||
                                Directory.Exists(Path.Combine(d, "lib")))
                    .OrderByDescending(d => Directory.GetCreationTime(d))
                    .ToList();

                if (cudnnFolders.Count > 0)
                    return cudnnFolders[0];

                // 중첩 폴더 확인 (cudnn-windows-x86_64.../cudnn-windows-x86_64... 구조)
                var allCudnnFolders = Directory.GetDirectories(downloadsPath, "cudnn*");
                foreach (var folder in allCudnnFolders)
                {
                    var subFolders = Directory.GetDirectories(folder, "cudnn*");
                    foreach (var subFolder in subFolders)
                    {
                        if (Directory.Exists(Path.Combine(subFolder, "bin")) ||
                            Directory.Exists(Path.Combine(subFolder, "include")) ||
                            Directory.Exists(Path.Combine(subFolder, "lib")))
                        {
                            return subFolder;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"cuDNN 폴더 탐색 실패: {ex.Message}");
                return null;
            }
        }

        private void CopyCuDnnFiles(string sourcePath)
        {
            try
            {
                cudnnCopyButton.Enabled = false;
                cudnnCopyStatusLabel.Text = "복사 중...";
                cudnnCopyStatusLabel.ForeColor = Color.Yellow;
                Application.DoEvents();

                string cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH");
                if (string.IsNullOrEmpty(cudaPath))
                {
                    cudaPath = @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v11.8";
                }

                if (!Directory.Exists(cudaPath))
                {
                    cudnnCopyStatusLabel.Text = "❌ CUDA 폴더를 찾을 수 없습니다";
                    cudnnCopyStatusLabel.ForeColor = Color.Red;
                    cudnnCopyButton.Enabled = true;
                    return;
                }

                int copiedFiles = 0;

                // bin 폴더 복사
                string srcBin = Path.Combine(sourcePath, "bin");
                string dstBin = Path.Combine(cudaPath, "bin");
                if (Directory.Exists(srcBin))
                {
                    copiedFiles += CopyFilesFromFolder(srcBin, dstBin, "*.dll");
                }

                // include 폴더 복사
                string srcInclude = Path.Combine(sourcePath, "include");
                string dstInclude = Path.Combine(cudaPath, "include");
                if (Directory.Exists(srcInclude))
                {
                    copiedFiles += CopyFilesFromFolder(srcInclude, dstInclude, "*.h");
                }

                // lib/x64 폴더 복사
                string srcLib = Path.Combine(sourcePath, "lib", "x64");
                string dstLib = Path.Combine(cudaPath, "lib", "x64");
                if (Directory.Exists(srcLib))
                {
                    copiedFiles += CopyFilesFromFolder(srcLib, dstLib, "*.lib");
                }

                if (copiedFiles > 0)
                {
                    cudnnCopyStatusLabel.Text = $"✅ {copiedFiles}개 파일 복사 완료!";
                    cudnnCopyStatusLabel.ForeColor = Color.LimeGreen;
                    cudnnCopyButton.Text = "✅ 복사 완료";
                    cudnnCopyButton.BackColor = Color.FromArgb(60, 60, 60);

                    MessageBox.Show(
                        $"cuDNN 파일 {copiedFiles}개가 성공적으로 복사되었습니다!\n\n" +
                        "프로그램을 재시작하면 CUDA GPU 가속이 활성화됩니다.",
                        "복사 완료",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                else
                {
                    cudnnCopyStatusLabel.Text = "⚠️ 복사할 파일이 없습니다";
                    cudnnCopyStatusLabel.ForeColor = Color.Orange;
                    cudnnCopyButton.Enabled = true;
                }
            }
            catch (UnauthorizedAccessException)
            {
                cudnnCopyStatusLabel.Text = "❌ 권한 부족 (베타칩을 관리자 권한으로 재실행)";
                cudnnCopyStatusLabel.ForeColor = Color.Red;
                cudnnCopyButton.Enabled = true;

                MessageBox.Show(
                    "CUDA 폴더에 파일을 복사할 권한이 없습니다.\n\n" +
                    "프로그램을 관리자 권한으로 실행하거나,\n" +
                    "수동으로 파일을 복사해주세요.",
                    "권한 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
            catch (Exception ex)
            {
                cudnnCopyStatusLabel.Text = "❌ 복사 실패";
                cudnnCopyStatusLabel.ForeColor = Color.Red;
                cudnnCopyButton.Enabled = true;

                MessageBox.Show(
                    $"파일 복사 중 오류가 발생했습니다:\n{ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private int CopyFilesFromFolder(string srcFolder, string dstFolder, string pattern)
        {
            int count = 0;

            if (!Directory.Exists(dstFolder))
                Directory.CreateDirectory(dstFolder);

            foreach (var file in Directory.GetFiles(srcFolder, pattern))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(dstFolder, fileName);
                File.Copy(file, destFile, overwrite: true);
                count++;
                Console.WriteLine($"복사됨: {fileName}");
            }

            return count;
        }

        private int CreateOnnxStatusRow(int y)
        {
            string statusIcon = detection.CudaRuntimeAvailable ? "✅" : "❌";
            Color statusColor = detection.CudaRuntimeAvailable ? Color.LimeGreen : Color.Red;

            var label = new Label
            {
                Text = $"{statusIcon} ONNX Runtime CUDA",
                Font = new Font("맑은 고딕", 10, FontStyle.Bold),
                Location = new Point(30, y),
                AutoSize = true,
                ForeColor = statusColor
            };
            contentPanel.Controls.Add(label);

            var descLabel = new Label
            {
                Text = detection.CudaRuntimeAvailable
                    ? "CUDA 가속 사용 가능"
                    : "위 항목들을 모두 설치 후 프로그램 재시작 필요",
                Font = new Font("맑은 고딕", 9),
                Location = new Point(280, y + 2),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            contentPanel.Controls.Add(descLabel);

            return y + 30;
        }

        private int CreateFinalStatus(int y)
        {
            var separator = new Label
            {
                BorderStyle = BorderStyle.Fixed3D,
                Location = new Point(20, y),
                Size = new Size(520, 2)
            };
            contentPanel.Controls.Add(separator);
            y += 15;

            string finalText;
            Color finalColor;

            if (detection.CanUseCuda)
            {
                finalText = "🎉 CUDA GPU 가속을 사용할 수 있습니다!";
                finalColor = Color.LimeGreen;
            }
            else if (detection.CanUseDirectML)
            {
                finalText = "✅ DirectML GPU 가속을 사용할 수 있습니다.";
                finalColor = Color.DeepSkyBlue;
            }
            else
            {
                finalText = "⚠️ GPU 가속을 사용할 수 없습니다. 위 항목들을 확인해주세요.";
                finalColor = Color.Orange;
            }

            var finalLabel = new Label
            {
                Text = finalText,
                Font = new Font("맑은 고딕", 11, FontStyle.Bold),
                Location = new Point(20, y),
                AutoSize = true,
                ForeColor = finalColor
            };
            contentPanel.Controls.Add(finalLabel);

            return y + 30;
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"URL을 열 수 없습니다: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}