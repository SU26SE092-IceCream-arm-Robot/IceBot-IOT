using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using IceBot.Config;

namespace IceBot.Api
{
    internal static class EdgeMtlsProbe
    {
        public static bool SendHeartbeat(out string message)
        {
            var settings = SiteConfigStore.Load();
            if (settings.ExecutionEndpointId == Guid.Empty || settings.FullEdgeRuntimeId == Guid.Empty)
            {
                message = "Thieu Execution Endpoint ID hoac Full Edge Runtime ID.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(settings.ExecutionClientCertificatePath) ||
                !File.Exists(settings.ExecutionClientCertificatePath))
            {
                message = "Khong tim thay PFX client mTLS.";
                return false;
            }
            if (!Uri.TryCreate(settings.BeApiUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri) ||
                baseUri.Scheme != Uri.UriSchemeHttps)
            {
                message = "BE_API_URL phai la HTTPS URL hop le.";
                return false;
            }

            try
            {
                using (var certificate = new X509Certificate2(
                    settings.ExecutionClientCertificatePath,
                    AppConfig.ExecutionClientCertificatePassword,
                    X509KeyStorageFlags.EphemeralKeySet))
                using (var handler = new HttpClientHandler())
                {
                    handler.ClientCertificates.Add(certificate);
                    using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) })
                    using (var content = new StringContent(JsonSerializer.Serialize(new
                    {
                        originNodeId = settings.FullEdgeRuntimeId,
                        heartbeatSequence = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        reportedAt = DateTimeOffset.UtcNow,
                        status = 1,
                        robotStatus = "Setup",
                        networkStatus = "Online",
                        appVersion = typeof(EdgeMtlsProbe).Assembly.GetName().Version?.ToString(),
                        pendingSyncEventCount = 0
                    }), Encoding.UTF8, "application/json"))
                    using (var response = client.PostAsync(
                        new Uri(baseUri, $"api/v1/iot/execution-endpoints/{settings.ExecutionEndpointId:D}/heartbeat"),
                        content).GetAwaiter().GetResult())
                    {
                        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        if (!response.IsSuccessStatusCode)
                        {
                            message = $"BE tu choi heartbeat mTLS (HTTP {(int)response.StatusCode}): {body}";
                            return false;
                        }
                        message = "BE da xac thuc chung chi va nhan heartbeat mTLS.";
                        return true;
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is System.Threading.Tasks.TaskCanceledException || ex is System.Security.Cryptography.CryptographicException)
            {
                message = "Kiem tra mTLS that bai: " + ex.Message;
                return false;
            }
        }
    }
}
