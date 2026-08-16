using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using IceBot.Api;
using IceBot.Config;
using IceBot.Robot.Hardware;

namespace IceBot.Workflow
{
    internal static class FullEdgeConfigurationInstaller
    {
        private const long MaximumBundleBytes = 100L * 1024 * 1024;

        public static ProvisionResult PullAndInstall()
        {
            try
            {
                var pull = EdgeDeploymentApi.PullCommands();
                var command = pull.Commands.FirstOrDefault(item =>
                    string.Equals(item.CommandType, "DeployConfiguration", StringComparison.OrdinalIgnoreCase));
                if (command == null)
                    return new ProvisionResult { Success = true, Message = "BE khong co deployment Lua dang cho." };

                return Install(command);
            }
            catch (Exception ex)
            {
                return new ProvisionResult { Success = false, Retryable = IsRetryable(ex), Message = ex.Message };
            }
        }

        public static ProvisionResult Install(EdgeCommandData command)
        {
            try
            {
                var payload = EdgeDeploymentApi.ParseFullEdgeDeployment(command);
                var currentSettings = SiteConfigStore.Load();
                if (currentSettings.ActiveConfigurationDeploymentId == payload.DeploymentId)
                {
                    DeploymentReportOutbox.Enqueue(command.CommandId, payload, "Installed");
                    DeploymentReportOutbox.Enqueue(command.CommandId, payload, "Active");
                    EdgeDeploymentApi.AcknowledgeAccepted(command.CommandId);
                    DeploymentReportOutbox.Flush();
                    return new ProvisionResult { Success = true, Message = $"Deployment {payload.DeploymentId:D} da duoc kich hoat truoc do." };
                }

                var bundle = payload.FullEdgeBundle!;
                ValidateDeclaredTargets(payload.Artifacts, new ConfiguredRobotDeviceDiscovery().Discover(SiteConfigStore.Load()));
                if (bundle.ContentLengthBytes <= 0 || bundle.ContentLengthBytes > MaximumBundleBytes)
                    throw new InvalidDataException("Bundle size is outside the allowed Full Edge limit.");
                if (bundle.ArtifactCount != payload.Artifacts.Count)
                    throw new InvalidDataException("Bundle artifact count does not match the deployment descriptor.");
                if (payload.Artifacts.Any(item => item.ContentLengthBytes <= 0) ||
                    payload.Artifacts.Sum(item => item.ContentLengthBytes) > MaximumBundleBytes)
                    throw new InvalidDataException("Deployment artifact content exceeds the allowed Full Edge limit.");

                var bytes = EdgeDeploymentApi.DownloadBundle(bundle);
                VerifyChecksum(bytes, bundle.Checksum, "bundle");
                var installed = InstallVerifiedBundle(bytes, payload.Artifacts, payload.DeploymentId);

                var settings = SiteConfigStore.Load();
                settings.ActiveConfigurationDeploymentId = payload.DeploymentId;
                settings.ActiveConfigurationReleaseId = payload.ConfigurationReleaseId;
                settings.ActiveConfigurationReleaseChecksum = payload.ReleaseChecksum;
                settings.ActiveWorkflowDirectory = installed.DirectoryPath;
                settings.ProvisionedSteps = installed.SavedFiles.Select(Path.GetFileNameWithoutExtension).ToList();
                SiteConfigStore.Save(settings);

                DeploymentReportOutbox.Enqueue(command.CommandId, payload, "Installed");
                DeploymentReportOutbox.Enqueue(command.CommandId, payload, "Active");
                EdgeDeploymentApi.AcknowledgeAccepted(command.CommandId);
                DeploymentReportOutbox.Flush();

                return new ProvisionResult
                {
                    Success = true,
                    Message = $"Da tai, xac minh va kich hoat {installed.SavedFiles.Count} file Lua tu deployment {payload.DeploymentId:D}.",
                    SavedFiles = installed.SavedFiles
                };
            }
            catch (Exception ex)
            {
                return new ProvisionResult { Success = false, Retryable = IsRetryable(ex), Message = ex.Message };
            }
        }

        private static bool IsRetryable(Exception exception)
        {
            return exception is System.Net.Http.HttpRequestException ||
                exception is System.Threading.Tasks.TaskCanceledException ||
                exception is IOException;
        }

