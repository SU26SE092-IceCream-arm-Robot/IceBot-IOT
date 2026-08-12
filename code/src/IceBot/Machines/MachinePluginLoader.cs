using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace IceBot.Machines
{
    internal sealed class MachinePluginManifest
    {
        public int SchemaVersion { get; set; }
        public string MachineType { get; set; } = string.Empty;
        public string Assembly { get; set; } = string.Empty;
        public string EntryType { get; set; } = string.Empty;
        public string DriverVersion { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
    }

    internal sealed class MachinePluginLoadResult
    {
        public IReadOnlyList<IMachineModule> Modules { get; set; } = Array.Empty<IMachineModule>();
        public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    }

    internal static class MachinePluginLoader
    {
        public static MachinePluginLoadResult Load(string driversDirectory)
        {
            var modules = new List<IMachineModule>();
            var errors = new List<string>();
            if (!Directory.Exists(driversDirectory))
                return new MachinePluginLoadResult { Modules = modules, Errors = errors };

            foreach (var manifestPath in Directory.GetFiles(driversDirectory, "driver.json", SearchOption.AllDirectories))
            {
                try { modules.Add(LoadOne(manifestPath)); }
                catch (Exception ex) { errors.Add($"{manifestPath}: {ex.Message}"); }
            }
            return new MachinePluginLoadResult { Modules = modules, Errors = errors };
        }

        internal static IMachineModule LoadOne(string manifestPath)
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<MachinePluginManifest>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("driver.json rong.");
            ValidateManifest(manifest);

            var directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
            var assemblyPath = Path.GetFullPath(Path.Combine(directory, manifest.Assembly));
            if (!assemblyPath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Duong dan DLL vuot khoi thu muc plugin.");
            if (!File.Exists(assemblyPath)) throw new FileNotFoundException("Khong tim thay DLL driver.", assemblyPath);
            if (!string.Equals(ComputeSha256(assemblyPath), manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("SHA-256 cua DLL khong khop driver.json.");

            // Load verified bytes instead of LoadFrom so Windows does not keep the package DLL
            // locked for the whole Edge process. A restart is still required to activate an
            // updated package, but deployment can replace the files safely beforehand.
            var assembly = System.Reflection.Assembly.Load(File.ReadAllBytes(assemblyPath));
            var type = assembly.GetType(manifest.EntryType, throwOnError: false, ignoreCase: false)
                ?? throw new InvalidDataException("Khong tim thay entryType trong DLL.");
            if (!typeof(IMachineModule).IsAssignableFrom(type) || type.IsAbstract || !type.IsPublic)
                throw new InvalidDataException("entryType phai la public class trien khai IMachineModule.");
            if (type.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidDataException("Driver phai co constructor public khong tham so.");

            var module = (IMachineModule)Activator.CreateInstance(type)!;
            ValidateModule(module);
            if (!string.Equals(module.MachineType, manifest.MachineType, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("machineType trong DLL khong khop driver.json.");
            return module;
        }

        internal static void ValidateManifest(MachinePluginManifest manifest)
        {
            if (manifest.SchemaVersion != 1) throw new InvalidDataException("schemaVersion plugin khong duoc ho tro.");
            if (string.IsNullOrWhiteSpace(manifest.MachineType) || string.IsNullOrWhiteSpace(manifest.Assembly) ||
                string.IsNullOrWhiteSpace(manifest.EntryType) || string.IsNullOrWhiteSpace(manifest.DriverVersion))
                throw new InvalidDataException("driver.json thieu truong bat buoc.");
            if (Path.GetFileName(manifest.Assembly) != manifest.Assembly || !manifest.Assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("assembly phai la ten file DLL trong thu muc plugin.");
            if (manifest.Sha256.Length != 64 || !manifest.Sha256.All(Uri.IsHexDigit))
                throw new InvalidDataException("sha256 phai gom 64 ky tu hex.");
        }

        internal static void ValidateModule(IMachineModule module)
        {
            if (string.IsNullOrWhiteSpace(module.MachineType) || string.IsNullOrWhiteSpace(module.DisplayName))
                throw new InvalidDataException("Driver tra ve dinh danh rong.");
            if (module.MachineType.IndexOfAny(new[] { ':', ',' }) >= 0)
                throw new InvalidDataException("MachineType khong duoc chua ':' hoac ','.");
            if (module.StepNames == null || module.StepNames.Count == 0 || module.StepNames.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("Driver phai khai bao it nhat mot StepName.");
        }

        internal static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
