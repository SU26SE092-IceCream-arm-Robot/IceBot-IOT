using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IceBot.Config;

namespace IceBot.Api
{
    internal sealed class PeripheralDeviceRegistration
    {
        public long DeviceTypeId { get; set; }
        public Guid? DeviceModelId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public string? PositionLabel { get; set; }
        public string? FirmwareVersion { get; set; }
        public DateTimeOffset? InstalledAt { get; set; }
    }

    internal sealed class PeripheralDeviceRegistrationResult
    {
        public bool Success { get; set; }
        public Guid DeviceId { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    internal sealed class PeripheralDeviceApi
    {
        private readonly HttpClient _http;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public PeripheralDeviceApi() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(15) }) { }

        internal PeripheralDeviceApi(HttpClient http)
        {
            _http = http;
        }

        public PeripheralDeviceRegistrationResult Register(Guid kioskId, PeripheralDeviceRegistration request)
        {
            if (kioskId == Guid.Empty) return Fail("KioskId khong hop le.");
            if (request.DeviceTypeId <= 0) return Fail("DeviceTypeId phai lon hon 0.");
            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
                return Fail("Code va Name cua may la bat buoc.");

            var first = Send(kioskId, request, SiteConfigStore.Load().OperatorAccessToken);
            if (first.StatusCode != HttpStatusCode.Unauthorized) return Parse(first);

            if (!StoreAuth.TryRefresh(out var refreshMessage))
                return Fail("BE tu choi access token va khong refresh duoc: " + refreshMessage);

            return Parse(Send(kioskId, request, SiteConfigStore.Load().OperatorAccessToken));
        }

        private ApiResponse Send(Guid kioskId, PeripheralDeviceRegistration request, string accessToken)
        {
            if (!TryBuildUri(kioskId, out var uri, out var error))
                return new ApiResponse(HttpStatusCode.BadRequest, string.Empty, error);
            if (string.IsNullOrWhiteSpace(accessToken))
                return new ApiResponse(HttpStatusCode.Unauthorized, string.Empty, "Chua co access token BE.");

            try
            {
                using (var message = new HttpRequestMessage(HttpMethod.Post, uri))
                {
                    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    message.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                    using (var response = _http.SendAsync(message).GetAwaiter().GetResult())
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

        private static PeripheralDeviceRegistrationResult Parse(ApiResponse response)
        {
            if (!string.IsNullOrWhiteSpace(response.TransportError)) return Fail(response.TransportError);
            return ParseRegistrationResponse(response.StatusCode, response.Body);
        }

        internal static PeripheralDeviceRegistrationResult ParseRegistrationResponse(HttpStatusCode statusCode, string bodyJson)
        {
            try
            {
                var body = JsonSerializer.Deserialize<DeviceApiResponse>(bodyJson, JsonOptions);
                if ((int)statusCode < 200 || (int)statusCode >= 300 || body == null || !body.Succeeded || body.Data == null)
                    return Fail(body?.Message ?? $"BE tu choi dang ky may (HTTP {(int)statusCode}).");
                if (body.Data.Id == Guid.Empty) return Fail("BE dang ky thanh cong nhung khong tra DeviceId.");
                return new PeripheralDeviceRegistrationResult
                {
                    Success = true,
                    DeviceId = body.Data.Id,
                    Message = body.Message ?? "Dang ky may thanh cong."
                };
            }
            catch (JsonException)
            {
                return Fail($"Response dang ky may tu BE khong hop le (HTTP {(int)statusCode}).");
            }
        }

        private static bool TryBuildUri(Guid kioskId, out Uri uri, out string error)
        {
            uri = null!;
            error = string.Empty;
            var baseUrl = SiteConfigStore.Load().BeApiUrl?.Trim();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                error = "Chua cau hinh BE_API_URL (can URL private cua BE tren NetBird).";
                return false;
            }
            if (!Uri.TryCreate(baseUrl!.TrimEnd('/') + "/", UriKind.Absolute, out var root) || root == null)
            {
                error = "BE_API_URL khong hop le.";
                return false;
            }
            uri = new Uri(root, $"api/v1/management/kiosks/{kioskId:D}/devices");
            return true;
        }

        private static PeripheralDeviceRegistrationResult Fail(string message) =>
            new PeripheralDeviceRegistrationResult { Success = false, Message = message };

        private sealed class DeviceApiResponse
        {
            public bool Succeeded { get; set; }
            public string? Message { get; set; }
            public DeviceData? Data { get; set; }
        }

        private sealed class DeviceData { public Guid Id { get; set; } }

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
