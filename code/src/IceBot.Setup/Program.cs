using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;

namespace IceBot.Setup;

internal static class Program
{
    private const int NetFramework472Release = 461808;
    private const string NetBirdPackageId = "Netbird.Netbird";

    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Title = "IceBot Setup";
        PrintHeader();

        try
        {
            if (!OperatingSystem.IsWindows())
                throw new InvalidOperationException("Setup.exe chỉ hỗ trợ Windows.");

            var source = GetArgument(args, "--source")
                ?? Path.Combine(AppContext.BaseDirectory, "payload");
            var installDirectory = GetArgument(args, "--install-dir");
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                installDirectory = SelectInstallDirectory();
                if (installDirectory == null)
                {
                    Console.WriteLine("[CANCELLED] Người dùng đã hủy cài đặt. Không có file nào được thay đổi.");
                    Pause();
                    return 0;
                }
            }

            source = Path.GetFullPath(source);
            installDirectory = Path.GetFullPath(installDirectory);
            ValidatePayload(source);

            Console.WriteLine("[1/5] Kiểm tra .NET Framework 4.7.2+");
            EnsureNetFramework(AppContext.BaseDirectory);

            Console.WriteLine("[2/5] Cài đặt NetBird");
            EnsureNetBird(AppContext.BaseDirectory);

            Console.WriteLine($"[3/5] Cài IceBot vào {installDirectory}");
            CopyPayload(source, installDirectory);

            Console.WriteLine("[4/5] Tạo thư mục dữ liệu");
            CreateRuntimeDirectories(installDirectory);
            SetRuntimePermissions(installDirectory);
            CreateSharedDriverDirectory();

