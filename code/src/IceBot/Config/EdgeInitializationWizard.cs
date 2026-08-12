using System;
using IceBot.Api;

namespace IceBot.Config
{
    internal static class EdgeInitializationWizard
    {
        public static void Run()
        {
            Console.WriteLine();
            Console.WriteLine("=== KHOI TAO MAY EDGE ===");
            Console.WriteLine("Buoc 1/4: Xac dinh Kiosk Code");
            var settings = SiteConfigStore.Load();
            if (!EnsureKioskCode(settings)) return;

            Console.WriteLine();
            Console.WriteLine("Buoc 2/4: Ket noi NetBird");
            if (!ConfigSetupWizard.RunNetBird())
            {
                Console.WriteLine("[ERROR] Chua the dang ky Edge vi NetBird chua ket noi.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Buoc 3/4: Kiem tra/dang ky Kiosk voi BE");
            RegisterExecutionEndpointIfMissing();
        }

        internal static void RegisterExecutionEndpointIfMissing()
        {
            var settings = SiteConfigStore.Load();
            var api = new ExecutionEndpointRegistrationApi();
            var kioskId = ResolveOrRegisterKiosk(api, settings);
            if (kioskId == Guid.Empty) return;

            if (settings.ExecutionEndpointId != Guid.Empty)
            {
                Console.WriteLine("Buoc 4/4: Kiem tra Execution Endpoint");
                Console.WriteLine($"[OK] Edge da co Execution Endpoint ID: {settings.ExecutionEndpointId:D}");
                return;
            }

            Console.WriteLine("Buoc 4/4: Dang ky Execution Endpoint");
            var endpointCode = ExecutionEndpointRegistrationApi.BuildEndpointCode(Environment.MachineName);
            Console.WriteLine($"Dang ky ma Edge: {endpointCode}");
            var result = api.FindOrCreate(kioskId, endpointCode);
            if (!result.Success)
            {
                Console.WriteLine("[ERROR] " + result.Message);
                return;
            }

            settings.KioskId = kioskId;
            settings.ExecutionEndpointId = result.EndpointId;
            SiteConfigStore.Save(settings);
            Console.WriteLine("[OK] " + result.Message);
            Console.WriteLine($"Execution Endpoint ID: {result.EndpointId:D}");
            if (!string.Equals(result.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[WARN] Trang thai BE hien tai: {result.Status}. Can provision chung chi mTLS truoc khi nhan Order.");
            }
        }

        private static Guid ResolveOrRegisterKiosk(ExecutionEndpointRegistrationApi api, SiteSettings settings)
        {
            if (!EnsureKioskCode(settings)) return Guid.Empty;

            if (settings.KioskId != Guid.Empty)
            {
                Console.WriteLine($"[OK] Tai su dung KioskId da luu: {settings.KioskId:D}");
                return settings.KioskId;
            }

            var result = api.FindOrCreateKiosk(settings.KioskCode, Environment.MachineName);
            if (!result.Success)
            {
                Console.WriteLine("[ERROR] " + result.Message);
                return Guid.Empty;
            }

            settings.KioskId = result.KioskId;
            SiteConfigStore.Save(settings);
            Console.WriteLine("[OK] " + result.Message);
            Console.WriteLine($"KioskId: {result.KioskId:D}");
            return result.KioskId;
        }

        private static bool EnsureKioskCode(SiteSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings.KioskCode))
            {
                Console.WriteLine($"[OK] Tai su dung Kiosk Code da luu: {settings.KioskCode}");
                return true;
            }

            settings.KioskCode = PromptKioskCode();
            if (string.IsNullOrWhiteSpace(settings.KioskCode)) return false;
            SiteConfigStore.Save(settings);
            Console.WriteLine($"[OK] Da luu Kiosk Code: {settings.KioskCode}");
            return true;
        }

        private static string PromptKioskCode()
        {
            while (true)
            {
                Console.Write("Nhap Kiosk Code in tren vo may: ");
                var input = Console.ReadLine();
                if (input == null) return string.Empty;
                if (ExecutionEndpointRegistrationApi.TryNormalizeKioskCode(input, out var code, out var error))
                    return code;
                Console.WriteLine("[ERROR] " + error);
            }
        }
    }
}
