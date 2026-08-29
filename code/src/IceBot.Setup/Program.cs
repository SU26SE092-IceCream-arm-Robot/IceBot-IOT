using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;

namespace IceBot.Setup;

internal static class Program
{
    private const int NetFramework472Release = 461808;
    private const string NetBirdPackageId = "Netbird.Netbird";
    private const string EmbeddedBundleResource = "IceBot.Setup.install-bundle.zip";

    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Title = "IceBot Setup";
        PrintHeader();

        string? temporaryBundleDirectory = null;
        try
        {
            if (!OperatingSystem.IsWindows())
                throw new InvalidOperationException("Setup chỉ hỗ trợ Windows.");

            var sourceArgument = GetArgument(args, "--source");
            string bundleDirectory;
            string payloadDirectory;
            if (string.IsNullOrWhiteSpace(sourceArgument))
            {
                temporaryBundleDirectory = ExtractEmbeddedBundle();
                bundleDirectory = temporaryBundleDirectory;
                payloadDirectory = Path.Combine(bundleDirectory, "payload");
            }
            else
            {
                payloadDirectory = Path.GetFullPath(sourceArgument);
                bundleDirectory = Directory.GetParent(payloadDirectory)?.FullName
                    ?? throw new InvalidOperationException("Không xác định được thư mục bundle từ --source.");
            }

            ValidateBundle(payloadDirectory, bundleDirectory);
            if (HasArgument(args, "--validate-only"))
            {
                Console.WriteLine("[OK] Bundle hợp lệ: Fairino robot3.7.8, runtime và hai driver đã được xác minh.");
                return 0;
            }

            var elevatedExitCode = RelaunchElevatedIfRequired(args);
            if (elevatedExitCode.HasValue)
                return elevatedExitCode.Value;

            var installDirectory = GetArgument(args, "--install-dir");
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                installDirectory = SelectInstallDirectory();
                if (installDirectory == null)
                {
                    Console.WriteLine("[CANCELLED] Đã hủy cài đặt. Không có file nào được thay đổi.");
                    Pause();
                    return 0;
                }
            }

            installDirectory = Path.GetFullPath(installDirectory);
            EnsureRuntimeProcessesStopped();

            Console.WriteLine("[1/5] Kiểm tra .NET Framework 4.7.2+");
            EnsureNetFramework(bundleDirectory);

            Console.WriteLine("[2/5] Cài đặt NetBird");
            EnsureNetBird(bundleDirectory);

            Console.WriteLine($"[3/5] Cài IceBot vào {installDirectory}");
            CopyPayload(payloadDirectory, installDirectory);

            Console.WriteLine("[4/5] Tạo dữ liệu và cài driver máy ngoại vi");
            CreateRuntimeDirectories(installDirectory);
            SetRuntimePermissions(installDirectory);
            CreateSharedDriverDirectory();
            InstallBundledDrivers(bundleDirectory);

