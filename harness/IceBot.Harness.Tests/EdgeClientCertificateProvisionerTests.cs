using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
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
                Assert.True(File.Exists(EdgeClientCertificateProvisioner.PasswordSidecarPath(path)));
                Assert.ThrowsAny<CryptographicException>(() =>
                    new X509Certificate2(path, string.Empty, X509KeyStorageFlags.EphemeralKeySet));
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
        public void Ensure_MigratesPasswordlessPfxToDpapiProtectedPassword()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-mtls-migrate-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "edge.pfx");
            Directory.CreateDirectory(directory);
            try
            {
                using (var rsa = RSA.Create(2048))
                {
                    var request = new CertificateRequest("CN=Legacy IceBot", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    using (var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1)))
                        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, string.Empty));
                }

                var result = EdgeClientCertificateProvisioner.Ensure(new SiteSettings
                {
                    KioskCode = "ICE-KIOSK-001",
                    ExecutionClientCertificatePath = path
                });

                Assert.True(result.Success, result.Message);
                Assert.True(File.Exists(EdgeClientCertificateProvisioner.PasswordSidecarPath(path)));
                Assert.ThrowsAny<CryptographicException>(() =>
                    new X509Certificate2(path, string.Empty, X509KeyStorageFlags.EphemeralKeySet));
            }
            finally { Directory.Delete(directory, true); }
        }
        [Fact]
        public async Task LoadForMtls_ProvidesPrivateKeyUsableByWindowsSchannel()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-mtls-schannel-" + Guid.NewGuid().ToString("N"));
            var clientPath = Path.Combine(directory, "client.pfx");
            Directory.CreateDirectory(directory);
            var listener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                var created = EdgeClientCertificateProvisioner.Ensure(new SiteSettings
                {
                    KioskCode = "ICE-KIOSK-001",
                    ExecutionClientCertificatePath = clientPath
                });
                Assert.True(created.Success, created.Message);

                using (var serverCertificate = CreateTlsCertificate("CN=localhost"))
                using (var clientCertificate = EdgeClientCertificateProvisioner.LoadForMtls(clientPath))
                {
                    listener.Start();
                    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    var server = Task.Run(async () =>
                    {
                        using (var accepted = await listener.AcceptTcpClientAsync())
                        using (var tls = new SslStream(accepted.GetStream(), false, (_, __, ___, ____) => true))
                        {
                            tls.AuthenticateAsServer(serverCertificate, true, SslProtocols.Tls12, false);
                            Assert.True(tls.IsMutuallyAuthenticated);
                            Assert.NotNull(tls.RemoteCertificate);
                        }
                    });

                    using (var client = new TcpClient())
                    {
                        await client.ConnectAsync(IPAddress.Loopback, port);
                        using (var tls = new SslStream(client.GetStream(), false, (_, __, ___, ____) => true))
                        {
                            var certificates = new X509CertificateCollection { clientCertificate };
                            tls.AuthenticateAsClient("localhost", certificates, SslProtocols.Tls12, false);
                            Assert.True(tls.IsMutuallyAuthenticated);
                        }
                    }

                    await server;
                }
            }
            finally
            {
                listener.Stop();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static X509Certificate2 CreateTlsCertificate(string subject)
        {
            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using (var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1)))
                    return new X509Certificate2(certificate.Export(X509ContentType.Pfx), string.Empty,
                        X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
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
