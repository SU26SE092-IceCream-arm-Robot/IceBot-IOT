using System;
using System.Linq;
using IceBot.Config;
using IceBot.Machines;
using IceBot.Robot.Hardware;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class RobotDeviceDiscoveryTests
    {
        [Fact]
        public void ConfiguredDiscovery_UsesConfiguredProfileAndOptionalBackendDeviceMapping()
        {
            var deviceId = Guid.NewGuid();
            var settings = new SiteSettings
            {
                PrimaryRobotSourceDeviceKey = "arm-left",
                PrimaryRobotRuntimeTargetCode = "FAIRINO_LUA_V1",
                PrimaryRobotMachineModelCode = "FR3"
            };
            settings.MachineDeviceIds["arm-left"] = deviceId;

            var device = Assert.Single(new ConfiguredRobotDeviceDiscovery().Discover(settings));

            Assert.Equal("arm-left", device.SourceDeviceKey);
            Assert.Equal(deviceId, device.DeviceId);
            Assert.Equal("FAIRINO_LUA_V1", device.RuntimeTargetCode);
            Assert.Equal("FR3", device.MachineModelCode);
        }

        [Fact]
        public void ConfiguredDiscovery_RejectsIncompleteHardwareConfiguration()
        {
            var settings = new SiteSettings
            {
                PrimaryRobotSourceDeviceKey = "arm-primary",
                PrimaryRobotRuntimeTargetCode = string.Empty,
                PrimaryRobotMachineModelCode = "FR5"
            };

            Assert.Empty(new ConfiguredRobotDeviceDiscovery().Discover(settings));
        }

        [Fact]
        public void ConfiguredPeripheralDiscovery_ReportsMappedTriggerPlugins()
        {
            var settings = new SiteSettings();
            var triggers = MachineRegistry.Modules.OfType<IMachineTrigger>().ToArray();
            Assert.NotEmpty(triggers);

            foreach (var trigger in triggers)
                settings.MachineDeviceIds[trigger.MachineType] = Guid.NewGuid();

            var devices = new ConfiguredPeripheralDeviceDiscovery().Discover(settings);

            Assert.Equal(triggers.Length, devices.Count);
            Assert.All(devices, device => Assert.Contains(triggers, trigger =>
                string.Equals(trigger.MachineType, device.SourceDeviceKey, StringComparison.OrdinalIgnoreCase)));
            Assert.All(devices, device => Assert.Equal("ICEBOT_SIMULATED_PERIPHERAL", device.RuntimeTargetCode));
        }
    }
}