        private static InstalledBundle InstallVerifiedBundle(
            byte[] bundleBytes,
            IReadOnlyCollection<DeploymentArtifactData> artifacts,
            Guid deploymentId)
        {
            var workflowRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workflow");
            var releasesDirectory = Path.Combine(workflowRoot, "releases");
            Directory.CreateDirectory(releasesDirectory);
            var expected = artifacts.ToDictionary(item => item.RobotArtifactId);
            var stagingDir = Path.Combine(releasesDirectory, ".staging-" + Guid.NewGuid().ToString("N"));
            var activeDirectory = Path.Combine(releasesDirectory, deploymentId.ToString("D") + "-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Directory.CreateDirectory(stagingDir);
            try
            {
                using (var stream = new MemoryStream(bundleBytes, false))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, false))
                {
                    var unexpected = archive.Entries.FirstOrDefault(entry =>
                        !string.Equals(entry.FullName, "release-content-manifest.json", StringComparison.Ordinal) &&
                        !(entry.FullName.StartsWith("artifacts/", StringComparison.Ordinal) &&
                          entry.FullName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) &&
                          entry.FullName.IndexOf('/', "artifacts/".Length) < 0));
                    if (unexpected != null) throw new InvalidDataException("Bundle contains an unexpected or unsafe entry.");
                    var entries = archive.Entries.Where(entry => entry.FullName.StartsWith("artifacts/", StringComparison.Ordinal) && entry.FullName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (entries.Length != expected.Count) throw new InvalidDataException("Bundle contains an unexpected number of Lua artifacts.");
                    foreach (var entry in entries)
                    {
                        var leaf = Path.GetFileName(entry.FullName);
                        if (!Guid.TryParse(Path.GetFileNameWithoutExtension(leaf), out var artifactId) || !expected.TryGetValue(artifactId, out var descriptor))
                            throw new InvalidDataException("Bundle contains an unexpected artifact entry.");
                        if (entry.Length != descriptor.ContentLengthBytes) throw new InvalidDataException($"Artifact {artifactId:D} size mismatch.");
                        var target = Path.Combine(stagingDir, leaf);
                        using (var source = entry.Open())
                        using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None)) source.CopyTo(output);
                        VerifyChecksum(File.ReadAllBytes(target), descriptor.ArtifactChecksum, leaf);
                    }

                    var manifest = archive.GetEntry("release-content-manifest.json") ?? throw new InvalidDataException("Bundle manifest is missing.");
                    if (manifest.Length > 10 * 1024 * 1024) throw new InvalidDataException("Bundle manifest exceeds the allowed size.");
                    using (var source = manifest.Open())
                    using (var output = new FileStream(Path.Combine(stagingDir, "release-content-manifest.json"), FileMode.CreateNew)) source.CopyTo(output);
                }

                var saved = new List<string>();
                foreach (var source in Directory.GetFiles(stagingDir, "*.lua"))
                {
                    var fileName = Path.GetFileName(source);
                    saved.Add(fileName);
                }
                Directory.Move(stagingDir, activeDirectory);
                return new InstalledBundle(activeDirectory, saved);
            }
            finally
            {
                if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true);
            }
        }

        private static void ValidateDeclaredTargets(
            IReadOnlyCollection<DeploymentArtifactData> artifacts,
            IReadOnlyCollection<ReportedRobotDevice> devices)
        {
            if (devices.Count == 0)
                throw new InvalidDataException("Edge has no reported hardware profile for deployment compatibility.");

            foreach (var artifact in artifacts)
            {
                if (string.IsNullOrWhiteSpace(artifact.RuntimeTargetCode) ||
                    string.IsNullOrWhiteSpace(artifact.MachineModelCode))
                    throw new InvalidDataException($"Artifact {artifact.RobotArtifactId:D} has no declared runtime target or machine model.");

                if (!devices.Any(device =>
                    string.Equals(device.RuntimeTargetCode, artifact.RuntimeTargetCode, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(device.MachineModelCode, artifact.MachineModelCode, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException($"Artifact {artifact.RobotArtifactId:D} is not declared compatible with this Edge hardware.");
            }
        }

        private sealed class InstalledBundle
        {
            public InstalledBundle(string directoryPath, IReadOnlyList<string> savedFiles)
            {
                DirectoryPath = directoryPath;
                SavedFiles = savedFiles;
            }

            public string DirectoryPath { get; }
            public IReadOnlyList<string> SavedFiles { get; }
        }

        private static void VerifyChecksum(byte[] bytes, string expected, string label)
        {
            if (string.IsNullOrWhiteSpace(expected)) throw new InvalidDataException($"Missing SHA-256 checksum for {label}.");
            using (var sha = SHA256.Create())
            {
                var actual = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"SHA-256 checksum mismatch for {label}.");
            }
        }
    }
}
