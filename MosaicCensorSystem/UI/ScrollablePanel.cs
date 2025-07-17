using System;
using System.Drawing;
using System.Windows.Forms;

namespace MosaicCensorSystem.UI
{
    /// <summary>
    /// 내용이 길어지면 자동으로 스크롤바를 생성하는 패널 (단순화 버전)
    /// </summary>
    public class ScrollablePanel : Panel
    {
        /// <summary>
        /// 실제 컨트롤들이 추가될, 스크롤되는 내부 패널.
        /// 이 패널의 크기가 ScrollablePanel보다 커지면 스크롤바가 나타납니다.
        /// </summary>
        public Panel ScrollableFrame { get; }

        public ScrollablePanel()
        {
            // 1. 스크롤바가 자동으로 나타나도록 설정합니다.
            this.AutoScroll = true;
            this.Dock = DockStyle.Fill;
            this.BackColor = SystemColors.Control;

            // 2. 스크롤될 내용을 담을 내부 패널을 생성합니다.
            ScrollableFrame = new Panel
            {
                // 위치는 (0,0)으로 고정하고, 내용에 따라 크기가 자동으로 조절되도록 합니다.
                Location = new Point(0, 0),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            // 3. 스크롤 기능을 가진 메인 패널(this)에 내부 패널을 추가합니다.
            this.Controls.Add(ScrollableFrame);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // ScrollableFrame은 메인 패널의 Controls 컬렉션에 의해 자동으로 정리됩니다.
            }
            base.Dispose(disposing);
        }
    }
}