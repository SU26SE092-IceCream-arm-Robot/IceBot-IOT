using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using IceBot.Machines;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class MachinePluginLoaderTests
    {
        [Fact]
        public void DriverDirectory_UsesSharedProgramDataLocation()
        {
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "IceBot",
                "drivers");

            Assert.Equal(expected, MachineDriverDirectory.Resolve());
            Assert.DoesNotContain(AppContext.BaseDirectory, MachineDriverDirectory.Resolve(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ValidateManifest_AcceptsSchemaOneContract()
        {
            MachinePluginLoader.ValidateManifest(new MachinePluginManifest
            {
                SchemaVersion = 1,
                MachineType = "vendor_machine",
                Assembly = "Vendor.Driver.dll",
                EntryType = "Vendor.Driver.Entry",
                DriverVersion = "1.0.0",
                Sha256 = new string('a', 64)
            });
        }

        [Fact]
        public void ValidateManifest_RejectsAssemblyTraversal()
        {
            Assert.Throws<InvalidDataException>(() => MachinePluginLoader.ValidateManifest(new MachinePluginManifest
            {
                SchemaVersion = 1,
                MachineType = "vendor_machine",
                Assembly = "../Vendor.Driver.dll",
                EntryType = "Vendor.Driver.Entry",
                DriverVersion = "1.0.0",
                Sha256 = new string('a', 64)
            }));
        }

        [Fact]
        public void Load_MissingDirectoryIsEmptyAndSafe()
        {
            var result = MachinePluginLoader.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
            Assert.Empty(result.Modules);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void Load_ValidDllPackageCreatesDriver()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-plugin-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var sourceAssembly = typeof(HarnessMachineDriver).Assembly.Location;
                var assemblyPath = Path.Combine(directory, "Harness.Driver.dll");
                File.Copy(sourceAssembly, assemblyPath);
                File.WriteAllText(Path.Combine(directory, "driver.json"), JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    machineType = "harness_machine",
                    assembly = "Harness.Driver.dll",
                    entryType = typeof(HarnessMachineDriver).FullName,
                    driverVersion = "1.0.0",
                    sha256 = MachinePluginLoader.ComputeSha256(assemblyPath)
                }));

                var result = MachinePluginLoader.Load(directory);

                Assert.Empty(result.Errors);
                Assert.Single(result.Modules);
                Assert.Equal("harness_machine", result.Modules[0].MachineType);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void Load_PackagedCupDroppingDriver_IsValidAndLoadable()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !Directory.Exists(Path.Combine(root.FullName, "DRIVER-DLL")))
                root = root.Parent;

            Assert.NotNull(root);
            var package = Path.Combine(root!.FullName, "DRIVER-DLL", "CupDropping");
            var result = MachinePluginLoader.Load(package);

            Assert.Empty(result.Errors);
            var driver = Assert.Single(result.Modules);
            Assert.Equal("cup_dropping", driver.MachineType);
            Assert.Contains("cup_s", driver.StepNames);
            Assert.IsAssignableFrom<IMachineTrigger>(driver);
        }

        [Fact]
        public void Load_RejectsTamperedDllBySha256()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-plugin-tamper-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                File.Copy(typeof(HarnessMachineDriver).Assembly.Location, Path.Combine(directory, "Driver.dll"));
                File.WriteAllText(Path.Combine(directory, "driver.json"), JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    machineType = "harness_machine",
                    assembly = "Driver.dll",
                    entryType = typeof(HarnessMachineDriver).FullName,
                    driverVersion = "1.0.0",
                    sha256 = new string('0', 64)
                }));

                var result = MachinePluginLoader.Load(directory);

                Assert.Empty(result.Modules);
                Assert.Contains(result.Errors, error => error.Contains("SHA-256"));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Theory]
        [InlineData(2, "machine", "Driver.dll", "Type", "1", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        [InlineData(1, "machine", "Driver.exe", "Type", "1", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
        [InlineData(1, "machine", "Driver.dll", "Type", "1", "not-a-checksum")]
        public void ValidateManifest_RejectsUnsupportedOrUnsafeContracts(int schema, string machine,
            string assembly, string entryType, string version, string sha)
        {
            Assert.Throws<InvalidDataException>(() => MachinePluginLoader.ValidateManifest(new MachinePluginManifest
            {
                SchemaVersion = schema,
                MachineType = machine,
                Assembly = assembly,
                EntryType = entryType,
                DriverVersion = version,
                Sha256 = sha
            }));
        }

        [Fact]
        public void ValidateModule_RejectsMachineTypeUnsafeForConfigSerialization()
        {
            Assert.Throws<InvalidDataException>(() => MachinePluginLoader.ValidateModule(new UnsafeMachineDriver()));
        }
    }

    public sealed class HarnessMachineDriver : IMachineModule
    {
        public string MachineType => "harness_machine";
        public string DisplayName => "Harness machine";
        public IReadOnlyCollection<string> StepNames { get; } = new[] { "harness_step" };
    }

    public sealed class UnsafeMachineDriver : IMachineModule
    {
        public string MachineType => "unsafe,type";
        public string DisplayName => "Unsafe";
        public IReadOnlyCollection<string> StepNames { get; } = new[] { "step" };
    }
}
