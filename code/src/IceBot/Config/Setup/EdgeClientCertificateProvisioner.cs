using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

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
                    var existingPassword = ResolveExistingPassword(path);
                    using (var existing = Load(path, existingPassword))
                    {
                        if (!existing.HasPrivateKey)
                            return Fail("File PFX hien tai khong chua private key.");
                        if (DateTime.UtcNow < existing.NotBefore.ToUniversalTime() ||
                            DateTime.UtcNow >= existing.NotAfter.ToUniversalTime())
                            return Fail("Chung chi mTLS hien tai chua co hieu luc hoac da het han.");

                        if (string.IsNullOrEmpty(GetEnvironmentPassword()) &&
                            !File.Exists(PasswordSidecarPath(path)))
                        {
                            var migratedPassword = CreateAndPersistPassword(path);
                            File.WriteAllBytes(path, existing.Export(X509ContentType.Pfx, migratedPassword));
                            using (var migrated = Load(path, migratedPassword))
                                return Success(path, migrated, "Da ma hoa lai va tai su dung chung chi mTLS hien co.");
                        }

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
                        var password = GetOrCreatePassword(path);
                        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
                        using (var persisted = Load(path, password))
                            return Success(path, persisted, "Da tao chung chi client mTLS duoc bao ve tren Edge.");
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

        private static X509Certificate2 Load(string path, string? password) => new X509Certificate2(
            path, password, X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);

        internal static X509Certificate2 LoadForMtls(string path) =>
            Load(path, ResolveExistingPassword(path));

        internal static string PasswordSidecarPath(string pfxPath) => pfxPath + ".password.dpapi";

        private static string? ResolveExistingPassword(string path)
        {
            var environmentPassword = GetEnvironmentPassword();
            if (!string.IsNullOrEmpty(environmentPassword)) return environmentPassword;
            var sidecar = PasswordSidecarPath(path);
            if (!File.Exists(sidecar)) return null;
            var protectedBytes = File.ReadAllBytes(sidecar);
            var clearBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(clearBytes);
        }

        private static string GetOrCreatePassword(string path) =>
            ResolveExistingPassword(path) ?? CreateAndPersistPassword(path);

        private static string CreateAndPersistPassword(string path)
        {
            var random = new byte[32];
            using (var generator = RandomNumberGenerator.Create()) generator.GetBytes(random);
            var password = Convert.ToBase64String(random);
            var protectedBytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(PasswordSidecarPath(path), protectedBytes);
            return password;
        }

        private static string? GetEnvironmentPassword() =>
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