            Console.WriteLine("[5/5] Tạo shortcut");
            CreateShortcuts(installDirectory);

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("[OK] CÀI ĐẶT ICEBOT HOÀN TẤT");
            Console.WriteLine("Bước tiếp theo: chạy InitIceBot.exe để khởi tạo Edge.");
            Console.WriteLine("Sau khi khởi tạo thành công, chạy IceBot.exe để bán hàng.");
            Console.WriteLine("========================================");
            Pause();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("========================================");
            Console.Error.WriteLine("[ERROR] CÀI ĐẶT THẤT BẠI");
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("========================================");
            Pause();
            return 1;
        }
    }

    private static void ValidatePayload(string source)
    {
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException($"Không tìm thấy payload: {source}");

        foreach (var file in new[] { "IceBot.exe", "InitIceBot.exe" })
        {
            if (!File.Exists(Path.Combine(source, file)))
                throw new FileNotFoundException($"Payload thiếu {file}. Hãy tạo package bằng deploy/installer/build-package.ps1.");
        }
    }

    private static string? SelectInstallDirectory()
    {
        var defaultDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IceBot");

        using var dialog = new FolderBrowserDialog
        {
            Description = "Chọn chính xác thư mục sẽ cài IceBot",
            SelectedPath = defaultDirectory,
            ShowNewFolderButton = true,
            UseDescriptionForTitle = true
        };

        Console.WriteLine("Chọn thư mục cài đặt trong cửa sổ vừa mở...");
        return dialog.ShowDialog() == DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }

    private static void EnsureNetFramework(string setupDirectory)
    {
        if (GetNetFrameworkRelease() >= NetFramework472Release)
        {
            Console.WriteLine("      Đã có .NET Framework tương thích.");
            return;
        }

        var prerequisites = Path.Combine(setupDirectory, "prerequisites");
        var installer = Directory.Exists(prerequisites)
            ? Directory.GetFiles(prerequisites, "ndp*.exe").OrderBy(path => path).FirstOrDefault()
            : null;

        if (installer == null)
            throw new InvalidOperationException(
                "Máy chưa có .NET Framework 4.7.2+. Package cài đặt thiếu offline installer ndp*.exe trong prerequisites.");

        Run(installer, "/q /norestart", "cài .NET Framework");
        if (GetNetFrameworkRelease() < NetFramework472Release)
            throw new InvalidOperationException(".NET Framework yêu cầu khởi động lại Windows. Hãy restart rồi chạy Setup.exe lại.");
    }

    private static int GetNetFrameworkRelease()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
        return key?.GetValue("Release") is int release ? release : 0;
    }

    private static void EnsureNetBird(string setupDirectory)
    {
        if (FindNetBird() != null)
        {
            Console.WriteLine("      NetBird đã được cài đặt.");
            return;
        }

        var prerequisites = Path.Combine(setupDirectory, "prerequisites");
        var offlineInstaller = Directory.Exists(prerequisites)
            ? Directory.GetFiles(prerequisites, "*netbird*.*")
                .Where(path => path.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path)
                .FirstOrDefault()
            : null;

        if (offlineInstaller != null)
        {
            if (offlineInstaller.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                Run("msiexec.exe", $"/i \"{offlineInstaller}\" /qn /norestart", "cài NetBird");
            else
                Run(offlineInstaller, "/S", "cài NetBird");
        }
        else
        {
            if (!CommandExists("winget.exe"))
                throw new InvalidOperationException(
                    "Không tìm thấy NetBird offline installer hoặc winget. Thêm bộ cài NetBird vào prerequisites rồi chạy lại Setup.exe.");

            Run("winget.exe",
                $"install --id {NetBirdPackageId} --exact --silent --accept-package-agreements --accept-source-agreements",
                "cài NetBird qua winget");
        }

        if (FindNetBird() == null)
            throw new InvalidOperationException("Đã chạy bộ cài NetBird nhưng chưa tìm thấy netbird.exe. Hãy restart Windows rồi chạy Setup.exe lại.");
    }

    private static string? FindNetBird()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var knownPath = Path.Combine(programFiles, "Netbird", "netbird.exe");
        if (File.Exists(knownPath)) return knownPath;
        return CommandExists("netbird.exe") ? "netbird.exe" : null;
    }

    private static bool CommandExists(string command)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = command,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(info);
            process?.WaitForExit(10_000);
            return process is { HasExited: true, ExitCode: 0 };
        }
        catch
        {
            return false;
        }
    }

    private static void CopyPayload(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        CopyDirectory(source, destination, string.Empty);
    }

    private static void CopyDirectory(string source, string destination, string relativePath)
    {
        var mutableRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "config", "certificates", "data", "drivers", "workflow"
        };

        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);

        foreach (var directory in Directory.GetDirectories(source))
        {
            var name = Path.GetFileName(directory);
            var childRelative = string.IsNullOrEmpty(relativePath) ? name : Path.Combine(relativePath, name);
            if (string.IsNullOrEmpty(relativePath) && mutableRoots.Contains(name)) continue;
            CopyDirectory(directory, Path.Combine(destination, name), childRelative);
        }
    }

    private static void CreateRuntimeDirectories(string installDirectory)
    {
        foreach (var name in new[] { "config", "certificates", "workflow", "test-workflow", "data", "data/order-inbox" })
            Directory.CreateDirectory(Path.Combine(installDirectory, name));
    }

    private static void SetRuntimePermissions(string installDirectory)
    {
        // Application binaries remain protected by Program Files. Only site-local paths need
        // Modify permission so IceBot can persist configuration, certificates and jobs.
        // Grant only the account running Setup, not every local Windows user.
        var userSid = WindowsIdentity.GetCurrent().User?.Value
            ?? throw new InvalidOperationException("Không xác định được tài khoản Windows đang cài đặt.");
        foreach (var name in new[] { "config", "certificates", "workflow", "test-workflow", "data" })
        {
            var path = Path.Combine(installDirectory, name);
            Run("icacls.exe", $"\"{path}\" /grant *{userSid}:(OI)(CI)M", $"cấp quyền thư mục {name}");
        }
    }

    private static void CreateSharedDriverDirectory()
    {
        var commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(commonData))
            throw new InvalidOperationException("Không xác định được thư mục ProgramData của Windows.");

        var path = Path.Combine(commonData, "IceBot", "drivers");
        Directory.CreateDirectory(path);

        var userSid = WindowsIdentity.GetCurrent().User?.Value
            ?? throw new InvalidOperationException("Không xác định được tài khoản Windows đang cài đặt.");
        Run("icacls.exe", $"\"{path}\" /grant *{userSid}:(OI)(CI)M", "cấp quyền thư mục driver dùng chung");
    }

    private static void CreateShortcuts(string installDirectory)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        var startMenu = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "IceBot");
        Directory.CreateDirectory(startMenu);

        CreateShortcut(Path.Combine(desktop, "IceBot.lnk"), Path.Combine(installDirectory, "IceBot.exe"), installDirectory);
        CreateShortcut(Path.Combine(desktop, "Init IceBot.lnk"), Path.Combine(installDirectory, "InitIceBot.exe"), installDirectory);
        CreateShortcut(Path.Combine(startMenu, "IceBot.lnk"), Path.Combine(installDirectory, "IceBot.exe"), installDirectory);
        CreateShortcut(Path.Combine(startMenu, "Init IceBot.lnk"), Path.Combine(installDirectory, "InitIceBot.exe"), installDirectory);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Windows Script Host không khả dụng để tạo shortcut.");
        var shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("Không thể khởi tạo Windows Script Host.");

        try
        {
            var shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath })
                ?? throw new InvalidOperationException($"Không thể tạo shortcut {shortcutPath}.");
            var shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDirectory });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            if (System.Runtime.InteropServices.Marshal.IsComObject(shell))
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }

    private static void Run(string fileName, string arguments, string operation)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Không thể bắt đầu {operation}.");
        process.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine($"      {e.Data}"); };
        process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.Error.WriteLine($"      {e.Data}"); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Không thể {operation} (exit code {process.ExitCode}).");
    }

    private static string? GetArgument(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                return args[index + 1];
        return null;
    }

    private static void PrintHeader()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("  ICEBOT SETUP | CÀI ĐẶT MÔI TRƯỜNG");
        Console.WriteLine("========================================");
    }

    private static void Pause()
    {
        if (Console.IsInputRedirected) return;
        Console.WriteLine();
        Console.WriteLine("Nhấn ENTER để đóng...");
        Console.ReadLine();
    }
}
