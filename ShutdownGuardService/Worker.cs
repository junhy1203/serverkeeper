using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ShutdownGuardService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly string _targetExeName = "ShutdownGuard";

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }

                CheckAndRestartApp();

                await Task.Delay(5000, stoppingToken);
            }
        }

        private void CheckAndRestartApp()
        {
            try
            {
                var processes = Process.GetProcessesByName(_targetExeName);
                if (processes.Length == 0)
                {
                    _logger.LogInformation("ShutdownGuard.exe is not running. Attempting to start it in active user sessions.");
                    StartProcessInUserSessions();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking or restarting app.");
            }
        }

        private void StartProcessInUserSessions()
        {
            // For simplicity in this example, we rely on the Startup folder/registry for initial launch.
            // If it's killed, starting it from Session 0 into Session 1+ is complex without Cassia or pinvoke.
            // As a fallback, we can use the 'schtasks' command to run it as the logged-on user.
            
            try
            {
                string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ShutdownGuard.exe");
                if (File.Exists(exePath))
                {
                    // This is a simplified approach; in production, CreateProcessAsUser is preferred.
                    // For now, if the user terminates it, it will be caught here but we need a robust way to relaunch it in their session.
                    // We will just log it for this plan, and let the startup registry handle the primary launch.
                    _logger.LogWarning("ShutdownGuard terminated. Re-launching from Session 0 requires CreateProcessAsUser.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting process in user session.");
            }
        }
    }
}
