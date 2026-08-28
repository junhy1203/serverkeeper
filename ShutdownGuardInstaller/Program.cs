using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace ShutdownGuardInstaller
{
    class Program
    {
        static readonly string InstallDir  = @"C:\ShutdownGuard";
        static readonly string ServiceName = "ShutdownGuardService";

        [STAThread]
        static void Main(string[] args)
        {
            Console.Title = "ShutdownGuard 설치 프로그램";
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            if (args.Length > 0 && args[0] == "/uninstall")
            {
                Uninstall();
                return;
            }

            Console.WriteLine("================================================");
            Console.WriteLine("   ShutdownGuard 설치 프로그램 (v16 - UI 잘림 현상 해결 (버튼 표시))");
            Console.WriteLine("================================================");
            Console.WriteLine();

            try
            {
                Install();
                Console.WriteLine();
                Console.WriteLine("[완료] 설치가 성공적으로 완료되었습니다!");
                Console.WriteLine($"       설치 경로: {InstallDir}");
                Console.WriteLine();
                Console.WriteLine("지금 바로 ShutdownGuard를 실행합니다...");
                StartApp();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[오류] {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.WriteLine("아무 키나 누르면 종료합니다...");
            Console.ReadKey();
        }

        static void Install()
        {
            Console.WriteLine("[1/5] 기존 설치 정리 중...");
            CleanupExisting();

            Console.WriteLine("[2/5] 설치 폴더 준비 중...");
            if (Directory.Exists(InstallDir))
                Directory.Delete(InstallDir, true);
            Directory.CreateDirectory(InstallDir);

            Console.WriteLine("[3/5] 파일 추출 및 설치 중...");
            ExtractAndUnzip("payload.zip", InstallDir);
            Console.WriteLine("       추출 완료.");

            Console.WriteLine("[4/5] Windows 서비스 등록 중...");
            string svcExe = Path.Combine(InstallDir, "ShutdownGuardService.exe");
            RunCmd($@"sc create {ServiceName} binPath= ""{svcExe}"" start= auto DisplayName= ""Shutdown Guard Service""");
            RunCmd($"sc start {ServiceName}");
            Console.WriteLine("       서비스 등록 및 시작 완료.");

            Console.WriteLine("[5/5] 시작 프로그램 등록 및 절전 비활성화 중...");
            string appExe = Path.Combine(InstallDir, "ShutdownGuard.exe");
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true)!)
            {
                key.SetValue("ShutdownGuard", $"\"{appExe}\"");
            }

            RunCmd("powercfg /h off");
            RunCmd("powercfg /change standby-timeout-ac 0");
            RunCmd("powercfg /change standby-timeout-dc 0");
            Console.WriteLine("       완료.");
        }

        static void Uninstall()
        {
            Console.WriteLine("ShutdownGuard를 제거합니다...");
            CleanupExisting();

            using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
            {
                key?.DeleteValue("ShutdownGuard", false);
            }

            if (Directory.Exists(InstallDir))
            {
                try { Directory.Delete(InstallDir, true); }
                catch (Exception ex) { Console.WriteLine($"[경고] 폴더 삭제 실패: {ex.Message}"); }
            }

            RevertRegistryRestrictions();
            Console.WriteLine("제거 완료.");
            Console.WriteLine("아무 키나 누르면 종료합니다...");
            Console.ReadKey();
        }

        static void CleanupExisting()
        {
            RunCmd("taskkill /f /im ShutdownGuard.exe 2>nul");
            RunCmd("taskkill /f /im ShutdownGuardService.exe 2>nul");
            System.Threading.Thread.Sleep(1000);
            RunCmd($"sc stop {ServiceName} 2>nul");
            System.Threading.Thread.Sleep(1000);
            RunCmd($"sc delete {ServiceName} 2>nul");
            System.Threading.Thread.Sleep(1000);
        }

        static void StartApp()
        {
            string appExe = Path.Combine(InstallDir, "ShutdownGuard.exe");
            if (File.Exists(appExe))
            {
                // UAC 매니페스트가 있으므로 정상적으로 관리자 권한으로 실행됩니다.
                Process.Start(new ProcessStartInfo(appExe) { UseShellExecute = true });
            }
            else
            {
                throw new FileNotFoundException($"설치 실패: 주 실행 파일이 없습니다 -> {appExe}");
            }
        }

        static void ExtractAndUnzip(string resourceName, string destFolder)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using Stream? stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new FileNotFoundException($"내장 리소스를 찾을 수 없음: {resourceName}");
            }

            string tempZip = Path.Combine(Path.GetTempPath(), "ShutdownGuard_payload.zip");
            using (FileStream fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write))
            {
                stream.CopyTo(fs);
            }

            ZipFile.ExtractToDirectory(tempZip, destFolder, true);
            File.Delete(tempZip);
        }

        static void RevertRegistryRestrictions()
        {
            try
            {
                DeleteRegValue(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoClose");
                DeleteRegValue(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoClose");
                DeleteRegValue(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoLogOff");
                DeleteRegValue(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoLogOff");
                DeleteRegValue(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableLockWorkstation");
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true);
                key?.SetValue("ShutdownWithoutLogon", 1, RegistryValueKind.DWord);
            }
            catch { }
        }

        static void DeleteRegValue(RegistryKey hive, string path, string name)
        {
            using RegistryKey? key = hive.OpenSubKey(path, true);
            key?.DeleteValue(name, false);
        }

        static void RunCmd(string cmd)
        {
            Process.Start(new ProcessStartInfo("cmd.exe", "/c " + cmd)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            })?.WaitForExit(5000);
        }
    }
}
