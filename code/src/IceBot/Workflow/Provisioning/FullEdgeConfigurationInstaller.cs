using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using IceBot.Api;
using IceBot.Config;

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

                var payload = EdgeDeploymentApi.ParseFullEdgeDeployment(command);
                var bundle = payload.FullEdgeBundle!;
                if (bundle.ContentLengthBytes <= 0 || bundle.ContentLengthBytes > MaximumBundleBytes)
                    throw new InvalidDataException("Bundle size is outside the allowed Full Edge limit.");
                if (bundle.ArtifactCount != payload.Artifacts.Count)
                    throw new InvalidDataException("Bundle artifact count does not match the deployment descriptor.");
                if (payload.Artifacts.Any(item => item.ContentLengthBytes <= 0) ||
                    payload.Artifacts.Sum(item => item.ContentLengthBytes) > MaximumBundleBytes)
                    throw new InvalidDataException("Deployment artifact content exceeds the allowed Full Edge limit.");

                var bytes = EdgeDeploymentApi.DownloadBundle(bundle);
                VerifyChecksum(bytes, bundle.Checksum, "bundle");
                var saved = InstallVerifiedBundle(bytes, payload.Artifacts, AppConfig.GetWorkflowDirectory());

                var settings = SiteConfigStore.Load();
                settings.ActiveConfigurationDeploymentId = payload.DeploymentId;
                settings.ActiveConfigurationReleaseId = payload.ConfigurationReleaseId;
                settings.ActiveConfigurationReleaseChecksum = payload.ReleaseChecksum;
                settings.ProvisionedSteps = saved.Select(Path.GetFileNameWithoutExtension).ToList();
                SiteConfigStore.Save(settings);

                EdgeDeploymentApi.AcknowledgeAccepted(command.CommandId);
                Report(command.CommandId, payload, "Installed");
                Report(command.CommandId, payload, "Active");

                return new ProvisionResult
                {
                    Success = true,
                    Message = $"Da tai, xac minh va kich hoat {saved.Count} file Lua tu deployment {payload.DeploymentId:D}.",
                    SavedFiles = saved
                };
            }
            catch (Exception ex)
            {
                return new ProvisionResult { Success = false, Message = ex.Message };
            }
        }

        private static void Report(Guid commandId, FullEdgeDeploymentPayload payload, string status)
        {
            EdgeDeploymentApi.ReportDeployment(
                commandId, payload, status, SiteConfigStore.NextExecutionReportSequence());
        }

        internal static IReadOnlyList<string> InstallVerifiedBundle(
            byte[] bundleBytes,
            IReadOnlyCollection<DeploymentArtifactData> artifacts,
            string workflowDir)
        {
            Directory.CreateDirectory(workflowDir);
            var expected = artifacts.ToDictionary(item => item.RobotArtifactId);
            var stagingDir = Path.Combine(Path.GetTempPath(), "icebot-lua-" + Guid.NewGuid().ToString("N"));
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
                    var destination = Path.Combine(workflowDir, fileName);
                    var temporary = destination + ".new";
                    File.Copy(source, temporary, true);
                    if (File.Exists(destination)) File.Replace(temporary, destination, null); else File.Move(temporary, destination);
                    saved.Add(fileName);
                }
                File.Copy(Path.Combine(stagingDir, "release-content-manifest.json"), Path.Combine(workflowDir, "release-content-manifest.json"), true);
                return saved;
            }
            finally
            {
                if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true);
            }
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
