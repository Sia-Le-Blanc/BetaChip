using System;
using System.IO;
using System.Windows.Forms;

namespace MosaicCensorSystem
{
    internal static class Program
    {
        // ONNX 모델 파일의 경로를 프로그램 실행 위치 기반의 절대 경로로 수정
        public static readonly string ONNX_MODEL_PATH = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            @"Resources\best.onnx"
        );

        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                File.WriteAllText("fatal_error.log", $"{DateTime.Now}: {ex?.ToString()}");
                MessageBox.Show("치명적인 오류가 발생했습니다. 프로그램을 종료합니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                var app = new MosaicApp();
                app.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"프로그램 초기화 중 오류가 발생했습니다: {ex.Message}", "초기화 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                File.WriteAllText("init_error.log", $"{DateTime.Now}: {ex.ToString()}");
            }
        }
    }
}