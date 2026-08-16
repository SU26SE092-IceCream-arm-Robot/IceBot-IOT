using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using IceBot.Config;

namespace IceBot.Api
{
    internal sealed class LoginResult
    {
        public bool Success { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    internal sealed class AuthenticationResponse
    {
        public bool Succeeded { get; set; }
        public string? Message { get; set; }
        public AuthenticatedAccount? Data { get; set; }
    }

    internal sealed class AuthenticatedAccount
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    /// <summary>
    /// Cloud BE API client.
    /// Contract: one request may return 1 or many .lua files in a single response.
    /// TODO: replace MockResolve with real HTTP when BE is ready.
    /// </summary>
    internal static class BeApi
    {
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Operator login through POST /api/v1/authentication/login. These user tokens are
        /// separate from the credential used to identify the Edge device.
        /// </summary>
        public static LoginResult Login(string account, string password)
        {
            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(password))
            {
                return new LoginResult { Success = false, Message = "Thieu tai khoan hoac mat khau." };
            }

            return SendAuthenticationRequest(
                "api/v1/authentication/login",
                new { emailOrUsername = account.Trim(), password });
        }

        public static LoginResult Refresh(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return new LoginResult { Success = false, Message = "Khong co refresh token." };
            }

            return SendAuthenticationRequest(
                "api/v1/authentication/refresh",
                new { refreshToken });
        }

        private static LoginResult SendAuthenticationRequest(string relativePath, object request)
        {
            if (!TryBuildBackendUri(relativePath, out var uri, out var uriError))
            {
                return new LoginResult { Success = false, Message = uriError };
            }

            try
            {
                var json = JsonSerializer.Serialize(request);
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                using (var response = Http.PostAsync(uri, content).GetAwaiter().GetResult())
                {
                    var responseJson = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    AuthenticationResponse? body = null;
                    if (!string.IsNullOrWhiteSpace(responseJson))
                    {
                        body = JsonSerializer.Deserialize<AuthenticationResponse>(responseJson, JsonOptions);
                    }

                    if (!response.IsSuccessStatusCode || body == null || !body.Succeeded || body.Data == null)
                    {
                        var message = body?.Message;
                        if (string.IsNullOrWhiteSpace(message))
                        {
                            message = $"BE tu choi dang nhap (HTTP {(int)response.StatusCode}).";
                        }

                        return new LoginResult { Success = false, Message = message! };
                    }

                    if (string.IsNullOrWhiteSpace(body.Data.AccessToken) ||
                        string.IsNullOrWhiteSpace(body.Data.RefreshToken))
                    {
                        return new LoginResult { Success = false, Message = "BE khong tra ve day du access/refresh token." };
                    }

                    return new LoginResult
                    {
                        Success = true,
                        AccessToken = body.Data.AccessToken,
                        RefreshToken = body.Data.RefreshToken,
                        Message = body.Message ?? "Dang nhap thanh cong."
                    };
                }
            }
            catch (TaskCanceledException)
            {
                return new LoginResult { Success = false, Message = "Ket noi BE bi timeout." };
            }
            catch (HttpRequestException ex)
            {
                return new LoginResult { Success = false, Message = $"Khong ket noi duoc BE: {ex.Message}" };
            }
            catch (JsonException)
            {
                return new LoginResult { Success = false, Message = "Response dang nhap tu BE khong hop le." };
            }
        }

        private static bool TryBuildBackendUri(string relativePath, out Uri uri, out string error)
        {
            uri = null!;
            error = string.Empty;
            var baseUrl = AppConfig.BeApiUrl?.Trim();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                error = "Chua cau hinh BE_API_URL.";
                return false;
            }

            if (!Uri.TryCreate(baseUrl!.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri) ||
                baseUri == null ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            {
                error = "BE_API_URL khong hop le.";
                return false;
            }

            uri = new Uri(baseUri, relativePath);
            return true;
        }

        /// <summary>
        /// Single API call. BE response: { "files": [{ "name", "content" }, ...] } — 1 or N files.
        /// </summary>
    }
}
