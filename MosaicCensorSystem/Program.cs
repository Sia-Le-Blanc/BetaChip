using System;
using System.IO;
using System.Windows.Forms;

namespace MosaicCensorSystem
{
    internal static class Program
    {
        /// <summary>
        /// ONNX 모델 파일의 상대 경로.
        /// </summary>
        public const string ONNX_MODEL_PATH = @"Resources\best.onnx";

        [STAThread]
        static void Main()
        {
            // 애플리케이션 전체에서 발생하는 처리되지 않은 예외를 기록합니다.
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                File.WriteAllText("fatal_error.log", $"{DateTime.Now}: {ex?.ToString()}");
                MessageBox.Show("치명적인 오류가 발생했습니다. 프로그램을 종료합니다.", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            // Windows Forms 애플리케이션을 초기화하고 실행합니다.
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                var app = new MosaicApp();
                app.Run();
            }
            catch (Exception ex)
            {
                // 초기화 과정에서 발생하는 예외를 처리합니다.
                MessageBox.Show($"프로그램 초기화 중 오류가 발생했습니다: {ex.Message}", "초기화 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                File.WriteAllText("init_error.log", $"{DateTime.Now}: {ex.ToString()}");
            }

            // ★★★ 추가된 코드: 콘솔 창을 유지하여 로그를 볼 수 있도록 함 ★★★
            Console.WriteLine("콘솔 출력을 확인하려면 아무 키나 누르세요...");
            Console.ReadLine();
        }
    }
}