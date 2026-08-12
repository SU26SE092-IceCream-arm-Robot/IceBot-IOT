using System;
using System.Collections.Generic;
using IceBot.Api;

namespace IceBot.Config
{
    internal static class EdgeInitializationWizard
    {
        public static void Run()
        {
            Console.WriteLine();
            Console.WriteLine("=== KHOI TAO MAY EDGE ===");
            Console.WriteLine("Buoc 1/3: Dang nhap BE");
            StoreAuth.RequireLogin();

            Console.WriteLine();
            Console.WriteLine("Buoc 2/3: Ket noi NetBird");
            if (!ConfigSetupWizard.RunNetBird())
            {
                Console.WriteLine("[ERROR] Chua the dang ky Edge vi NetBird chua ket noi.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Buoc 3/3: Kiem tra dang ky Edge voi BE");
            RegisterExecutionEndpointIfMissing();
        }

        internal static void RegisterExecutionEndpointIfMissing()
        {
            var settings = SiteConfigStore.Load();
            if (settings.ExecutionEndpointId != Guid.Empty)
            {
                Console.WriteLine($"[OK] Edge da co Execution Endpoint ID: {settings.ExecutionEndpointId:D}");
                return;
            }

            var api = new ExecutionEndpointRegistrationApi();
            var kioskId = ResolveKiosk(api, settings.KioskId);
            if (kioskId == Guid.Empty) return;

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

        private static Guid ResolveKiosk(ExecutionEndpointRegistrationApi api, Guid savedKioskId)
        {
            var kiosks = api.ListKiosks(out var error);
            if (!string.IsNullOrWhiteSpace(error))
            {
                Console.WriteLine("[ERROR] " + error);
                return Guid.Empty;
            }
            if (kiosks.Count == 0)
            {
                Console.WriteLine("[ERROR] Tai khoan khong duoc gan voi kiosk nao.");
                return Guid.Empty;
            }

            foreach (var kiosk in kiosks)
                if (kiosk.Id == savedKioskId) return savedKioskId;
            if (kiosks.Count == 1)
            {
                Console.WriteLine($"Tu dong chon kiosk: {kiosks[0].Name} ({kiosks[0].Code})");
                return kiosks[0].Id;
            }

            Console.WriteLine("Tai khoan co nhieu kiosk. Chon kiosk gan voi Edge nay:");
            for (var i = 0; i < kiosks.Count; i++)
                Console.WriteLine($"{i + 1}. {kiosks[i].Name} ({kiosks[i].Code}) - {kiosks[i].Id:D}");
            Console.Write("Chon: ");
            if (!int.TryParse(Console.ReadLine()?.Trim(), out var selected) || selected < 1 || selected > kiosks.Count)
            {
                Console.WriteLine("[ERROR] Lua chon kiosk khong hop le.");
                return Guid.Empty;
            }
            return kiosks[selected - 1].Id;
        }
    }
}
