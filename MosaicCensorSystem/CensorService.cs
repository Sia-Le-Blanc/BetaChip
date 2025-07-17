#nullable disable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using MosaicCensorSystem.Capture;
using MosaicCensorSystem.Detection;
using MosaicCensorSystem.Overlay;
using MosaicCensorSystem.UI;
using OpenCvSharp;

namespace MosaicCensorSystem
{
    /// <summary>
    /// 화면 캡처, 객체 감지, 오버레이 업데이트 등 핵심 로직을 처리하는 서비스
    /// </summary>
    public class CensorService : IDisposable
    {
        private readonly GuiController ui;
        private readonly ScreenCapturer capturer;
        private readonly MosaicProcessor processor;
        private readonly FullscreenOverlay overlay;

        private Thread processThread;
        private volatile bool isRunning = false;

        private int targetFPS = 15;
        private bool enableDetection = true;
        private bool enableCensoring = true;

        public CensorService(GuiController uiController)
        {
            ui = uiController;
            capturer = new ScreenCapturer();
            processor = new MosaicProcessor(Program.ONNX_MODEL_PATH);
            overlay = new FullscreenOverlay();
        }

        public void Start()
        {
            if (isRunning) return;
            if (!processor.IsModelLoaded())
            {
                ui.LogMessage("❌ 모델 파일이 로드되지 않아 시작할 수 없습니다.");
                MessageBox.Show("ONNX 모델 파일(best.onnx)을 찾을 수 없습니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            isRunning = true;
            ui.SetRunningState(true);
            ui.UpdateStatus("🚀 시스템 실행 중...", Color.Green);

            overlay.Show();
            
            processThread = new Thread(ProcessingLoop) { IsBackground = true, Name = "CensorProcessingThread" };
            processThread.Start();
        }

        public void Stop()
        {
            if (!isRunning) return;
            isRunning = false;
            processThread?.Join(1000);

            overlay.Hide();
            ui.SetRunningState(false);
            ui.UpdateStatus("⭕ 시스템 대기 중", Color.Red);
        }

        private void ProcessingLoop()
        {
            while (isRunning)
            {
                var frameStart = DateTime.Now;
                
                using Mat frame = capturer.GetFrame();
                if (frame == null || frame.Empty())
                {
                    Thread.Sleep(30); // 캡처 실패 시 잠시 대기
                    continue;
                }

                // 작업용 프레임 복사
                using Mat displayFrame = frame.Clone();

                if (enableDetection)
                {
                    var detections = processor.DetectObjects(frame);
                    if (enableCensoring && detections.Count > 0)
                    {
                        foreach (var detection in detections)
                        {
                            // ApplySingleCensorOptimized는 내부에서 ROI를 다루므로 복사본에 적용
                            processor.ApplySingleCensorOptimized(displayFrame, detection);
                        }
                    }
                }

                overlay.UpdateFrame(displayFrame);

                // FPS 제어
                var elapsedMs = (DateTime.Now - frameStart).TotalMilliseconds;
                int delay = (1000 / targetFPS) - (int)elapsedMs;
                if (delay > 0)
                {
                    Thread.Sleep(delay);
                }
            }
        }

        public void UpdateSetting(string key, object value)
        {
            switch (key)
            {
                case "TargetFPS": targetFPS = (int)value; break;
                case "EnableDetection": enableDetection = (bool)value; break;
                case "EnableCensoring": enableCensoring = (bool)value; break;
                case "CensorType": processor.SetCensorType((CensorType)value); break;
                case "Strength": processor.SetStrength((int)value); break;
                case "Confidence": processor.ConfThreshold = (float)value; break;
                case "Targets": processor.SetTargets((List<string>)value); break;
            }
        }

        public void TestCapture()
        {
            try
            {
                using Mat testFrame = capturer.GetFrame();
                if (testFrame != null && !testFrame.Empty())
                {
                    string testPath = Path.Combine(Environment.CurrentDirectory, "capture_test.jpg");
                    testFrame.SaveImage(testPath);
                    ui.LogMessage($"✅ 캡처 테스트 성공! 크기: {testFrame.Width}x{testFrame.Height}");
                    MessageBox.Show($"캡처 테스트 성공! 이미지가 {testPath}에 저장되었습니다.", "성공", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    ui.LogMessage("❌ 캡처 테스트 실패: 빈 프레임이 반환되었습니다.");
                }
            }
            catch (Exception ex)
            {
                ui.LogMessage($"❌ 캡처 테스트 오류: {ex.Message}");
            }
        }
        
        public void Dispose()
        {
            Stop();
            capturer?.Dispose();
            processor?.Dispose();
            overlay?.Dispose();
        }
    }
}