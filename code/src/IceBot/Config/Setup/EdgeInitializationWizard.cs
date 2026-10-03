using System;
using System.Collections.Generic;
using System.Linq;
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
            Console.WriteLine("Buoc 1/7: Ket noi NetBird");
            if (!ConfigSetupWizard.RunNetBird())
            {
                Console.WriteLine("[ERROR] Chua the dang ky Edge vi NetBird chua ket noi.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Buoc 2/7: Cau hinh Robot Fairino");
            ConfigSetupWizard.RunRobotSettings();

            Console.WriteLine();
            Console.WriteLine("Buoc 3/7: Xac dinh Kiosk voi BE");
            RegisterExecutionEndpointIfMissing();
        }

        internal static void RegisterExecutionEndpointIfMissing()
        {
            var settings = SiteConfigStore.Load();
            var api = new ExecutionEndpointRegistrationApi();
            var kioskId = ResolveOrRegisterKiosk(api, settings);
            if (kioskId == Guid.Empty) return;

            Console.WriteLine();
            Console.WriteLine("Buoc 4/7: Cau hinh ten dia diem Edge");
            if (!ConfigSetupWizard.RunEdgeLocationSettings())
                return;
            settings = SiteConfigStore.Load();

            var endpointId = Guid.Empty;
            var endpointStatus = string.Empty;
            Guid? backendProfileIdentity = null;
            if (settings.ExecutionEndpointId != Guid.Empty)
            {
                Console.WriteLine("Buoc 5/7: Kiem tra Execution Endpoint");
                Console.WriteLine($"[OK] Edge da co Execution Endpoint ID: {settings.ExecutionEndpointId:D}");
                var current = api.GetEndpoint(kioskId, settings.ExecutionEndpointId);
                if (!current.Success)
                {
                    Console.WriteLine("[ERROR] " + current.Message);
                    return;
                }
                if (string.Equals(current.Status, "Retired", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("[WARN] Execution Endpoint cu da Retired; se tim FullEdge dang cho cau hinh.");
                    settings.ExecutionEndpointId = Guid.Empty;
                    settings.FullEdgeRuntimeId = Guid.Empty;
                    SiteConfigStore.Save(settings);
                }
                else
                {
                    endpointId = settings.ExecutionEndpointId;
                    endpointStatus = current.Status;
                    backendProfileIdentity = current.ProfileIdentity;
                }
            }

            if (settings.ExecutionEndpointId == Guid.Empty)
            {
                Console.WriteLine("Buoc 5/7: Dang ky Execution Endpoint");
                if (!ExecutionEndpointRegistrationApi.TryBuildEndpointCodeFromLocation(
                    settings.EdgeLocationName, out var endpointCode, out var endpointCodeError))
                {
                    Console.WriteLine("[ERROR] " + endpointCodeError);
                    return;
                }
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
            Console.WriteLine("Buoc 6/7: Provision mTLS");
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
            Console.WriteLine("Buoc 7/7: Kich hoat kiosk va kiem tra ket noi");
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

            var kiosks = api.ListAccessibleOperationalKiosks(out var listError);
            if (!string.IsNullOrWhiteSpace(listError))
            {
                Console.WriteLine("[ERROR] " + listError);
                return Guid.Empty;
            }

            if (kiosks.Count == 0)
            {
                Console.WriteLine("[ERROR] Tai khoan Org khong co kiosk hop le (Active/Provisioning va Operational).");
                return Guid.Empty;
            }

            BackendKiosk selectedKiosk;
            if (kiosks.Count == 1)
            {
                selectedKiosk = kiosks[0];
                Console.WriteLine($"[OK] Tu dong chon kiosk duy nhat: {selectedKiosk.Code} - {selectedKiosk.Name}");
            }
            else
            {
                Console.WriteLine($"Tai khoan co {kiosks.Count} kiosk hop le:");
                for (var i = 0; i < kiosks.Count; i++)
                {
                    Console.WriteLine($"  {i + 1}. {kiosks[i].Code} | {kiosks[i].Name} ({kiosks[i].Status})");
                }

                while (true)
                {
                    var promptDefault = !string.IsNullOrWhiteSpace(settings.KioskCode) ? $" [{settings.KioskCode}]" : string.Empty;
                    Console.Write($"Chon kiosk [1-{kiosks.Count}] hoac nhap KIOSK_CODE{promptDefault}: ");
                    var input = Console.ReadLine()?.Trim();
                    if (string.IsNullOrWhiteSpace(input) && !string.IsNullOrWhiteSpace(settings.KioskCode))
                    {
                        input = settings.KioskCode;
                    }

                    if (string.IsNullOrWhiteSpace(input))
                    {
                        Console.WriteLine("[ERROR] Vui long chon kiosk.");
                        continue;
                    }

                    if (int.TryParse(input, out var index) && index >= 1 && index <= kiosks.Count)
                    {
                        selectedKiosk = kiosks[index - 1];
                        break;
                    }

                    var matched = kiosks.FirstOrDefault(k => string.Equals(k.Code, input, StringComparison.OrdinalIgnoreCase));
                    if (matched != null)
                    {
                        selectedKiosk = matched;
                        break;
                    }

                    Console.WriteLine("[ERROR] Lua chon khong hop le. Vui long chon lai.");
                }
            }

            if (selectedKiosk.Id == Guid.Empty)
            {
                Console.WriteLine("[ERROR] Kiosk duoc chon khong co KioskId hop le.");
                return Guid.Empty;
            }

            settings.KioskId = selectedKiosk.Id;
            settings.KioskCode = selectedKiosk.Code;
            SiteConfigStore.Save(settings);

            Console.WriteLine("[OK] Da xac dinh Kiosk thanh cong.");
            Console.WriteLine($"Kiosk Code: {selectedKiosk.Code} ({selectedKiosk.Name})");
            Console.WriteLine($"KioskId: {selectedKiosk.Id:D}");
            return selectedKiosk.Id;
        }
    }
}
