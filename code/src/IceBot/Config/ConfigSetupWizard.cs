using System;
using System.Collections.Generic;
using System.Linq;
using IceBot.Machines;

namespace IceBot.Config
{
    internal static class ConfigSetupWizard
    {
        // Only the two fields NetBird itself actually needs. Everything else in SiteSettings
        // (API key, robot IP, store account, COM ports, ProvisionedSteps, BeApiUrl) is left
        // untouched — see RunSystemSettings() for those.
        public static bool RunNetBird()
        {
            Console.WriteLine();
            Console.WriteLine("=== Cau hinh NetBird ===");
            Console.WriteLine("Nhan ENTER de giu gia tri hien tai (neu co).");
            Console.WriteLine();

            var settings = SiteConfigStore.Load();

            var netBirdSetupKey = PromptSecret("NetBird setup key", settings.NetBirdSetupKey);
            var connected = false;
            if (!string.IsNullOrWhiteSpace(netBirdSetupKey))
            {
                Console.WriteLine();
                Console.WriteLine("Dang chay 'netbird up --setup-key ...' ...");
                connected = NetBirdSetup.RunUp(netBirdSetupKey, out var netBirdMessage);
                Console.WriteLine(connected ? $"[OK] {netBirdMessage}" : $"[ERROR] {netBirdMessage}");
                Console.WriteLine();
            }

            settings.NetBirdSetupKey = netBirdSetupKey;
            settings.PublicUrl = Prompt("Public URL cho BE (NetBird cap, vd: https://shop.api.tenban.com)", settings.PublicUrl);

            SiteConfigStore.Save(settings);

            Console.WriteLine();
            Console.WriteLine("[OK] Da luu cau hinh: " + SiteConfigStore.SiteConfigPath);
            Console.WriteLine();
            PrintSummary(settings);
            return connected;
        }

        // Everything that is NOT NetBird and NOT a secret better left to its own dedicated
        // prompt: robot IP, store account (identity only), COM ports. API key and the store
        // password are deliberately NOT prompted here — API key has no other entry point yet
        // (set it via ICEBOT_API_KEY or by editing icebot.site.env directly if needed), and the
        // store password is only ever entered at the actual login gate (StoreAuth.RequireLogin
        // / `IceBot.exe login`), not duplicated into a general settings screen.
        public static void RunSystemSettings()
        {
            Console.WriteLine();
            Console.WriteLine("=== Cau hinh he thong ===");
            Console.WriteLine("Nhan ENTER de giu gia tri hien tai (neu co).");
            Console.WriteLine();

            var current = SiteConfigStore.Load();

            var settings = new SiteSettings
            {
                // Carried forward as-is — not this wizard's concern.
                NetBirdSetupKey = current.NetBirdSetupKey,
                PublicUrl = current.PublicUrl,
                ApiKey = current.ApiKey,
                StorePassword = current.StorePassword,
                ProvisionedSteps = new List<string>(current.ProvisionedSteps),
                ActiveConfigurationDeploymentId = current.ActiveConfigurationDeploymentId,
                ActiveConfigurationReleaseId = current.ActiveConfigurationReleaseId,
                ActiveConfigurationReleaseChecksum = current.ActiveConfigurationReleaseChecksum,
                ExecutionReportSequence = current.ExecutionReportSequence,
                BeApiUrl = Prompt("Backend API URL (vd: https://api.icebot.vn)", current.BeApiUrl),
                ExecutionEndpointId = PromptGuid("Execution endpoint ID (BE provision)", current.ExecutionEndpointId),
                ExecutionClientCertificatePath = Prompt("Execution client certificate PFX path", current.ExecutionClientCertificatePath),
                RobotIp = Prompt("IP robot Fairino", string.IsNullOrWhiteSpace(current.RobotIp) ? AppConfig.DefaultRobotIp : current.RobotIp),
                StoreAccount = Prompt("Tai khoan cua hang (BE cap)", current.StoreAccount),
                MachinePorts = new Dictionary<string, string>(current.MachinePorts, StringComparer.OrdinalIgnoreCase),
            };
            PreserveBackendDeviceIdentities(current, settings);

            // Store password isn't prompted here, but the account might still change — either
            // way invalidates a previously saved session key (a key obtained under a different
            // account no longer applies).
            settings.OperatorAccessToken = settings.StoreAccount == current.StoreAccount
                ? current.OperatorAccessToken
                : string.Empty;
            settings.OperatorRefreshToken = settings.StoreAccount == current.StoreAccount
                ? current.OperatorRefreshToken
                : string.Empty;

            // One COM-port prompt per registered machine that actually needs serial (IMachineTrigger)
            // — a plain arm-motion machine (IMachineModule only) has no port to configure.
            foreach (var trigger in MachineRegistry.Modules.OfType<IMachineTrigger>())
            {
                var port = Prompt($"COM port {trigger.DisplayName} (vd: COM3, de trong neu chua lap)", current.GetMachinePort(trigger.MachineType));
                if (string.IsNullOrWhiteSpace(port))
                {
                    settings.MachinePorts.Remove(trigger.MachineType);
                }
                else
                {
                    settings.MachinePorts[trigger.MachineType] = port;
                }
            }

            SiteConfigStore.Save(settings);

            Console.WriteLine();
            Console.WriteLine("[OK] Da luu cau hinh: " + SiteConfigStore.SiteConfigPath);
            Console.WriteLine();
            PrintSummary(settings);
        }

