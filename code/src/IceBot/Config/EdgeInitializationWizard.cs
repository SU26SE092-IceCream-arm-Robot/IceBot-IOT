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
            Console.WriteLine("Buoc 1/3: Ket noi NetBird");
            if (!ConfigSetupWizard.RunNetBird())
            {
                Console.WriteLine("[ERROR] Chua the dang ky Edge vi NetBird chua ket noi.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Buoc 2/3: Kiem tra/dang ky Kiosk voi BE");
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
                Console.WriteLine("Buoc 3/3: Kiem tra Execution Endpoint");
                Console.WriteLine($"[OK] Edge da co Execution Endpoint ID: {settings.ExecutionEndpointId:D}");
                return;
            }

            Console.WriteLine("Buoc 3/3: Dang ky Execution Endpoint");
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
            if (settings.KioskId != Guid.Empty)
            {
                Console.WriteLine($"[OK] Tai su dung KioskId da luu: {settings.KioskId:D}");
                return settings.KioskId;
            }

            if (settings.EdgeInstallationId == Guid.Empty)
            {
                settings.EdgeInstallationId = Guid.NewGuid();
                SiteConfigStore.Save(settings);
                Console.WriteLine($"Da tao dinh danh cai dat Edge: {settings.EdgeInstallationId:D}");
            }

            var result = api.FindOrCreateKiosk(settings.EdgeInstallationId, Environment.MachineName);
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
    }
}
