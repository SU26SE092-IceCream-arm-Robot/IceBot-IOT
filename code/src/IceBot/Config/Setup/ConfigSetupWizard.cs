using System;
using System.Collections.Generic;
using System.Linq;
using IceBot.Api;
using IceBot.Machines;

namespace IceBot.Config
{
    internal static class ConfigSetupWizard
    {
        // NetBird itself only needs the setup key. Everything else in SiteSettings
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

            SiteConfigStore.Save(settings);

            Console.WriteLine();
            Console.WriteLine("[OK] Da luu cau hinh: " + SiteConfigStore.SiteConfigPath);
            Console.WriteLine();
            PrintSummary(settings);
            return connected;
        }



        public static void RunBackendSettings()
        {
            Console.WriteLine();
            Console.WriteLine("=== CAU HINH BACKEND ===");
            Console.WriteLine("Nhan ENTER de giu gia tri hien tai.");
            Console.WriteLine();

            var settings = SiteConfigStore.Load();
            settings.BeApiUrl = Prompt("Backend API URL HTTPS qua NetBird", settings.BeApiUrl);
            SiteConfigStore.Save(settings);
            Console.WriteLine("[OK] Da luu cau hinh Backend.");
        }

        public static void RunRobotSettings()
        {
            Console.WriteLine();
            Console.WriteLine("=== CAU HINH ROBOT ===");
            Console.WriteLine("Nhan ENTER de giu gia tri hien tai.");
            Console.WriteLine();

            var settings = SiteConfigStore.Load();
            settings.RobotIp = Prompt("IP robot Fairino", string.IsNullOrWhiteSpace(settings.RobotIp) ? AppConfig.DefaultRobotIp : settings.RobotIp);
            settings.PrimaryRobotSourceDeviceKey = Prompt("Source device key", settings.PrimaryRobotSourceDeviceKey);
            settings.PrimaryRobotRuntimeTargetCode = Prompt("Runtime target code", settings.PrimaryRobotRuntimeTargetCode);
            settings.PrimaryRobotMachineModelCode = Prompt("Machine model code", settings.PrimaryRobotMachineModelCode);
            SiteConfigStore.Save(settings);
            Console.WriteLine("[OK] Da luu cau hinh robot.");
        }

        public static bool RunEdgeLocationSettings()
        {
            Console.WriteLine();
            Console.WriteLine("=== CAU HINH DIA DIEM EDGE ===");
            Console.WriteLine("Nhap ma Edge/ten dia diem, vi du: Betea -> Betea.");
            Console.WriteLine("Nhan ENTER de giu gia tri hien tai.");
            Console.WriteLine();

            var settings = SiteConfigStore.Load();
            while (true)
            {
                var location = Prompt("Ten dia diem Edge", settings.EdgeLocationName);
                if (ExecutionEndpointRegistrationApi.TryBuildEndpointCodeFromLocation(
                    location, out var endpointCode, out var error))
                {
                    settings.EdgeLocationName = location;
                    SiteConfigStore.Save(settings);
                    Console.WriteLine("[OK] Da luu ten dia diem Edge: " + location);
                    Console.WriteLine("Ma Edge se dang ky: " + endpointCode);
                    return true;
                }

                Console.WriteLine("[ERROR] " + error);
            }
        }

