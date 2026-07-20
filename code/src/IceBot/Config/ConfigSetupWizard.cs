using System;
using System.Collections.Generic;
using System.Linq;
using IceBot.Machines;

namespace IceBot.Config
{
    internal static class ConfigSetupWizard
    {
        public static void Run()
        {
            Console.WriteLine();
            Console.WriteLine("=== Cau hinh DuckDNS + Cloudflare Tunnel ===");
            Console.WriteLine("Nhan ENTER de giu gia tri hien tai (neu co).");
            Console.WriteLine();

            var current = SiteConfigStore.Load();

            var settings = new SiteSettings
            {
                DuckDnsSubdomain = Prompt("DuckDNS subdomain (vd: ice-shop-01)", current.DuckDnsSubdomain),
                DuckDnsToken = PromptSecret("DuckDNS token", current.DuckDnsToken),
                TunnelName = Prompt("Cloudflare tunnel name (vd: icebot)", current.TunnelName),
                PublicUrl = Prompt("Public URL cho BE (Cloudflare, vd: https://shop.api.tenban.com)", current.PublicUrl),
                ApiKey = PromptSecret("API key chia se voi BE (X-Api-Key)", current.ApiKey),
                RobotIp = Prompt("IP robot Fairino", string.IsNullOrWhiteSpace(current.RobotIp) ? AppConfig.DefaultRobotIp : current.RobotIp),
                MachinePorts = new Dictionary<string, string>(current.MachinePorts, StringComparer.OrdinalIgnoreCase),
            };

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
            Console.WriteLine();
            Console.WriteLine("Buoc tiep theo (mot lan tren may nay):");
            Console.WriteLine("  1. deploy\\duckdns\\register-scheduled-task.ps1");
            Console.WriteLine("  2. deploy\\cloudflare\\setup-tunnel.ps1  (sau khi cloudflared tunnel login)");
            Console.WriteLine("  3. Chon menu 'Chay server' trong IceBot");
        }

        public static void PrintSummary(SiteSettings settings)
        {
            Console.WriteLine("--- Cau hinh hien tai ---");
            Console.WriteLine($"  DuckDNS        : {settings.DuckDnsDomain}");
            Console.WriteLine($"  Tunnel name    : {settings.TunnelName}");
            Console.WriteLine($"  Public URL     : {settings.PublicUrl}");
            Console.WriteLine($"  API key        : {(string.IsNullOrEmpty(settings.ApiKey) ? "(chua dat)" : "****")}");
            Console.WriteLine($"  Robot IP       : {settings.RobotIp}");
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

        private static string PromptSecret(string label, string current)
        {
            var hasValue = !string.IsNullOrWhiteSpace(current);
            Console.Write($"{label}{(hasValue ? " [****]" : "")}: ");
            var input = Console.ReadLine()?.Trim() ?? string.Empty;
            return string.IsNullOrEmpty(input) ? current : input;
        }
    }
}
