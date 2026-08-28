using System;
using System.Diagnostics;
using System.Windows;

namespace ShutdownGuard
{
    public partial class ShutdownDialog : Window
    {
        private readonly string _expectedCode;
        private readonly bool _isQueryEndSession;

        /// <summary>코드 인증 통과 여부 (WM_QUERYENDSESSION 모드에서 호출자가 읽음)</summary>
        public bool IsConfirmed { get; private set; } = false;

        /// <param name="isQueryEndSession">
        /// true  = WM_QUERYENDSESSION에서 호출됨 → 종료/재시작 버튼 눌러도 shutdown.exe 직접 호출
        ///         (Windows가 이미 종류를 알고 있으므로 IsConfirmed=true만 반환하면 됨)
        /// false = Alt+F4 / WMI 에서 호출됨 → shutdown.exe 직접 호출
        /// </param>
        public ShutdownDialog(bool isQueryEndSession = false)
        {
            InitializeComponent();
            _isQueryEndSession = isQueryEndSession;
            _expectedCode = new Random().Next(1000, 10000).ToString();
            CodeTextBlock.Text = _expectedCode;
            InputTextBox.Focus();
        }

        private void ShutdownButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateCode()) return;

            IsConfirmed = true;
            if (!_isQueryEndSession)
            {
                // Alt+F4 / WMI 경로: 직접 종료 명령
                DoShutdown("shutdown /s /f /t 0");
            }
            // WM_QUERYENDSESSION 경로: Windows가 알아서 처리, IsConfirmed=true만 반환
            this.DialogResult = true;
            this.Close();
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateCode()) return;

            IsConfirmed = true;
            if (!_isQueryEndSession)
            {
                DoShutdown("shutdown /r /f /t 0");
            }
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            this.DialogResult = false;
            this.Close();
        }

        private bool ValidateCode()
        {
            if (InputTextBox.Text.Trim() == _expectedCode)
                return true;

            System.Windows.MessageBox.Show(
                "코드가 일치하지 않습니다.\n다시 입력해주세요.",
                "코드 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            InputTextBox.Clear();
            InputTextBox.Focus();
            return false;
        }

        private void DoShutdown(string command)
        {
            ShutdownInterceptorWindow.AllowNextShutdown = true;
            WmiWatcher.Instance?.Stop();
            RegistryConfig.RevertRestrictions(restartExplorer: false);
            
            Process.Start(new ProcessStartInfo("cmd.exe", "/c " + command)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
            // 윈도우가 종료 절차를 밟게 둠.
        }
    }
}
