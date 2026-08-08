using System;
using IceBot.Api;
using IceBot.Machines;

namespace IceBot.Config
{
    internal static class PeripheralDeviceRegistrationWizard
    {
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
            var kioskText = Prompt("KioskId", settings.KioskId == Guid.Empty ? string.Empty : settings.KioskId.ToString("D"));
            if (!Guid.TryParse(kioskText, out var kioskId) || kioskId == Guid.Empty)
            {
                Console.WriteLine("[ERROR] KioskId khong hop le.");
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
