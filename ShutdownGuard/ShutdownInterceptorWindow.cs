using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ShutdownGuard
{
    /// <summary>
    /// 숨겨진 창으로 WM_QUERYENDSESSION을 후킹 - 모든 종료/재시작 시도를 차단
    /// </summary>
    public class ShutdownInterceptorWindow : Window
    {
        private const int WM_QUERYENDSESSION = 0x0011;
        private const int ENDSESSION_LOGOFF  = unchecked((int)0x80000000);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool ShutdownBlockReasonCreate(IntPtr hWnd, string reason);

        [DllImport("user32.dll")]
        private static extern bool ShutdownBlockReasonDestroy(IntPtr hWnd);

        public static bool AllowNextShutdown = false;
        private bool _allowNext = false; // 인증 통과 후 다음 WM_QUERYENDSESSION은 허용

        public ShutdownInterceptorWindow()
        {
            // 화면 밖 1x1 픽셀 창 (완전히 안 보이지만 HWND는 살아있음)
            Width  = 1;
            Height = 1;
            Left   = -32000;
            Top    = -32000;
            WindowStyle   = WindowStyle.None;
            ShowInTaskbar = false;
            ShowActivated = false;
            AllowsTransparency = true;
            Opacity    = 0;
            Background = System.Windows.Media.Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            // Windows에게 "이 앱이 종료를 막겠다"고 등록
            ShutdownBlockReasonCreate(hwnd, "ShutdownGuard - 종료 보호 활성화됨");

            // 메시지 후크 등록
            HwndSource.FromHwnd(hwnd).AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_QUERYENDSESSION) return IntPtr.Zero;

            bool isLogoff = ((int)lParam & ENDSESSION_LOGOFF) != 0;

            // 로그아웃은 무조건 차단 (다이얼로그 없이)
            if (isLogoff)
            {
                handled = true;
                return IntPtr.Zero;
            }

            if (AllowNextShutdown)
            {
                AllowNextShutdown = false;
                ShutdownBlockReasonDestroy(hwnd);
                handled = true;
                return new IntPtr(1);
            }

            // 인증 통과 후 실제 종료/재시작 허용
            if (_allowNext)
            {
                _allowNext = false;
                ShutdownBlockReasonDestroy(hwnd);
                handled = true;
                return new IntPtr(1);
            }

            // UI 스레드에서 다이얼로그 표시 (ShowDialog = 동기적 대기)
            bool confirmed = false;
            Dispatcher.Invoke(() =>
            {
                // 이미 열린 다이얼로그가 있으면 포커스만
                foreach (Window w in System.Windows.Application.Current.Windows)
                {
                    if (w is ShutdownDialog existing)
                    {
                        existing.Activate();
                        return;
                    }
                }
                var dlg = new ShutdownDialog(isQueryEndSession: true);
                dlg.ShowDialog();
                confirmed = dlg.IsConfirmed;
            });

            if (confirmed)
            {
                _allowNext = true;
                // 실제 종료는 ShutdownDialog가 shutdown.exe를 호출함
                // WM_QUERYENDSESSION도 그 다음에 올 수 있으므로 allowNext=true
                handled = true;
                return new IntPtr(1);
            }
            else
            {
                // 차단: 이유 재등록
                ShutdownBlockReasonCreate(hwnd, "ShutdownGuard - 종료 보호 활성화됨");
                handled = true;
                return IntPtr.Zero;
            }
        }
    }
}
