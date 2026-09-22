using System;
using IceBot.Api;

namespace IceBot.Config
{
    internal static class EdgeInitializationWizard
    {
        public static void Run()
        {
            StoreAuth.RequireLogin();

            Console.WriteLine();
            Console.WriteLine("=== KHOI TAO MAY EDGE ===");
            var settings = SiteConfigStore.Load();

            Console.WriteLine();
            Console.WriteLine("Buoc 1/6: Ket noi NetBird");
            if (!ConfigSetupWizard.RunNetBird())
            {
                Console.WriteLine("[ERROR] Chua the dang ky Edge vi NetBird chua ket noi.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Buoc 2/6: Cau hinh Robot va cong COM");
            ConfigSetupWizard.RunRobotSettings();
            ConfigSetupWizard.RunMachinePortSettings();

            Console.WriteLine();
            Console.WriteLine("Buoc 3/6: Tu dong xac dinh/dang ky Kiosk voi BE");
            RegisterExecutionEndpointIfMissing();
        }

        internal static void RegisterExecutionEndpointIfMissing()
        {
            var settings = SiteConfigStore.Load();
            var api = new ExecutionEndpointRegistrationApi();
            var kioskId = ResolveOrRegisterKiosk(api, settings);
            if (kioskId == Guid.Empty) return;

            Guid endpointId;
            string endpointStatus;
            Guid? backendProfileIdentity = null;
            if (settings.ExecutionEndpointId != Guid.Empty)
            {
                Console.WriteLine("Buoc 4/6: Kiem tra Execution Endpoint");
                Console.WriteLine($"[OK] Edge da co Execution Endpoint ID: {settings.ExecutionEndpointId:D}");
                var current = api.GetEndpoint(kioskId, settings.ExecutionEndpointId);
                if (!current.Success)
                {
                    Console.WriteLine("[ERROR] " + current.Message);
                    return;
                }
                endpointId = settings.ExecutionEndpointId;
                endpointStatus = current.Status;
                backendProfileIdentity = current.ProfileIdentity;
            }
            else
            {
                Console.WriteLine("Buoc 4/6: Dang ky Execution Endpoint");
                var endpointCode = ExecutionEndpointRegistrationApi.BuildEndpointCode(Environment.MachineName);
                Console.WriteLine($"Dang ky ma Edge: {endpointCode}");
                var result = api.FindOrCreate(kioskId, endpointCode);
                if (!result.Success)
                {
                    Console.WriteLine("[ERROR] " + result.Message);
                    return;
                }
                endpointId = result.EndpointId;
                endpointStatus = result.Status;
                backendProfileIdentity = result.ProfileIdentity;
                settings.ExecutionEndpointId = endpointId;
                SiteConfigStore.Save(settings);
                Console.WriteLine("[OK] " + result.Message);
                Console.WriteLine($"Execution Endpoint ID: {endpointId:D}");
            }

            settings.KioskId = kioskId;
            SiteConfigStore.Save(settings);
            Console.WriteLine();
            Console.WriteLine("Buoc 5/6: Provision mTLS");
            CompleteMutualTls(api, settings, endpointStatus, backendProfileIdentity);
        }

        private static void CompleteMutualTls(
            ExecutionEndpointRegistrationApi api,
            SiteSettings settings,
            string endpointStatus,
            Guid? backendProfileIdentity)
        {
            if (string.Equals(endpointStatus, "Active", StringComparison.OrdinalIgnoreCase))
            {
                if (backendProfileIdentity.HasValue && backendProfileIdentity.Value != Guid.Empty)
                    settings.FullEdgeRuntimeId = backendProfileIdentity.Value;
                if (string.IsNullOrWhiteSpace(settings.ExecutionClientCertificatePath) &&
                    System.IO.File.Exists(EdgeClientCertificateProvisioner.DefaultCertificatePath))
                    settings.ExecutionClientCertificatePath = EdgeClientCertificateProvisioner.DefaultCertificatePath;
                SiteConfigStore.Save(settings);
                if (string.IsNullOrWhiteSpace(settings.ExecutionClientCertificatePath) ||
                    !System.IO.File.Exists(settings.ExecutionClientCertificatePath))
                {
                    Console.WriteLine("[ERROR] Endpoint da Active nhung Edge khong co PFX da duoc BE gan fingerprint; khong tu tao PFX thay the.");
                    return;
                }
                ActivateKioskAndProbe(api, settings);
                return;
            }

            if (!string.Equals(endpointStatus, "Provisioning", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[ERROR] Khong the provision endpoint o trang thai {endpointStatus}.");
                return;
            }

            if (settings.FullEdgeRuntimeId == Guid.Empty)
            {
                settings.FullEdgeRuntimeId = Guid.NewGuid();
                SiteConfigStore.Save(settings);
            }

            var certificate = EdgeClientCertificateProvisioner.Ensure(settings);
            if (!certificate.Success)
            {
                Console.WriteLine("[ERROR] " + certificate.Message);
                return;
            }
            settings.ExecutionClientCertificatePath = certificate.CertificatePath;
            SiteConfigStore.Save(settings);
            Console.WriteLine("[OK] " + certificate.Message);
            Console.WriteLine("SHA-256 fingerprint: " + certificate.Sha256Fingerprint);

            var provision = api.ProvisionMutualTls(
                settings.KioskId,
                settings.ExecutionEndpointId,
                settings.FullEdgeRuntimeId,
                certificate.Sha256Fingerprint);
            if (!provision.Success)
            {
                // A timeout after BE commit is recoverable: inspect current state before failing.
                var current = api.GetEndpoint(settings.KioskId, settings.ExecutionEndpointId);
                if (!current.Success || !string.Equals(current.Status, "Active", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("[ERROR] Provision mTLS that bai: " + provision.Message);
                    return;
                }
            }

            var confirmed = api.GetEndpoint(settings.KioskId, settings.ExecutionEndpointId);
            if (!confirmed.Success || !string.Equals(confirmed.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("[ERROR] BE chua xac nhan Execution Endpoint o trang thai Active.");
                return;
            }
            Console.WriteLine("[OK] Execution Endpoint da Active tren BE.");
            ActivateKioskAndProbe(api, settings);
        }

        private static void ActivateKioskAndProbe(ExecutionEndpointRegistrationApi api, SiteSettings settings)
        {
            Console.WriteLine();
            Console.WriteLine("Buoc 6/6: Kich hoat kiosk va kiem tra ket noi");
            var activation = api.ActivateKiosk(settings.KioskId);
            if (!activation.Success)
            {
                Console.WriteLine("[ERROR] Kich hoat kiosk that bai: " + activation.Message);
                return;
            }
            if (!string.Equals(activation.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[ERROR] Kiosk chua Active (trang thai: {activation.Status}).");
                return;
            }
            if (!string.Equals(activation.OperationalState, "Operational", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[ERROR] Kiosk Active nhung operational state la {activation.OperationalState}, chua the nhan don online.");
                return;
            }
            Console.WriteLine("[OK] Kiosk da Active va Operational.");
            var connected = EdgeMtlsProbe.SendHeartbeatAndReportedDevices(out var message);
            Console.WriteLine(connected ? "[OK] " + message : "[ERROR] " + message);
        }

        private static Guid ResolveOrRegisterKiosk(ExecutionEndpointRegistrationApi api, SiteSettings settings)
        {
            if (settings.KioskId != Guid.Empty)
            {
                Console.WriteLine($"[OK] Tai su dung KioskId da luu: {settings.KioskId:D}");
                return settings.KioskId;
            }

            // Existing installations with a saved KioskCode remain compatible, but a fresh
            // Edge no longer asks the technician to type the code. It resolves the kiosk from
            // the logged-in Org account and accepts only one visible kiosk.
            var result = string.IsNullOrWhiteSpace(settings.KioskCode)
                ? api.ResolveSingleAccessibleKiosk()
                : api.FindOrCreateKiosk(settings.KioskCode, Environment.MachineName);
            if (!result.Success)
            {
                Console.WriteLine("[ERROR] " + result.Message);
                return Guid.Empty;
            }

            settings.KioskId = result.KioskId;
            if (!string.IsNullOrWhiteSpace(result.KioskCode))
                settings.KioskCode = result.KioskCode;
            SiteConfigStore.Save(settings);
            Console.WriteLine("[OK] " + result.Message);
            if (!string.IsNullOrWhiteSpace(result.KioskCode))
                Console.WriteLine($"Kiosk Code: {result.KioskCode}");
            Console.WriteLine($"KioskId: {result.KioskId:D}");
            return result.KioskId;
        }
    }
}
