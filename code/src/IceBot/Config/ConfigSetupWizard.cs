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
            Console.WriteLine("=== Cau hinh NextBird ===");
            Console.WriteLine("Nhan ENTER de giu gia tri hien tai (neu co).");
            Console.WriteLine();

            var current = SiteConfigStore.Load();

            var settings = new SiteSettings
            {
                NextBirdSetupKey = PromptSecret("NextBirdSetup_key", current.NextBirdSetupKey),
                PublicUrl = Prompt("Public URL cho BE (NextBird cap, vd: https://shop.api.tenban.com)", current.PublicUrl),
                ApiKey = PromptSecret("API key chia se voi BE (X-Api-Key)", current.ApiKey),
                RobotIp = Prompt("IP robot Fairino", string.IsNullOrWhiteSpace(current.RobotIp) ? AppConfig.DefaultRobotIp : current.RobotIp),
                StoreAccount = Prompt("Tai khoan cua hang (BE cap)", current.StoreAccount),
                StorePassword = PromptSecret("Mat khau cua hang", current.StorePassword),
                MachinePorts = new Dictionary<string, string>(current.MachinePorts, StringComparer.OrdinalIgnoreCase),
            };

            // A previously saved key was obtained for the old account/password — if either
            // changed here, it's stale until the store logs in again (`IceBot.exe login`, or
            // automatically next time the app starts).
            settings.BeSessionKey =
                settings.StoreAccount == current.StoreAccount && settings.StorePassword == current.StorePassword
                    ? current.BeSessionKey
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
            Console.WriteLine();
            Console.WriteLine("Buoc tiep theo (mot lan tren may nay):");
            Console.WriteLine("  1. Dam bao NextBirdSetup_key da duoc kich hoat ben NextBird");
            Console.WriteLine("  2. Chon menu 'Chay he thong' trong IceBot");
        }

        public static void PrintSummary(SiteSettings settings)
        {
            Console.WriteLine("--- Cau hinh hien tai ---");
            Console.WriteLine($"  NextBirdSetup_key : {(string.IsNullOrEmpty(settings.NextBirdSetupKey) ? "(chua dat)" : "****")}");
            Console.WriteLine($"  Public URL     : {settings.PublicUrl}");
            Console.WriteLine($"  API key        : {(string.IsNullOrEmpty(settings.ApiKey) ? "(chua dat)" : "****")}");
            Console.WriteLine($"  Robot IP       : {settings.RobotIp}");
            Console.WriteLine($"  Tai khoan cua hang : {(string.IsNullOrEmpty(settings.StoreAccount) ? "(chua dat)" : settings.StoreAccount)}");
            Console.WriteLine($"  Da dang nhap BE    : {(string.IsNullOrEmpty(settings.BeSessionKey) ? "CHUA (IceBot.exe login)" : "ROI")}");
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