        internal static void PreserveBackendDeviceIdentities(SiteSettings current, SiteSettings updated)
        {
            updated.KioskCode = current.KioskCode;
            updated.KioskId = current.KioskId;
            updated.MachineDeviceIds = new Dictionary<string, Guid>(
                current.MachineDeviceIds,
                StringComparer.OrdinalIgnoreCase);
        }

        public static void PrintSummary(SiteSettings settings)
        {
            Console.WriteLine("--- Cau hinh hien tai ---");
            Console.WriteLine($"  NetBird setup key : {(string.IsNullOrEmpty(settings.NetBirdSetupKey) ? "(chua dat)" : "****")}");
            Console.WriteLine($"  Public URL     : {settings.PublicUrl}");
            Console.WriteLine($"  Backend API URL: {settings.BeApiUrl}");
            Console.WriteLine($"  Execution endpoint: {(settings.ExecutionEndpointId == Guid.Empty ? "(chua dat)" : settings.ExecutionEndpointId.ToString("D"))}");
            Console.WriteLine($"  Client certificate: {(string.IsNullOrWhiteSpace(settings.ExecutionClientCertificatePath) ? "(chua dat)" : settings.ExecutionClientCertificatePath)}");
            Console.WriteLine($"  Active deployment: {(settings.ActiveConfigurationDeploymentId == Guid.Empty ? "(chua co)" : settings.ActiveConfigurationDeploymentId.ToString("D"))}");
            Console.WriteLine($"  API key        : {(string.IsNullOrEmpty(settings.ApiKey) ? "(chua dat)" : "****")}");
            Console.WriteLine($"  Robot IP       : {settings.RobotIp}");
            Console.WriteLine($"  Tai khoan cua hang : {(string.IsNullOrEmpty(settings.StoreAccount) ? "(chua dat)" : settings.StoreAccount)}");
            Console.WriteLine($"  Kiosk code          : {(string.IsNullOrEmpty(settings.KioskCode) ? "(chua nhap)" : settings.KioskCode)}");
            Console.WriteLine($"  Da dang nhap BE    : {(string.IsNullOrEmpty(settings.OperatorAccessToken) ? "CHUA (dung Khoi tao Edge trong InitIceBot.exe)" : "ROI")}");
            foreach (var trigger in MachineRegistry.Modules.OfType<IMachineTrigger>())
            {
                var port = settings.GetMachinePort(trigger.MachineType);
                Console.WriteLine($"  {trigger.DisplayName,-15}: {(string.IsNullOrEmpty(port) ? "(chua cau hinh)" : port)}");
            }
            Console.WriteLine($"  Local API      : {AppConfig.ApiListenPrefix}");
            Console.WriteLine($"  BE POST orders : {settings.PublicUrl.TrimEnd('/')}/api/orders");
            Console.WriteLine($"  BE GET health  : {settings.PublicUrl.TrimEnd('/')}/health");
        }

        private static string Prompt(string label, string current)
        {
            var suffix = string.IsNullOrWhiteSpace(current) ? string.Empty : $" [{current}]";
            Console.Write($"{label}{suffix}: ");
            var input = Console.ReadLine()?.Trim() ?? string.Empty;
            return string.IsNullOrEmpty(input) ? current : input;
        }

        private static Guid PromptGuid(string label, Guid current)
        {
            while (true)
            {
                var value = Prompt(label, current == Guid.Empty ? string.Empty : current.ToString("D"));
                if (string.IsNullOrWhiteSpace(value)) return Guid.Empty;
                if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty) return parsed;
                Console.WriteLine("Gia tri phai la GUID hop le.");
            }
        }

        private static string PromptSecret(string label, string current)
        {
            var hasValue = !string.IsNullOrWhiteSpace(current);
            Console.Write($"{label}{(hasValue ? " [****]" : "")}: ");
            var input = Console.ReadLine()?.Trim() ?? string.Empty;
            return string.IsNullOrEmpty(input) ? current : input;
        }
    }
}
