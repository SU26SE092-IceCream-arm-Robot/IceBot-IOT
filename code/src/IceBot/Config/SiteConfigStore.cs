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
                    case "BE_ACCESS_TOKEN": settings.OperatorAccessToken = value; break;
                    case "BE_REFRESH_TOKEN": settings.OperatorRefreshToken = value; break;
                    case "KIOSK_CODE": settings.KioskCode = value; break;
                    case "KIOSK_ID": Guid.TryParse(value, out var kioskId); settings.KioskId = kioskId; break;
                    case "MACHINE_DEVICE_IDS": settings.MachineDeviceIds = ParseMachineDeviceIds(value); break;
                    case "EXECUTION_ENDPOINT_ID": Guid.TryParse(value, out var endpointId); settings.ExecutionEndpointId = endpointId; break;
                    case "EXECUTION_CLIENT_CERT_PATH": settings.ExecutionClientCertificatePath = value; break;
                    case "ACTIVE_CONFIGURATION_DEPLOYMENT_ID": Guid.TryParse(value, out var deploymentId); settings.ActiveConfigurationDeploymentId = deploymentId; break;
                    case "ACTIVE_CONFIGURATION_RELEASE_ID": Guid.TryParse(value, out var releaseId); settings.ActiveConfigurationReleaseId = releaseId; break;
                    case "ACTIVE_CONFIGURATION_RELEASE_CHECKSUM": settings.ActiveConfigurationReleaseChecksum = value; break;
                    case "EXECUTION_REPORT_SEQUENCE": long.TryParse(value, out var sequence); settings.ExecutionReportSequence = sequence; break;
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
                $"BE_ACCESS_TOKEN={settings.OperatorAccessToken}",
                $"BE_REFRESH_TOKEN={settings.OperatorRefreshToken}",
                $"KIOSK_CODE={settings.KioskCode}",
                $"KIOSK_ID={settings.KioskId:D}",
                $"MACHINE_DEVICE_IDS={SerializeMachineDeviceIds(settings.MachineDeviceIds)}",
                $"EXECUTION_ENDPOINT_ID={settings.ExecutionEndpointId:D}",
                $"EXECUTION_CLIENT_CERT_PATH={settings.ExecutionClientCertificatePath}",
                $"ACTIVE_CONFIGURATION_DEPLOYMENT_ID={settings.ActiveConfigurationDeploymentId:D}",
                $"ACTIVE_CONFIGURATION_RELEASE_ID={settings.ActiveConfigurationReleaseId:D}",
                $"ACTIVE_CONFIGURATION_RELEASE_CHECKSUM={settings.ActiveConfigurationReleaseChecksum}",
                $"EXECUTION_REPORT_SEQUENCE={settings.ExecutionReportSequence}",
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
            SetEnv("ICEBOT_BE_ACCESS_TOKEN", settings.OperatorAccessToken);
            SetEnv("ICEBOT_BE_REFRESH_TOKEN", settings.OperatorRefreshToken);
            SetEnv("ICEBOT_KIOSK_CODE", settings.KioskCode);
            SetEnv("ICEBOT_KIOSK_ID", settings.KioskId == Guid.Empty ? string.Empty : settings.KioskId.ToString("D"));
            SetEnv("ICEBOT_EXECUTION_ENDPOINT_ID", settings.ExecutionEndpointId == Guid.Empty ? string.Empty : settings.ExecutionEndpointId.ToString("D"));
            SetEnv("ICEBOT_EXECUTION_CLIENT_CERT_PATH", settings.ExecutionClientCertificatePath);
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

        // Encoded as "machine_type:device-guid,...". MachineType cannot contain ':' or ','.
        internal static Dictionary<string, Guid> ParseMachineDeviceIds(string value)
        {
            var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(value)) return result;

            foreach (var entry in value.Split(','))
            {
                var idx = entry.IndexOf(':');
                if (idx <= 0) continue;
                var machineType = entry.Substring(0, idx).Trim();
                if (machineType.Length > 0 && Guid.TryParse(entry.Substring(idx + 1).Trim(), out var deviceId) && deviceId != Guid.Empty)
                {
                    result[machineType] = deviceId;
                }
            }

            return result;
        }

        internal static string SerializeMachineDeviceIds(Dictionary<string, Guid> machineDeviceIds)
        {
            var parts = new List<string>();
            foreach (var item in machineDeviceIds)
            {
                if (!string.IsNullOrWhiteSpace(item.Key) && item.Value != Guid.Empty)
                {
                    parts.Add($"{item.Key}:{item.Value:D}");
                }
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
