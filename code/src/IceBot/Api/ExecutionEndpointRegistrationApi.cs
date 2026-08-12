using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IceBot.Config;

namespace IceBot.Api
{
    internal sealed class BackendKiosk
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    internal sealed class BackendExecutionEndpoint
    {
        public Guid Id { get; set; }
        public Guid KioskId { get; set; }
        public string EndpointCode { get; set; } = string.Empty;
        public string ExecutionProfile { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    internal sealed class ExecutionEndpointRegistrationResult
    {
        public bool Success { get; set; }
        public Guid EndpointId { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool Created { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>Operator-authorized management client used only while initializing a new Edge.</summary>
    internal sealed class ExecutionEndpointRegistrationApi
    {
        private readonly HttpClient _http;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public ExecutionEndpointRegistrationApi() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(15) }) { }

        internal ExecutionEndpointRegistrationApi(HttpClient http)
        {
            _http = http;
        }

        public IReadOnlyList<BackendKiosk> ListKiosks(out string error)
        {
            var response = SendWithRefresh(HttpMethod.Get, "api/v1/management/kiosks", null);
            return ParseList<BackendKiosk>(response, "danh sach kiosk", out error);
        }

        public ExecutionEndpointRegistrationResult FindOrCreate(Guid kioskId, string endpointCode)
        {
            if (kioskId == Guid.Empty) return Fail("KioskId khong hop le.");
            if (string.IsNullOrWhiteSpace(endpointCode)) return Fail("EndpointCode khong hop le.");

            var listResponse = SendWithRefresh(
                HttpMethod.Get,
                $"api/v1/management/execution-endpoints?kioskId={kioskId:D}",
                null);
            var endpoints = ParseList<BackendExecutionEndpoint>(listResponse, "danh sach execution endpoint", out var listError);
            if (!string.IsNullOrWhiteSpace(listError)) return Fail(listError);

            var existing = endpoints.FirstOrDefault(endpoint =>
                endpoint.KioskId == kioskId &&
                string.Equals(endpoint.EndpointCode, endpointCode, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                if (!string.Equals(existing.ExecutionProfile, "FullEdge", StringComparison.OrdinalIgnoreCase))
                    return Fail($"Ma {endpointCode} da ton tai nhung khong phai FullEdge.");
                return new ExecutionEndpointRegistrationResult
                {
                    Success = true,
                    EndpointId = existing.Id,
                    Status = existing.Status,
                    Created = false,
                    Message = "Edge da duoc dang ky tren BE; da khoi phuc Execution Endpoint ID."
                };
            }

            var createResponse = SendWithRefresh(
                HttpMethod.Post,
                $"api/v1/management/kiosks/{kioskId:D}/execution-endpoints",
                new { endpointCode, executionProfile = 1 });
            return ParseCreate(createResponse);
        }

        internal static string BuildEndpointCode(string machineName)
        {
            var source = string.IsNullOrWhiteSpace(machineName) ? "UNKNOWN" : machineName.Trim();
            var builder = new StringBuilder("EDGE-");
            foreach (var c in source)
            {
                builder.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? char.ToUpperInvariant(c) : '-');
            }

            var value = builder.ToString().TrimEnd('-');
            return value.Length <= 100 ? value : value.Substring(0, 100);
        }

        private ApiResponse SendWithRefresh(HttpMethod method, string relativePath, object? body)
        {
            var first = Send(method, relativePath, body, SiteConfigStore.Load().OperatorAccessToken);
            if (first.StatusCode != HttpStatusCode.Unauthorized) return first;

            if (!StoreAuth.TryRefresh(out var message))
                return new ApiResponse(0, string.Empty, "BE tu choi access token va khong refresh duoc: " + message);

            return Send(method, relativePath, body, SiteConfigStore.Load().OperatorAccessToken);
        }

        private ApiResponse Send(HttpMethod method, string relativePath, object? body, string accessToken)
        {
            if (!TryBuildUri(relativePath, out var uri, out var error))
                return new ApiResponse(0, string.Empty, error);
            if (string.IsNullOrWhiteSpace(accessToken))
                return new ApiResponse(HttpStatusCode.Unauthorized, string.Empty, "Chua co access token BE.");

            try
            {
                using (var request = new HttpRequestMessage(method, uri))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    if (body != null)
                        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                    using (var response = _http.SendAsync(request).GetAwaiter().GetResult())
                    {
                        return new ApiResponse(response.StatusCode,
                            response.Content.ReadAsStringAsync().GetAwaiter().GetResult(), string.Empty);
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is System.Threading.Tasks.TaskCanceledException)
            {
                return new ApiResponse(0, string.Empty, "Khong ket noi duoc BE: " + ex.Message);
            }
        }

        private static IReadOnlyList<T> ParseList<T>(ApiResponse response, string subject, out string error)
        {
            error = string.Empty;
            if (!string.IsNullOrWhiteSpace(response.TransportError))
            {
                error = response.TransportError;
                return Array.Empty<T>();
            }

            try
            {
                var envelope = JsonSerializer.Deserialize<ApiEnvelope<List<T>>>(response.Body, JsonOptions);
                if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300 ||
                    envelope == null || !envelope.Succeeded || envelope.Data == null)
                {
                    error = envelope?.Message ?? $"BE tu choi lay {subject} (HTTP {(int)response.StatusCode}).";
                    return Array.Empty<T>();
                }
                return envelope.Data;
            }
            catch (JsonException)
            {
                error = $"Response {subject} tu BE khong hop le (HTTP {(int)response.StatusCode}).";
                return Array.Empty<T>();
            }
        }

        internal static ExecutionEndpointRegistrationResult ParseCreateResponse(HttpStatusCode statusCode, string json) =>
            ParseCreate(new ApiResponse(statusCode, json, string.Empty));

        private static ExecutionEndpointRegistrationResult ParseCreate(ApiResponse response)
        {
            if (!string.IsNullOrWhiteSpace(response.TransportError)) return Fail(response.TransportError);
            try
            {
                var envelope = JsonSerializer.Deserialize<ApiEnvelope<BackendExecutionEndpoint>>(response.Body, JsonOptions);
                if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300 ||
                    envelope == null || !envelope.Succeeded || envelope.Data == null)
                    return Fail(envelope?.Message ?? $"BE tu choi dang ky Edge (HTTP {(int)response.StatusCode}).");
                if (envelope.Data.Id == Guid.Empty) return Fail("BE tao Edge nhung khong tra Execution Endpoint ID.");
                return new ExecutionEndpointRegistrationResult
                {
                    Success = true,
                    EndpointId = envelope.Data.Id,
                    Status = envelope.Data.Status,
                    Created = true,
                    Message = envelope.Message ?? "Dang ky Edge thanh cong."
                };
            }
            catch (JsonException)
            {
                return Fail($"Response dang ky Edge tu BE khong hop le (HTTP {(int)response.StatusCode}).");
            }
        }

        private static bool TryBuildUri(string relativePath, out Uri uri, out string error)
        {
            uri = null!;
            error = string.Empty;
            var baseUrl = SiteConfigStore.Load().BeApiUrl?.Trim();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                error = "Chua cau hinh BE_API_URL.";
                return false;
            }
            if (!Uri.TryCreate(baseUrl!.TrimEnd('/') + "/", UriKind.Absolute, out var root) || root == null)
            {
                error = "BE_API_URL khong hop le.";
                return false;
            }
            uri = new Uri(root, relativePath);
            return true;
        }

        private static ExecutionEndpointRegistrationResult Fail(string message) =>
            new ExecutionEndpointRegistrationResult { Success = false, Message = message };

        private sealed class ApiEnvelope<T>
        {
            public bool Succeeded { get; set; }
            public string? Message { get; set; }
            public T? Data { get; set; }
        }

        private sealed class ApiResponse
        {
            public ApiResponse(HttpStatusCode statusCode, string body, string transportError)
            { StatusCode = statusCode; Body = body; TransportError = transportError; }
            public HttpStatusCode StatusCode { get; }
            public string Body { get; }
            public string TransportError { get; }
        }
    }
}
