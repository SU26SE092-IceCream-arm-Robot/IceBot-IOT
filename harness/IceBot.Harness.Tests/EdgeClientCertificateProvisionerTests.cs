using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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

        [Fact]
        public void Ensure_RejectsExistingCertificateWithoutPrivateKey()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-mtls-public-test-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "edge.pfx");
            Directory.CreateDirectory(directory);
            try
            {
                using (var rsa = RSA.Create(2048))
                {
                    var request = new CertificateRequest("CN=Public Only", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    using (var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1)))
                        File.WriteAllBytes(path, certificate.Export(X509ContentType.Cert));
                }

                var result = EdgeClientCertificateProvisioner.Ensure(new SiteSettings { ExecutionClientCertificatePath = path });

                Assert.False(result.Success);
                Assert.Contains("private key", result.Message, StringComparison.OrdinalIgnoreCase);
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
