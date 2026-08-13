using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using IceBot.Api;
using IceBot.Workflow;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class FullEdgeConfigurationInstallerTests
    {
        [Fact]
        public void InstallVerifiedBundle_InstallsGuidLuaAndManifest()
        {
            var artifactId = Guid.NewGuid();
            var lua = Encoding.UTF8.GetBytes("print('ok')");
            var bundle = BuildBundle(artifactId, lua);
            var workflow = TempDirectory();
            try
            {
                var saved = FullEdgeConfigurationInstaller.InstallVerifiedBundle(bundle,
                    new[] { Descriptor(artifactId, lua) }, workflow);

                Assert.Single(saved);
                Assert.Equal(lua, File.ReadAllBytes(Path.Combine(workflow, artifactId.ToString("D") + ".lua")));
                Assert.True(File.Exists(Path.Combine(workflow, "release-content-manifest.json")));
            }
            finally { Directory.Delete(workflow, true); }
        }

        [Fact]
        public void InstallVerifiedBundle_RejectsArtifactChecksumMismatchWithoutActivatingLua()
        {
            var artifactId = Guid.NewGuid();
            var lua = Encoding.UTF8.GetBytes("print('unsafe')");
            var workflow = TempDirectory();
            try
            {
                var descriptor = Descriptor(artifactId, lua);
                descriptor.ArtifactChecksum = new string('0', 64);

                Assert.Throws<InvalidDataException>(() => FullEdgeConfigurationInstaller.InstallVerifiedBundle(
                    BuildBundle(artifactId, lua), new[] { descriptor }, workflow));
                Assert.Empty(Directory.GetFiles(workflow, "*.lua"));
            }
            finally { Directory.Delete(workflow, true); }
        }

        [Fact]
        public void InstallVerifiedBundle_RejectsUnexpectedArchiveEntry()
        {
            var artifactId = Guid.NewGuid();
            var lua = Encoding.UTF8.GetBytes("print('ok')");
            var workflow = TempDirectory();
            try
            {
                var bytes = BuildBundle(artifactId, lua, "../outside.txt");
                Assert.Throws<InvalidDataException>(() => FullEdgeConfigurationInstaller.InstallVerifiedBundle(
                    bytes, new[] { Descriptor(artifactId, lua) }, workflow));
                Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(workflow)!, "outside.txt")));
            }
            finally { Directory.Delete(workflow, true); }
        }

        private static byte[] BuildBundle(Guid artifactId, byte[] lua, string? extraEntry = null)
        {
            using (var stream = new MemoryStream())
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    Write(archive, "artifacts/" + artifactId.ToString("D") + ".lua", lua);
                    Write(archive, "release-content-manifest.json", Encoding.UTF8.GetBytes("{}"));
                    if (extraEntry != null) Write(archive, extraEntry, Encoding.UTF8.GetBytes("bad"));
                }
                return stream.ToArray();
            }
        }

        private static void Write(ZipArchive archive, string name, byte[] content)
        {
            var entry = archive.CreateEntry(name);
            using (var output = entry.Open()) output.Write(content, 0, content.Length);
        }

        private static DeploymentArtifactData Descriptor(Guid id, byte[] content) => new DeploymentArtifactData
        {
            RobotArtifactId = id,
            ArtifactChecksum = Sha256(content),
            ContentLengthBytes = content.Length
        };

        private static string Sha256(byte[] content)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(content)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string TempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "icebot-deploy-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
