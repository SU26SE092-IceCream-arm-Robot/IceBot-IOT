using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace IceBot.Config
{
    internal sealed class EdgeClientCertificateResult
    {
        public bool Success { get; set; }
        public string CertificatePath { get; set; } = string.Empty;
        public string Sha256Fingerprint { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    internal static class EdgeClientCertificateProvisioner
    {
        public static string DefaultCertificatePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "certificates", "icebot-edge-client.pfx");

        public static EdgeClientCertificateResult Ensure(SiteSettings settings)
        {
            try
            {
                var path = string.IsNullOrWhiteSpace(settings.ExecutionClientCertificatePath)
                    ? DefaultCertificatePath
                    : Path.GetFullPath(settings.ExecutionClientCertificatePath);

                if (File.Exists(path))
                {
                    using (var existing = Load(path))
                    {
                        if (!existing.HasPrivateKey)
                            return Fail("File PFX hien tai khong chua private key.");
                        if (DateTime.UtcNow < existing.NotBefore.ToUniversalTime() ||
                            DateTime.UtcNow >= existing.NotAfter.ToUniversalTime())
                            return Fail("Chung chi mTLS hien tai chua co hieu luc hoac da het han.");
                        return Success(path, existing, "Tai su dung chung chi mTLS da co.");
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using (var rsa = RSA.Create(3072))
                {
                    var subject = "CN=IceBot Edge " + EscapeDistinguishedName(settings.KioskCode);
                    var request = new CertificateRequest(
                        subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
                    request.CertificateExtensions.Add(new X509KeyUsageExtension(
                        X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
                    var usages = new OidCollection { new Oid("1.3.6.1.5.5.7.3.2", "Client Authentication") };
                    request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, true));
                    request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

                    using (var certificate = request.CreateSelfSigned(
                        DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(5)))
                    {
                        // Password is optional and environment-only. With no environment value the
                        // generated PFX is passwordless, keeping the first-run flow non-interactive.
                        File.WriteAllBytes(path, certificate.Export(
                            X509ContentType.Pfx, GetPfxPassword()));
                        using (var persisted = Load(path))
                            return Success(path, persisted, "Da tao chung chi client mTLS tren Edge.");
                    }
                }
            }
            catch (Exception ex) when (ex is CryptographicException || ex is IOException || ex is UnauthorizedAccessException)
            {
                return Fail("Khong tao/doc duoc chung chi mTLS: " + ex.Message);
            }
        }

        internal static string GetSha256Fingerprint(X509Certificate2 certificate)
        {
            using (var sha256 = SHA256.Create())
                return BitConverter.ToString(sha256.ComputeHash(certificate.RawData))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static X509Certificate2 Load(string path) => new X509Certificate2(
            path,
            GetPfxPassword(),
            X509KeyStorageFlags.EphemeralKeySet);

        private static string? GetPfxPassword() =>
            string.IsNullOrEmpty(AppConfig.ExecutionClientCertificatePassword)
                ? null
                : AppConfig.ExecutionClientCertificatePassword;

        private static string EscapeDistinguishedName(string value) =>
            (string.IsNullOrWhiteSpace(value) ? "UNKNOWN" : value.Trim())
                .Replace("\\", "\\\\").Replace(",", "\\,").Replace("+", "\\+").Replace("\"", "\\\"");

        private static EdgeClientCertificateResult Success(string path, X509Certificate2 certificate, string message) =>
            new EdgeClientCertificateResult
            {
                Success = true,
                CertificatePath = path,
                Sha256Fingerprint = GetSha256Fingerprint(certificate),
                Message = message
            };

        private static EdgeClientCertificateResult Fail(string message) =>
            new EdgeClientCertificateResult { Success = false, Message = message };
    }
}
