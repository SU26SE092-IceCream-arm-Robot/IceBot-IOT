using System;
using System.IO;
using IceBot.Config;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class EdgeClientCertificateProvisionerTests
    {
        [Fact]
        public void Ensure_CreatesReusableClientPfxAndSha256Fingerprint()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-mtls-test-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "edge.pfx");
            try
            {
                var settings = new SiteSettings
                {
                    KioskCode = "ICE-KIOSK-001",
                    ExecutionClientCertificatePath = path
                };

                var created = EdgeClientCertificateProvisioner.Ensure(settings);
                var reused = EdgeClientCertificateProvisioner.Ensure(settings);

                Assert.True(created.Success, created.Message);
                Assert.True(File.Exists(path));
                Assert.Equal(64, created.Sha256Fingerprint.Length);
                Assert.True(reused.Success, reused.Message);
                Assert.Equal(created.Sha256Fingerprint, reused.Sha256Fingerprint);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
