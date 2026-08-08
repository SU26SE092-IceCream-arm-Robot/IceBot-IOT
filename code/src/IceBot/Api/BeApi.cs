using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using IceBot.Config;

namespace IceBot.Api
{
    internal sealed class LuaScript
    {
        public LuaScript(string fileName, string content)
        {
            FileName = fileName;
            Content = content;
        }

        public string FileName { get; }
        public string Content { get; }
    }

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

        public static IReadOnlyList<LuaScript> GetLua(string model)
        {
            return GetLua(new[] { model });
        }

        /// <summary>
        /// Single API call. BE response: { "files": [{ "name", "content" }, ...] } — 1 or N files.
        /// </summary>
        public static IReadOnlyList<LuaScript> GetLua(IReadOnlyList<string> models)
        {
            if (models == null || models.Count == 0)
            {
                throw new ArgumentException("Chua nhap model.", nameof(models));
            }

            var normalized = NormalizeModels(models);
            if (normalized.Count == 0)
            {
                throw new ArgumentException("Chua nhap model.", nameof(models));
            }

            // TODO: POST { machineIds: normalized } -> parse response.files[]
            var files = MockResolve(normalized);
            if (files.Count == 0)
            {
                throw new InvalidOperationException("BE khong tra ve file nao.");
            }

            return files;
        }

        private static IReadOnlyList<LuaScript> MockResolve(IReadOnlyList<string> models)
        {
            var files = new List<LuaScript>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var model in models)
            {
                var key = model.ToLowerInvariant();

                if (BundleCatalog.TryGetValue(key, out var bundle))
                {
                    foreach (var bundleFile in bundle)
                    {
                        AddFile(files, seen, model, bundleFile);
                    }

                    continue;
                }

                if (ModelCatalog.TryGetValue(key, out var mappedFile))
                {
                    AddFile(files, seen, model, mappedFile);
                    continue;
                }

                AddFile(files, seen, model, SanitizeFileName(key));
            }

            return files;
        }

        private static void AddFile(List<LuaScript> files, HashSet<string> seen, string model, string fileName)
        {
            if (!seen.Add(fileName))
            {
                return;
            }

            files.Add(new LuaScript(fileName, BuildStubContent(model, fileName)));
        }

        private static List<string> NormalizeModels(IReadOnlyList<string> models)
        {
            var list = new List<string>();
            foreach (var model in models)
            {
                var trimmed = model?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(trimmed))
                {
                    list.Add(trimmed);
                }
            }

            return list;
        }

        private static readonly Dictionary<string, string> ModelCatalog =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["lay_coc"] = "lay_coc.lua",
                ["cup_s"] = "cup_s.lua",
                ["ice_chocolate_s"] = "ice_chocolate_s.lua",
                ["topping_keo_com"] = "topping_keo_com.lua",
                ["deliver_tray"] = "deliver_tray.lua",
            };

        /// <summary>Models that return a full workflow bundle in one API response.</summary>
        private static readonly Dictionary<string, string[]> BundleCatalog =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["fr5"] = new[]
                {
                    "lay_coc.lua",
                    "cup_s.lua",
                    "ice_chocolate_s.lua",
                    "topping_keo_com.lua",
                    "deliver_tray.lua",
                },
                ["full"] = new[]
                {
                    "lay_coc.lua",
                    "cup_s.lua",
                    "ice_chocolate_s.lua",
                    "topping_keo_com.lua",
                    "deliver_tray.lua",
                },
            };

        private static string SanitizeFileName(string model)
        {
            var safe = model.Replace(' ', '_').Replace('-', '_');
            return safe.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) ? safe : safe + ".lua";
        }

        private static string BuildStubContent(string model, string fileName)
        {
            return $@"-- Mock Lua from BeApi.GetLua
-- Model: {model}
-- File: {fileName}
-- TODO: replace with real BE API response

local function toDouble(v)
    return tonumber(tostring(v)) + 0.0
end

-- Home pose (stub)
MoveJ({{toDouble(""0.000""), toDouble(""-96.600""), toDouble(""-83.500""), toDouble(""0.000""), toDouble(""0.000""), toDouble(""0.000"")}}, 0, 0, toDouble(""30.0""), toDouble(""30.0""), toDouble(""-1.0""), toDouble(""-1.0""))
";
        }
    }
}
