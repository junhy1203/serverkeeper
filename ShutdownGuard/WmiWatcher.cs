using System;
using System.Diagnostics;
using System.Management;
using System.Windows;

namespace ShutdownGuard
{
    public class WmiWatcher
    {
        public static WmiWatcher? Instance { get; private set; }

        private ManagementEventWatcher? _watcher;
        private bool _isStopped = false;

        public WmiWatcher()
        {
            Instance = this;
        }

        public void Start()
        {
            try
            {
                _isStopped = false;
                WqlEventQuery query = new WqlEventQuery(
                    "SELECT * FROM __InstanceCreationEvent WITHIN 1 " +
                    "WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.Name = 'shutdown.exe'");
                
                _watcher = new ManagementEventWatcher(query);
                _watcher.EventArrived += Watcher_EventArrived;
                _watcher.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WMI Watcher Start Error: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isStopped = true;
            if (_watcher != null)
            {
                _watcher.Stop();
                _watcher.Dispose();
                _watcher = null;
            }
        }

        private void Watcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            if (_isStopped) return;

            try
            {
                var targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
                uint processId = (uint)targetInstance["ProcessId"];
                string commandLine = (string)targetInstance["CommandLine"];

                // Kill the shutdown.exe process immediately
                try
                {
                    Process p = Process.GetProcessById((int)processId);
                    p.Kill();
                }
                catch
                {
                    // Process might already be dead
                }

                // Show our custom dialog on the UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Check if dialog is already open to prevent multiple dialogs
                    foreach (Window window in System.Windows.Application.Current.Windows)
                    {
                        if (window is ShutdownDialog)
                        {
                            window.Activate();
                            return;
                        }
                    }

                    var dialog = new ShutdownDialog();
                    dialog.Show();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WMI Watcher Event Error: {ex.Message}");
            }
        }
    }
}