            Console.WriteLine("[5/5] Tạo shortcut");
            CreateShortcuts(installDirectory);

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("[OK] CÀI ĐẶT ICEBOT HOÀN TẤT");
            Console.WriteLine("Tiếp theo: chạy InitIceBot.exe để khởi tạo Edge.");
            Console.WriteLine("Sau đó chạy IceBot.exe để vận hành bán hàng.");
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
        finally
        {
            if (temporaryBundleDirectory != null)
            {
                try { Directory.Delete(temporaryBundleDirectory, true); }
                catch { }
            }
        }
    }

    private static string ExtractEmbeddedBundle()
    {
        using var bundle = Assembly.GetExecutingAssembly().GetManifestResourceStream(EmbeddedBundleResource)
            ?? throw new InvalidOperationException(
                "IceBot-Setup.exe không chứa payload. Hãy tạo lại bằng deploy/installer/build-package.ps1.");

        var destination = Path.Combine(Path.GetTempPath(), "IceBot-Setup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(destination);
        using var archive = new ZipArchive(bundle, ZipArchiveMode.Read);
        archive.ExtractToDirectory(destination);
        return destination;
    }

    private static void ValidateBundle(string payloadDirectory, string bundleDirectory)
    {
        if (!Directory.Exists(payloadDirectory))
            throw new DirectoryNotFoundException($"Không tìm thấy payload: {payloadDirectory}");

        foreach (var file in new[]
        {
            "IceBot.exe",
            "InitIceBot.exe",
            "libfairino.dll",
            "CookComputing.XmlRpcV2.dll",
            "IceBot.Driver.Abstractions.dll"
        })
        {
            if (!File.Exists(Path.Combine(payloadDirectory, file)))
                throw new FileNotFoundException($"Payload thiếu {file}.");
        }

        foreach (var mutableRoot in new[] { "config", "certificates", "data", "drivers", "workflow" })
        {
            if (Directory.Exists(Path.Combine(payloadDirectory, mutableRoot)))
                throw new InvalidOperationException($"Payload khong duoc chua du lieu cuc bo: {mutableRoot}.");
        }

        var manifestPath = Path.Combine(bundleDirectory, "installer-manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Bundle thiếu installer-manifest.json.");

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        var sdk = root.GetProperty("fairinoSdk").GetString();
        var expectedHash = root.GetProperty("libfairinoSha256").GetString();
        if (!string.Equals(sdk, "robot3.7.8", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Fairino SDK không tương thích: {sdk ?? "không xác định"}.");

        ValidateHash(Path.Combine(payloadDirectory, "libfairino.dll"), expectedHash, "libfairino.dll");
        ValidateDriverPackage(bundleDirectory, "CupDropping", "bt_cup_l90");
        ValidateDriverPackage(bundleDirectory, "IceCream", "ice_cream");
    }

    private static void ValidateDriverPackage(string bundleDirectory, string packageName, string expectedMachineType)
    {
        var packageDirectory = Path.Combine(bundleDirectory, "drivers", packageName);
        var manifestPath = Path.Combine(packageDirectory, "driver.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Bundle thiếu driver {packageName}.");

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        var machineType = root.GetProperty("machineType").GetString();
        var assemblyName = root.GetProperty("assembly").GetString();
        var expectedHash = root.GetProperty("sha256").GetString();
        if (!string.Equals(machineType, expectedMachineType, StringComparison.Ordinal))
            throw new InvalidOperationException($"Driver {packageName} có machineType không đúng: {machineType}.");
        if (string.IsNullOrWhiteSpace(assemblyName) || Path.GetFileName(assemblyName) != assemblyName)
            throw new InvalidOperationException($"Driver {packageName} có tên assembly không hợp lệ.");

        ValidateHash(Path.Combine(packageDirectory, assemblyName), expectedHash, $"driver {packageName}");
    }

    private static void ValidateHash(string filePath, string? expectedHash, string label)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Thiếu {label}: {filePath}");
        if (string.IsNullOrWhiteSpace(expectedHash))
            throw new InvalidOperationException($"Manifest thiếu SHA-256 của {label}.");

        var actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(filePath)));
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{label} không khớp SHA-256 trong manifest.");
    }

    private static string? SelectInstallDirectory()
    {
        var defaultDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IceBot");
        using var dialog = new FolderBrowserDialog
        {
            Description = "Chọn thư mục cài IceBot",
            SelectedPath = defaultDirectory,
            ShowNewFolderButton = true,
            UseDescriptionForTitle = true
        };
        Console.WriteLine("Chọn thư mục cài đặt trong cửa sổ vừa mở...");
        return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
    }

    private static void EnsureNetFramework(string bundleDirectory)
    {
        if (GetNetFrameworkRelease() >= NetFramework472Release)
        {
            Console.WriteLine("      Đã có .NET Framework tương thích.");
            return;
        }

        var prerequisites = Path.Combine(bundleDirectory, "prerequisites");
        var installer = Directory.Exists(prerequisites)
            ? Directory.GetFiles(prerequisites, "ndp*.exe").OrderBy(path => path).FirstOrDefault()
            : null;
        if (installer == null)
            throw new InvalidOperationException(
                "Máy chưa có .NET Framework 4.7.2+ và installer không chứa bộ cài offline ndp*.exe.");

        Run(installer, "/q /norestart", "cài .NET Framework");
        if (GetNetFrameworkRelease() < NetFramework472Release)
            throw new InvalidOperationException("Hãy restart Windows rồi chạy IceBot-Setup.exe lại.");
    }

    private static int GetNetFrameworkRelease()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
        return key?.GetValue("Release") is int release ? release : 0;
    }

    private static void EnsureNetBird(string bundleDirectory)
    {
        if (FindNetBird() != null)
        {
            Console.WriteLine("      NetBird đã được cài đặt.");
            return;
        }

        var prerequisites = Path.Combine(bundleDirectory, "prerequisites");
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
                throw new InvalidOperationException("Không có NetBird offline installer hoặc winget.");
            Run("winget.exe",
                $"install --id {NetBirdPackageId} --exact --silent --accept-package-agreements --accept-source-agreements",
                "cài NetBird qua winget");
        }

        if (FindNetBird() == null)
            throw new InvalidOperationException("Đã cài NetBird nhưng chưa tìm thấy netbird.exe.");
    }

    private static string? FindNetBird()
    {
        var knownPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Netbird", "netbird.exe");
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
        catch { return false; }
    }

    private static void EnsureRuntimeProcessesStopped()
    {
        var running = new[] { "IceBot", "InitIceBot" }
            .SelectMany(Process.GetProcessesByName)
            .Where(process => !process.HasExited)
            .Select(process => $"{process.ProcessName} (PID {process.Id})")
            .ToArray();
        if (running.Length > 0)
            throw new InvalidOperationException(
                "Hay dong IceBot/InitIceBot truoc khi cai dat hoac nang cap: " + string.Join(", ", running));
    }
    private static void CopyPayload(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        CopyDirectory(source, destination, string.Empty, preserveMutableRoots: true);
    }

    private static void CopyDirectory(string source, string destination, string relativePath, bool preserveMutableRoots)
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
            if (preserveMutableRoots && string.IsNullOrEmpty(relativePath) && mutableRoots.Contains(name))
                continue;
            var childRelative = string.IsNullOrEmpty(relativePath) ? name : Path.Combine(relativePath, name);
            CopyDirectory(directory, Path.Combine(destination, name), childRelative, preserveMutableRoots);
        }
    }

    private static void CreateRuntimeDirectories(string installDirectory)
    {
        foreach (var name in new[] { "config", "certificates", "workflow", "test-workflow", "data", "data/order-inbox" })
            Directory.CreateDirectory(Path.Combine(installDirectory, name));
    }

    private static void SetRuntimePermissions(string installDirectory)
    {
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
        var path = GetSharedDriverDirectory();
        Directory.CreateDirectory(path);
        var userSid = WindowsIdentity.GetCurrent().User?.Value
            ?? throw new InvalidOperationException("Không xác định được tài khoản Windows đang cài đặt.");
        Run("icacls.exe", $"\"{path}\" /grant *{userSid}:(OI)(CI)M", "cấp quyền thư mục driver dùng chung");
    }

    private static string GetSharedDriverDirectory()
    {
        var commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(commonData))
            throw new InvalidOperationException("Không xác định được ProgramData.");
        return Path.Combine(commonData, "IceBot", "drivers");
    }

    private static void InstallBundledDrivers(string bundleDirectory)
    {
        var source = Path.Combine(bundleDirectory, "drivers");
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException("Bundle thieu thu muc drivers.");

        var destination = GetSharedDriverDirectory();
        foreach (var packageDirectory in Directory.GetDirectories(source))
        {
            var manifestPath = Path.Combine(packageDirectory, "driver.json");
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var machineType = document.RootElement.GetProperty("machineType").GetString();
            if (string.IsNullOrWhiteSpace(machineType) || Path.GetFileName(machineType) != machineType)
                throw new InvalidOperationException("Driver bundle co machineType khong hop le.");

            var canonicalDirectory = Path.Combine(destination, machineType);
            CopyDirectory(packageDirectory, canonicalDirectory, string.Empty, preserveMutableRoots: false);

            foreach (var existingDirectory in Directory.GetDirectories(destination))
            {
                if (string.Equals(existingDirectory, canonicalDirectory, StringComparison.OrdinalIgnoreCase))
                    continue;
                var existingManifest = Path.Combine(existingDirectory, "driver.json");
                if (!File.Exists(existingManifest)) continue;
                try
                {
                    using var existingDocument = JsonDocument.Parse(File.ReadAllText(existingManifest));
                    var existingMachineType = existingDocument.RootElement.GetProperty("machineType").GetString();
                    if (string.Equals(existingMachineType, machineType, StringComparison.OrdinalIgnoreCase))
                        Directory.Delete(existingDirectory, true);
                }
                catch (JsonException)
                {
                    // Invalid third-party packages are left untouched for the runtime diagnostics.
                }
            }
        }
    }
    private static void CreateShortcuts(string installDirectory)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "IceBot");
        Directory.CreateDirectory(startMenu);
        CreateShortcut(Path.Combine(desktop, "IceBot.lnk"), Path.Combine(installDirectory, "IceBot.exe"), installDirectory);
        CreateShortcut(Path.Combine(desktop, "Init IceBot.lnk"), Path.Combine(installDirectory, "InitIceBot.exe"), installDirectory);
        CreateShortcut(Path.Combine(startMenu, "IceBot.lnk"), Path.Combine(installDirectory, "IceBot.exe"), installDirectory);
        CreateShortcut(Path.Combine(startMenu, "Init IceBot.lnk"), Path.Combine(installDirectory, "InitIceBot.exe"), installDirectory);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Windows Script Host không khả dụng.");
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

    private static int? RelaunchElevatedIfRequired(string[] args)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            return null;

        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Không xác định được đường dẫn IceBot-Setup.exe.");
        var info = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = true,
            Verb = "runas"
        };
        foreach (var argument in args)
            info.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(info)
                ?? throw new InvalidOperationException("Không thể yêu cầu quyền Administrator.");
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new InvalidOperationException("Người dùng đã từ chối quyền Administrator.");
        }
    }

    private static string? GetArgument(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                return args[index + 1];
        return null;
    }

    private static bool HasArgument(string[] args, string name) =>
        args.Any(argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));

    private static void PrintHeader()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("  ICEBOT SETUP | CÀI ĐẶT HỆ THỐNG");
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