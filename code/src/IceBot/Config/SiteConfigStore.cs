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

        public static string DuckDnsEnvPath =>
            Path.Combine(ConfigDirectory, "duckdns.env");

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
                    case "DUCKDNS_SUBDOMAIN": settings.DuckDnsSubdomain = value; break;
                    case "DUCKDNS_TOKEN": settings.DuckDnsToken = value; break;
                    case "TUNNEL_NAME": settings.TunnelName = value; break;
                    case "PUBLIC_URL": settings.PublicUrl = value; break;
                    case "BE_API_URL": settings.BeApiUrl = value; break;
                    case "API_KEY": settings.ApiKey = value; break;
                    case "ROBOT_IP": settings.RobotIp = value; break;
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
                $"DUCKDNS_SUBDOMAIN={settings.DuckDnsSubdomain}",
                $"DUCKDNS_TOKEN={settings.DuckDnsToken}",
                $"TUNNEL_NAME={settings.TunnelName}",
                $"PUBLIC_URL={settings.PublicUrl}",
                $"BE_API_URL={settings.BeApiUrl}",
                $"API_KEY={settings.ApiKey}",
                $"ROBOT_IP={settings.RobotIp}",
            };

            File.WriteAllLines(SiteConfigPath, lines, Encoding.UTF8);

            var duckLines = new[]
            {
                "# Synced from IceBot config wizard",
                $"DUCKDNS_SUBDOMAIN={settings.DuckDnsSubdomain}",
                $"DUCKDNS_TOKEN={settings.DuckDnsToken}",
            };
            File.WriteAllLines(DuckDnsEnvPath, duckLines, Encoding.UTF8);

            _cached = settings;
            ApplyToEnvironment(settings);
        }

        public static void ApplyToEnvironment(SiteSettings settings)
        {
            SetEnv("ICEBOT_DUCKDNS_DOMAIN", settings.DuckDnsDomain);
            SetEnv("ICEBOT_PUBLIC_URL", settings.PublicUrl);
            SetEnv("ICEBOT_BE_API_URL", settings.BeApiUrl);
            SetEnv("ICEBOT_API_KEY", settings.ApiKey);
            SetEnv("ICEBOT_ROBOT_IP", settings.RobotIp);
            SetEnv("ICEBOT_TUNNEL_NAME", settings.TunnelName);
        }

        private static void SetEnv(string name, string value)
        {
            Environment.SetEnvironmentVariable(name, string.IsNullOrWhiteSpace(value) ? null : value);
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
