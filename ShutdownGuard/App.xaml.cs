using System.Windows;
using System.Threading;

namespace ShutdownGuard;

public partial class App : System.Windows.Application
{
    private GlobalHook? _globalHook;
    private WmiWatcher? _wmiWatcher;
    private TrayIcon? _trayIcon;
    private Mutex? _mutex;
    private ShutdownInterceptorWindow? _interceptorWindow;
    private PowerMenuWatcher? _powerMenuWatcher;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 폴링 방식으로 시작 메뉴 전원 버튼 툴팁/메뉴 감지
        _powerMenuWatcher = new PowerMenuWatcher();

        // Ensure only one instance runs
        bool isNewInstance;
        _mutex = new Mutex(true, "ShutdownGuard_SingleInstance", out isNewInstance);
        if (!isNewInstance)
        {
            System.Windows.MessageBox.Show("이미 종료 보호 프로그램이 실행 중입니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            Current.Shutdown();
            return;
        }

        // Initialize components
        _trayIcon = new TrayIcon();

        // WM_QUERYENDSESSION 후킹 창 (시작 메뉴 전원 버튼 인터셉트)
        _interceptorWindow = new ShutdownInterceptorWindow();
        _interceptorWindow.Show();
        
        _globalHook = new GlobalHook();
        _globalHook.AltF4Pressed += (s, args) =>
        {
            Dispatcher.Invoke(() =>
            {
                // Check if dialog is already open
                foreach (Window window in Current.Windows)
                {
                    if (window is ShutdownDialog)
                    {
                        window.Activate();
                        return;
                    }
                }
                new ShutdownDialog().Show();
            });
        };

        // CRITICAL: Don't exit when the dialog window is closed
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _wmiWatcher = new WmiWatcher();
        _wmiWatcher.Start();

        RegistryConfig.ApplyRestrictions();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _globalHook?.Dispose();
        _wmiWatcher?.Stop();
        _powerMenuWatcher?.Dispose();
        _trayIcon?.Dispose();
        _interceptorWindow?.Close();
        WmiWatcher.Instance?.Stop();
        RegistryConfig.RevertRestrictions();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
