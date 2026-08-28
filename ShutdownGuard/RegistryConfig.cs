using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace ShutdownGuard
{
    public static class RegistryConfig
    {
        public static void ApplyRestrictions()
        {
            try
            {
                // ======== 잠금(Win+L) 비활성화 ========
                SetRegistryValue(Registry.CurrentUser,
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\System",
                    "DisableLockWorkstation", 1, RegistryValueKind.DWord);

                // ======== Ctrl+Alt+Del 화면의 전원 버튼 제거 ========
                SetRegistryValue(Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                    "ShutdownWithoutLogon", 0, RegistryValueKind.DWord);

                // ======== Windows Update 자동 재시작 차단 ========
                SetRegistryValue(Registry.LocalMachine,
                    @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                    "NoAutoRebootWithLoggedOnUsers", 1, RegistryValueKind.DWord);

                // ======== 절전 모드 비활성화 ========
                RunCommand("powercfg /h off");
                RunCommand("powercfg /change standby-timeout-ac 0");
                RunCommand("powercfg /change standby-timeout-dc 0");

                // ======== 시작메뉴 전원 옵션(종료/절전/다시시작) 및 로그아웃 제거 ========
                SetRegistryValue(Registry.CurrentUser,
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                    "NoClose", 1, RegistryValueKind.DWord);
                SetRegistryValue(Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                    "NoClose", 1, RegistryValueKind.DWord);

                SetRegistryValue(Registry.CurrentUser,
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                    "NoLogOff", 1, RegistryValueKind.DWord);
                SetRegistryValue(Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                    "NoLogOff", 1, RegistryValueKind.DWord);

                // ======== Explorer 재시작 → 레지스트리 즉시 적용 ========
                RestartExplorer();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ApplyRestrictions Error: " + ex.Message);
            }
        }

        public static void RevertRestrictions(bool restartExplorer = true)
        {
            try
            {
                DeleteRegistryValue(Registry.CurrentUser,
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\System",
                    "DisableLockWorkstation");

                SetRegistryValue(Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                    "ShutdownWithoutLogon", 1, RegistryValueKind.DWord);

                DeleteRegistryValue(Registry.LocalMachine,
                    @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                    "NoAutoRebootWithLoggedOnUsers");

                DeleteRegistryValue(Registry.CurrentUser,
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                    "NoClose");
                DeleteRegistryValue(Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                    "NoClose");

                DeleteRegistryValue(Registry.CurrentUser,
                    @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                    "NoLogOff");
                DeleteRegistryValue(Registry.LocalMachine,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                    "NoLogOff");

                if (restartExplorer)
                {
                    RestartExplorer();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("RevertRestrictions Error: " + ex.Message);
            }
        }

        private static void RestartExplorer()
        {
            try
            {
                // Explorer 프로세스 강제 종료
                foreach (Process p in Process.GetProcessesByName("explorer"))
                {
                    p.Kill();
                    p.WaitForExit(2000);
                }

                System.Threading.Thread.Sleep(500);

                // Explorer 재시작
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("RestartExplorer Error: " + ex.Message);
            }
        }

        private static void RunCommand(string cmd)
        {
            Process.Start(new ProcessStartInfo("cmd.exe", "/c " + cmd)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            })?.WaitForExit(5000);
        }

        private static void SetRegistryValue(RegistryKey baseKey, string subKeyName, string valueName, object value, RegistryValueKind kind)
        {
            using RegistryKey? key = baseKey.CreateSubKey(subKeyName, true);
            key?.SetValue(valueName, value, kind);
        }

        private static void DeleteRegistryValue(RegistryKey baseKey, string subKeyName, string valueName)
        {
            using RegistryKey? key = baseKey.OpenSubKey(subKeyName, true);
            key?.DeleteValue(valueName, false);
        }
    }
}
