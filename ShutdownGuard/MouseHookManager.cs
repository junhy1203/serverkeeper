using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;

namespace ShutdownGuard
{
    public class MouseHookManager : IDisposable
    {
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        private static IntPtr _hookID = IntPtr.Zero;
        private static LowLevelMouseProc _proc = HookCallback;
        private static System.Collections.Concurrent.ConcurrentQueue<System.Windows.Point> _clickQueue = new System.Collections.Concurrent.ConcurrentQueue<System.Windows.Point>();
        private static Thread _processingThread;
        private static readonly object _logLock = new object();

        public MouseHookManager()
        {
            try {
                System.IO.File.AppendAllText(@"C:\ShutdownGuard\hook.log", $"[{DateTime.Now:HH:mm:ss}] MouseHookManager initializing...\n");
                _hookID = SetHook(_proc);
                System.IO.File.AppendAllText(@"C:\ShutdownGuard\hook.log", $"[{DateTime.Now:HH:mm:ss}] _hookID: {_hookID}\n");
            } catch (Exception ex) {
                System.IO.File.AppendAllText(@"C:\ShutdownGuard\hook.log", $"[{DateTime.Now:HH:mm:ss}] Constructor ERROR: {ex.Message}\n");
            }
            
            _processingThread = new Thread(ProcessClicks);
            _processingThread.SetApartmentState(ApartmentState.STA);
            _processingThread.IsBackground = true;
            _processingThread.Start();
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static void ProcessClicks()
        {
            while (true)
            {
                if (_clickQueue.TryDequeue(out System.Windows.Point pt))
                {
                    int x = (int)pt.X;
                    int y = (int)pt.Y;
                    try {
                        lock (_logLock) {
                            System.IO.File.AppendAllText(@"C:\ShutdownGuard\hook.log", $"[{DateTime.Now:HH:mm:ss}] Processing Click at {x},{y}\n");
                        }
                    } catch {}
                    
                    CheckAndShowDialog(x, y);
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
            {
                try {
                    MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT))!;
                    _clickQueue.Enqueue(new System.Windows.Point(hookStruct.pt.x, hookStruct.pt.y));
                } catch {}
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static void CheckAndShowDialog(int x, int y)
        {
            try
            {
                var pt = new System.Windows.Point(x, y);
                var element = AutomationElement.FromPoint(pt);
                if (element != null)
                {
                    string name = element.Current.Name ?? "";
                    string autoId = element.Current.AutomationId ?? "";
                    string className = element.Current.ClassName ?? "";
                    string ctrlType = element.Current.ControlType?.ProgrammaticName ?? "";
                    
                    string pName = "", pAutoId = "", pClass = "";
                    try {
                        var parent = TreeWalker.ControlViewWalker.GetParent(element);
                        if (parent != null) {
                            pName = parent.Current.Name ?? "";
                            pAutoId = parent.Current.AutomationId ?? "";
                            pClass = parent.Current.ClassName ?? "";
                        }
                    } catch {}

                    try {
                        lock (_logLock) {
                            System.IO.File.AppendAllText(@"C:\ShutdownGuard\hook.log", 
                                $"[{DateTime.Now:HH:mm:ss}] Click at {x},{y}\n  Element: Name='{name}', AutoId='{autoId}', Class='{className}', Type='{ctrlType}'\n  Parent: Name='{pName}', AutoId='{pAutoId}', Class='{pClass}'\n");
                        }
                    } catch {}

                    if (name.Contains("전원") || autoId.Contains("Power") || name.Contains("Power") || autoId.Contains("UCPowerButton") ||
                        pName.Contains("전원") || pAutoId.Contains("Power") || pName.Contains("Power") || pAutoId.Contains("UCPowerButton") ||
                        className.Contains("PowerButton") || pClass.Contains("PowerButton"))
                    {
                        ShowCustomDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("UIA Error: " + ex.Message);
            }
        }

        private static void ShowCustomDialog()
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (Window win in System.Windows.Application.Current.Windows)
                {
                    if (win is ShutdownDialog)
                    {
                        win.Activate();
                        return;
                    }
                }

                var dialog = new ShutdownDialog(isQueryEndSession: false);
                dialog.Show();
            }));
        }

        public void Dispose()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }
    }
}
