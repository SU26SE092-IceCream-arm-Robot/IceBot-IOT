using System;
using System.Collections.Generic;
using IceBot.Api;
using IceBot.Machines;

namespace IceBot.Config
{
    internal static class PeripheralDeviceRegistrationWizard
    {
        public static void PrintDeviceList()
        {
            Console.WriteLine();
            Console.WriteLine("=== DANH SACH MAY NGOAI VI ===");
            Console.WriteLine();

            foreach (var error in MachineRegistry.PluginErrors)
                Console.WriteLine($"[PLUGIN ERROR] {error}");
            if (MachineRegistry.PluginErrors.Count > 0) Console.WriteLine();

            var settings = SiteConfigStore.Load();
            for (var i = 0; i < MachineRegistry.Modules.Count; i++)
            {
                var machine = MachineRegistry.Modules[i];
                var deviceId = settings.GetMachineDeviceId(machine.MachineType);
                Console.WriteLine($"{i + 1}. {machine.DisplayName}");
                Console.WriteLine($"   Dinh danh Edge : {machine.MachineType}");
                Console.WriteLine($"   DeviceId BE    : {(deviceId == Guid.Empty ? "CHUA DANG KY" : deviceId.ToString("D"))}");
            }
        }

        public static void Run()
        {
            Console.WriteLine();
            Console.WriteLine("=== DANG KY MAY NGOAI VI VOI BE ===");
            Console.WriteLine("BE_API_URL phai la URL private cua BE tren NetBird.");
            Console.WriteLine();

            for (var i = 0; i < MachineRegistry.Modules.Count; i++)
            {
                var module = MachineRegistry.Modules[i];
                var savedId = SiteConfigStore.Load().GetMachineDeviceId(module.MachineType);
                var saved = savedId == Guid.Empty ? "chua dang ky" : savedId.ToString("D");
                Console.WriteLine($"{i + 1}. {module.DisplayName} ({module.MachineType}) - {saved}");
            }

            if (!TryReadInt("Chon may", out var selection) || selection < 1 || selection > MachineRegistry.Modules.Count)
            {
                Console.WriteLine("[ERROR] Lua chon may khong hop le.");
                return;
            }

            var machine = MachineRegistry.Modules[selection - 1];
            var settings = SiteConfigStore.Load();
            var kioskId = settings.KioskId;
            if (kioskId == Guid.Empty)
            {
                Console.WriteLine("[ERROR] Chua co KioskId da luu. Hay hoan tat khoi tao kiosk voi BE truoc.");
                return;
            }
            if (!TryReadLong("DeviceTypeId", out var deviceTypeId) || deviceTypeId <= 0)
            {
                Console.WriteLine("[ERROR] DeviceTypeId phai lon hon 0.");
                return;
            }

            var modelText = Prompt("DeviceModelId (bo trong neu khong co)", string.Empty);
            Guid? deviceModelId = null;
            if (!string.IsNullOrWhiteSpace(modelText))
            {
                if (!Guid.TryParse(modelText, out var parsedModelId) || parsedModelId == Guid.Empty)
                {
                    Console.WriteLine("[ERROR] DeviceModelId khong hop le.");
                    return;
                }
                deviceModelId = parsedModelId;
            }

            var code = Prompt("Ma may (Code)", machine.MachineType);
            var name = Prompt("Ten may", machine.DisplayName);
            var serial = Prompt("Serial number (bo trong neu khong co)", string.Empty);
            var position = Prompt("Vi tri (bo trong neu khong co)", string.Empty);
            var firmware = Prompt("Firmware (bo trong neu khong co)", string.Empty);

            var result = new PeripheralDeviceApi().Register(kioskId, new PeripheralDeviceRegistration
            {
                DeviceTypeId = deviceTypeId,
                DeviceModelId = deviceModelId,
                Code = code,
                Name = name,
                SerialNumber = EmptyToNull(serial),
                PositionLabel = EmptyToNull(position),
                FirmwareVersion = EmptyToNull(firmware),
                InstalledAt = DateTimeOffset.UtcNow
            });

            if (!result.Success)
            {
                Console.WriteLine("[ERROR] " + result.Message);
                return;
            }

            settings.KioskId = kioskId;
            settings.MachineDeviceIds[machine.MachineType] = result.DeviceId;
            SiteConfigStore.Save(settings);
            Console.WriteLine($"[OK] {result.Message}");
            Console.WriteLine($"Da luu {machine.MachineType} -> DeviceId {result.DeviceId:D}");
        }

        public static void LinkExisting()
        {
            Console.WriteLine();
            Console.WriteLine("=== LIEN KET MAY NGOAI VI DA CO TREN BE ===");
            var settings = SiteConfigStore.Load();
            if (settings.KioskId == Guid.Empty)
            {
                Console.WriteLine("[ERROR] Chua co KioskId da luu. Hay hoan tat khoi tao kiosk voi BE truoc.");
                return;
            }

            for (var i = 0; i < MachineRegistry.Modules.Count; i++)
            {
                var module = MachineRegistry.Modules[i];
                Console.WriteLine($"{i + 1}. {module.DisplayName} ({module.MachineType})");
            }
            if (!TryReadInt("Chon may Edge", out var machineSelection) ||
                machineSelection < 1 || machineSelection > MachineRegistry.Modules.Count)
            {
                Console.WriteLine("[ERROR] Lua chon may khong hop le.");
                return;
            }

            var result = new PeripheralDeviceApi().List(settings.KioskId);
            if (!result.Success)
            {
                Console.WriteLine("[ERROR] " + result.Message);
                return;
            }

            var devices = new List<PeripheralDeviceSummary>();
            foreach (var device in result.Devices)
            {
                if (!string.Equals(device.Status, "Retired", StringComparison.OrdinalIgnoreCase))
                    devices.Add(device);
            }
            if (devices.Count == 0)
            {
                Console.WriteLine("[ERROR] Kiosk khong co thiet bi BE nao co the lien ket.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Thiet bi BE co the lien ket:");
            for (var i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                Console.WriteLine($"{i + 1}. {device.Code} - {device.Name}");
                Console.WriteLine($"   DeviceId: {device.Id:D}");
                Console.WriteLine($"   Type: {device.DeviceTypeCode}, Status: {device.Status}");
            }
            if (!TryReadInt("Chon thiet bi BE", out var deviceSelection) ||
                deviceSelection < 1 || deviceSelection > devices.Count)
            {
                Console.WriteLine("[ERROR] Lua chon thiet bi BE khong hop le.");
                return;
            }

            var machine = MachineRegistry.Modules[machineSelection - 1];
            var selected = devices[deviceSelection - 1];
            settings.MachineDeviceIds[machine.MachineType] = selected.Id;
            SiteConfigStore.Save(settings);
            Console.WriteLine($"[OK] Da lien ket {machine.MachineType} -> DeviceId {selected.Id:D}.");
        }

        private static string Prompt(string label, string current)
        {
            var suffix = string.IsNullOrWhiteSpace(current) ? string.Empty : $" [{current}]";
            Console.Write($"{label}{suffix}: ");
            var input = Console.ReadLine()?.Trim() ?? string.Empty;
            return input.Length == 0 ? current : input;
        }

        private static bool TryReadInt(string label, out int value) =>
            int.TryParse(Prompt(label, string.Empty), out value);

        private static bool TryReadLong(string label, out long value) =>
            long.TryParse(Prompt(label, string.Empty), out value);

        private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
