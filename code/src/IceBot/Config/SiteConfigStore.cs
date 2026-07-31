using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IceBot.Config
{
    internal static class SiteConfigStore
    {
        private static SiteSettings? _cached;

        public static string ConfigDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");

        public static string SiteConfigPath =>
            Path.Combine(ConfigDirectory, "icebot.site.env");

        public static SiteSettings Load()
        {
            if (_cached != null)
            {
                return _cached;
            }

            var settings = new SiteSettings();
            if (!File.Exists(SiteConfigPath))
            {
                _cached = settings;
                return settings;
            }

            foreach (var line in File.ReadAllLines(SiteConfigPath))
            {
                if (!TryParseLine(line, out var key, out var value))
                {
                    continue;
                }

                switch (key)
                {
                    case "NETBIRD_SETUP_KEY": settings.NetBirdSetupKey = value; break;
                    case "PUBLIC_URL": settings.PublicUrl = value; break;
                    case "BE_API_URL": settings.BeApiUrl = value; break;
                    case "API_KEY": settings.ApiKey = value; break;
                    case "ROBOT_IP": settings.RobotIp = value; break;
                    case "STORE_ACCOUNT": settings.StoreAccount = value; break;
                    case "STORE_PASSWORD": settings.StorePassword = value; break;
                    case "BE_SESSION_KEY": settings.BeSessionKey = value; break;
                    case "MACHINE_PORTS": settings.MachinePorts = ParseMachinePorts(value); break;
                    case "PROVISIONED_STEPS": settings.ProvisionedSteps = ParseList(value); break;
                }
            }

            _cached = settings;
            ApplyToEnvironment(settings);
            return settings;
        }

        public static void Save(SiteSettings settings)
        {
            Directory.CreateDirectory(ConfigDirectory);

            var lines = new[]
            {
                "# IceBot site config — do not commit to git",
                $"NETBIRD_SETUP_KEY={settings.NetBirdSetupKey}",
                $"PUBLIC_URL={settings.PublicUrl}",
                $"BE_API_URL={settings.BeApiUrl}",
                $"API_KEY={settings.ApiKey}",
                $"ROBOT_IP={settings.RobotIp}",
                $"STORE_ACCOUNT={settings.StoreAccount}",
                $"STORE_PASSWORD={settings.StorePassword}",
                $"BE_SESSION_KEY={settings.BeSessionKey}",
                $"MACHINE_PORTS={SerializeMachinePorts(settings.MachinePorts)}",
                $"PROVISIONED_STEPS={string.Join(",", settings.ProvisionedSteps)}",
            };

            File.WriteAllLines(SiteConfigPath, lines, Encoding.UTF8);

            _cached = settings;
            ApplyToEnvironment(settings);
        }

        public static void ApplyToEnvironment(SiteSettings settings)
        {
            SetEnv("ICEBOT_NETBIRD_SETUP_KEY", settings.NetBirdSetupKey);
            SetEnv("ICEBOT_PUBLIC_URL", settings.PublicUrl);
            SetEnv("ICEBOT_BE_API_URL", settings.BeApiUrl);
            SetEnv("ICEBOT_API_KEY", settings.ApiKey);
            SetEnv("ICEBOT_ROBOT_IP", settings.RobotIp);
            SetEnv("ICEBOT_STORE_ACCOUNT", settings.StoreAccount);
            SetEnv("ICEBOT_BE_SESSION_KEY", settings.BeSessionKey);
        }

        private static void SetEnv(string name, string value)
        {
            Environment.SetEnvironmentVariable(name, string.IsNullOrWhiteSpace(value) ? null : value);
        }

        // Encoded as "type1:COM3,type2:COM4" — see SiteSettings.MachinePorts.
        private static Dictionary<string, string> ParseMachinePorts(string value)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(value))
            {
                return result;
            }

            foreach (var entry in value.Split(','))
            {
                var idx = entry.IndexOf(':');
                if (idx <= 0)
                {
                    continue;
                }

                var type = entry.Substring(0, idx).Trim();
                var port = entry.Substring(idx + 1).Trim();
                if (type.Length > 0 && port.Length > 0)
                {
                    result[type] = port;
                }
            }

            return result;
        }

        private static string SerializeMachinePorts(Dictionary<string, string> machinePorts)
        {
            var parts = new List<string>();
            foreach (var kvp in machinePorts)
            {
                parts.Add($"{kvp.Key}:{kvp.Value}");
            }

            return string.Join(",", parts);
        }

        // Encoded as "a,b,c" — see SiteSettings.ProvisionedSteps.
        private static List<string> ParseList(string value)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return result;
            }

            foreach (var entry in value.Split(','))
            {
                var trimmed = entry.Trim();
                if (trimmed.Length > 0)
                {
                    result.Add(trimmed);
                }
            }

            return result;
        }

        private static bool TryParseLine(string line, out string key, out string value)
        {
            key = string.Empty;
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
            {
                return false;
            }

            var idx = line.IndexOf('=');
            if (idx <= 0)
            {
                return false;
            }

            key = line.Substring(0, idx).Trim();
            value = line.Substring(idx + 1).Trim();
            return true;
        }
    }
}