        public static void RunMachinePortSettings()
        {
            Console.WriteLine();
            Console.WriteLine("=== CAU HINH CONG COM MAY NGOAI VI ===");
            Console.WriteLine("Nhan ENTER de giu gia tri hien tai; nhap '-' de xoa cong da luu.");
            Console.WriteLine();

            try
            {
                var availablePorts = System.IO.Ports.SerialPort.GetPortNames();
                if (availablePorts.Length > 0)
                {
                    Console.WriteLine("Cac cong COM dang ket noi tren may tinh: " + string.Join(", ", availablePorts.OrderBy(p => p)));
                }
                else
                {
                    Console.WriteLine("[INFO] Hien tai Windows chua phat hien cong COM nao dang cam.");
                }
            }
            catch
            {
                // Bo qua neu OS khong ho tro lay cong COM
            }
            Console.WriteLine();

            var triggers = MachineRegistry.Modules.OfType<IMachineTrigger>().ToList();
            if (triggers.Count == 0)
            {
                Console.WriteLine("[WARN] Chua co driver may ngoai vi nao duoc cai dat trong thu muc 'drivers/'.");
                Console.WriteLine("       Hay copy package driver (May lam kem, May tha coc...) vao thu muc 'drivers/' truoc.");
                return;
            }

            var settings = SiteConfigStore.Load();
            foreach (var trigger in triggers)
            {
                var current = settings.GetMachinePort(trigger.MachineType);
                var port = Prompt($"COM port {MachineRegistry.GetDisplayName(trigger)}", current);
                if (port == "-") settings.MachinePorts.Remove(trigger.MachineType);
                else if (!string.IsNullOrWhiteSpace(port)) settings.MachinePorts[trigger.MachineType] = port;
            }
            SiteConfigStore.Save(settings);
            Console.WriteLine();
            Console.WriteLine("[OK] Da luu cau hinh cong COM.");
        }

        public static void RunAutoDetectMachinePorts()
        {
            Console.WriteLine();
            Console.WriteLine("=== TU DONG NHAN DIEN CONG COM (REALTIME) ===");
            var triggers = MachineRegistry.Modules.OfType<IMachineTrigger>().ToList();
            if (triggers.Count == 0)
            {
                Console.WriteLine("[WARN] Chua co driver may ngoai vi nao duoc cai dat trong thu muc 'drivers/'.");
                Console.WriteLine("       Hay copy package driver (May lam kem, May tha coc...) vao thu muc 'drivers/' truoc.");
                return;
            }

            var settings = SiteConfigStore.Load();
            Console.WriteLine("Quy trinh nhan dien:");
            Console.WriteLine("  1. Rút cáp USB/Serial của thiết bị ra.");
            Console.WriteLine("  2. Khi hệ thống yêu cầu, cắm lại cáp USB/Serial vào máy tính.");
            Console.WriteLine("  3. Hệ thống sẽ tự phát hiện cổng COM mới và gán cấu hình.");
            Console.WriteLine();

            for (var i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                var name = MachineRegistry.GetDisplayName(trigger);
                var currentPort = settings.GetMachinePort(trigger.MachineType);

                Console.WriteLine($"----------------------------------------");
                Console.WriteLine($"Thiet bi {i + 1}/{triggers.Count}: {name}");
                Console.WriteLine($"Cong hien tai: {(string.IsNullOrEmpty(currentPort) ? "(chua dat)" : currentPort)}");
                Console.WriteLine("Lua chon:");
                Console.WriteLine("  [ENTER]       : Bat dau lang nghe tu dong (cam day vao)");
                Console.WriteLine("  [S] + ENTER   : Bo qua (giu nguyen cong hien tai)");
                Console.WriteLine("  [-] + ENTER   : Xoa cong COM cua thiet bi nay");
                Console.Write("Nhap lua chon: ");
                var choice = Console.ReadLine()?.Trim();

                if (string.Equals(choice, "S", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"-> Da bo qua {name}.");
                    continue;
                }
                if (choice == "-")
                {
                    settings.MachinePorts.Remove(trigger.MachineType);
                    Console.WriteLine($"-> Da xoa cong COM cua {name}.");
                    continue;
                }

                var detectedPort = DetectNewPortInteractive(name);
                if (!string.IsNullOrWhiteSpace(detectedPort))
                {
                    settings.MachinePorts[trigger.MachineType] = detectedPort!;
                    Console.WriteLine($"[OK] Da gan {name} -> {detectedPort}");
                }
                else
                {
                    Console.WriteLine($"[INFO] Khong thay doi cong COM cho {name}.");
                }
            }

            SiteConfigStore.Save(settings);
            Console.WriteLine();
            Console.WriteLine("[OK] Da luu cau hinh cong COM tu dong.");
        }

