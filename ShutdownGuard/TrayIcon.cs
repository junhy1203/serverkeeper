using System;
using System.Drawing;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace ShutdownGuard
{
    public class TrayIcon : IDisposable
    {
        private NotifyIcon _notifyIcon;

        public TrayIcon()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = SystemIcons.Shield; // default icon
            _notifyIcon.Text = "종료 보호 작동 중";
            _notifyIcon.Visible = true;

            ContextMenuStrip menu = new ContextMenuStrip();
            
            var exitItem = new ToolStripMenuItem("종료 보호 끄기");
            exitItem.Click += ExitItem_Click;
            menu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = menu;
        }

        private void ExitItem_Click(object? sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "종료 보호를 해제하시겠습니까?\n해제하면 일반적인 방법으로 서버를 종료할 수 있습니다.", 
                "보호 해제 확인", 
                MessageBoxButtons.YesNo, 
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        }
    }
}
