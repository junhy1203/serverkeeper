using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Automation;

namespace ShutdownGuard
{
    public class PowerMenuWatcher : IDisposable
    {
        private bool _isRunning = true;
        private Thread _pollingThread;

        public PowerMenuWatcher()
        {
            _pollingThread = new Thread(PollForPowerMenu);
            _pollingThread.SetApartmentState(ApartmentState.STA);
            _pollingThread.IsBackground = true;
            _pollingThread.Start();
        }

        private void PollForPowerMenu()
        {
            var textCond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text);
            var menuCond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem);
            var paneCond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Pane);
            var winCond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window);
            
            while (_isRunning)
            {
                try
                {
                    AutomationElement found = null;
                    IntPtr fgHwnd = GetForegroundWindow();
                    
                    if (fgHwnd != IntPtr.Zero)
                    {
                        var fgElement = AutomationElement.FromHandle(fgHwnd);
                        var condition = new PropertyCondition(AutomationElement.NameProperty, "현재 사용 가능한 전원 옵션이 없습니다.");
                        found = fgElement.FindFirst(TreeScope.Descendants, condition);
                        
                        if (found == null)
                        {
                            var elements = fgElement.FindAll(TreeScope.Descendants, new OrCondition(textCond, menuCond));
                            foreach (AutomationElement el in elements)
                            {
                                if (el.Current.Name.Contains("전원 옵션이 없"))
                                {
                                    found = el;
                                    break;
                                }
                            }
                        }
                    }

                    if (found != null)
                    {
                        ShowCustomDialog();
                        
                        // 창이 뜬 후, 시작 메뉴의 "전원 옵션이 없습니다" 텍스트가 사라질 때까지 대기
                        while (_isRunning)
                        {
                            Thread.Sleep(500);
                            bool stillExists = false;
                            IntPtr currentFg = GetForegroundWindow();
                            
                            if (currentFg != IntPtr.Zero)
                            {
                                var fgElCheck = AutomationElement.FromHandle(currentFg);
                                var findCheck = fgElCheck.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "현재 사용 가능한 전원 옵션이 없습니다."));
                                
                                if (findCheck != null)
                                {
                                    stillExists = true;
                                }
                                else
                                {
                                    var elementsCheck = fgElCheck.FindAll(TreeScope.Descendants, new OrCondition(textCond, menuCond));
                                    foreach (AutomationElement el in elementsCheck)
                                    {
                                        if (el.Current.Name.Contains("전원 옵션이 없"))
                                        {
                                            stillExists = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            
                            if (!stillExists) break; // 텍스트가 화면에서 사라지면 원래 폴링으로 복귀
                        }
                    }
                }
                catch (Exception)
                {
                    // Ignore
                }

                Thread.Sleep(400); // 0.4초마다 검사
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private void ShowCustomDialog()
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
            _isRunning = false;
        }
    }
}