        private static string? DetectNewPortInteractive(string deviceName)
        {
            HashSet<string> GetCurrentPorts()
            {
                try
                {
                    return new HashSet<string>(System.IO.Ports.SerialPort.GetPortNames(), StringComparer.OrdinalIgnoreCase);
                }
                catch
                {
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
            }

            var initialPorts = GetCurrentPorts();
            Console.WriteLine();
            Console.WriteLine($">> Dang lang nghe... Hay CAM CAP USB/SERIAL cua [{deviceName}] vao may tinh.");
            Console.WriteLine("   (Nhan phim ESC hoac phim bat ky de huy lang nghe neu muon dung lai)");

            var spinner = new[] { '|', '/', '-', '\\' };
            var spinnerIndex = 0;

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.Spacebar)
                    {
                        Console.WriteLine("\n[DA HUY] Da huy lang nghe cong COM.");
                        return null;
                    }
                }

                var currentPorts = GetCurrentPorts();
                var newPorts = currentPorts.Except(initialPorts).ToList();
                if (newPorts.Count > 0)
                {
                    var newPort = newPorts[0];
                    Console.WriteLine($"\n[PHAT HIEN] Tim thay cong COM moi: {newPort}!");
                    return newPort;
                }

                // Neu co cong bi rut ra trong luc cho, cap nhat lai initialPorts de khi cam lai nhan dien duoc
                var removedPorts = initialPorts.Except(currentPorts).ToList();
                if (removedPorts.Count > 0)
                {
                    foreach (var p in removedPorts)
                    {
                        Console.WriteLine($"\n[INFO] Cong {p} da duoc rut ra. Gio ban co the cam lai vao.");
                    }
                    initialPorts = currentPorts;
                }

                Console.Write($"\r   Dang cho thiet bi [{spinner[spinnerIndex++ % spinner.Length]}] (Cong hien tai: {string.Join(",", currentPorts)})...   ");
                System.Threading.Thread.Sleep(300);
            }
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
            Console.WriteLine($"  Backend API URL: {settings.BeApiUrl}");
            Console.WriteLine($"  Execution endpoint: {(settings.ExecutionEndpointId == Guid.Empty ? "(chua dat)" : settings.ExecutionEndpointId.ToString("D"))}");
            Console.WriteLine($"  Full Edge runtime : {(settings.FullEdgeRuntimeId == Guid.Empty ? "(chua dat)" : settings.FullEdgeRuntimeId.ToString("D"))}");
            Console.WriteLine($"  Client certificate: {(string.IsNullOrWhiteSpace(settings.ExecutionClientCertificatePath) ? "(chua dat)" : settings.ExecutionClientCertificatePath)}");
            Console.WriteLine($"  Active deployment: {(settings.ActiveConfigurationDeploymentId == Guid.Empty ? "(chua co)" : settings.ActiveConfigurationDeploymentId.ToString("D"))}");
            Console.WriteLine($"  API key        : {(string.IsNullOrEmpty(settings.ApiKey) ? "(chua dat)" : "****")}");
            Console.WriteLine($"  Robot IP       : {settings.RobotIp}");
            Console.WriteLine($"  Tai khoan cua hang : {(string.IsNullOrEmpty(settings.StoreAccount) ? "(chua dat)" : settings.StoreAccount)}");
            Console.WriteLine($"  Kiosk code          : {(string.IsNullOrEmpty(settings.KioskCode) ? "(chua nhap)" : settings.KioskCode)}");
            Console.WriteLine($"  Edge location       : {(string.IsNullOrEmpty(settings.EdgeLocationName) ? "(chua nhap)" : settings.EdgeLocationName)}");
            Console.WriteLine($"  Da dang nhap BE    : {(string.IsNullOrEmpty(settings.OperatorAccessToken) ? "CHUA (dung Khoi tao Edge trong InitIceBot.exe)" : "ROI")}");
            foreach (var trigger in MachineRegistry.Modules.OfType<IMachineTrigger>())
            {
                var port = settings.GetMachinePort(trigger.MachineType);
                Console.WriteLine($"  {MachineRegistry.GetDisplayName(trigger),-15}: {(string.IsNullOrEmpty(port) ? "(chua cau hinh)" : port)}");
            }
            Console.WriteLine("  Order command  : mTLS pull tu Backend");
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
            return ConsoleSecretReader.Read(current);
        }
    }
}
