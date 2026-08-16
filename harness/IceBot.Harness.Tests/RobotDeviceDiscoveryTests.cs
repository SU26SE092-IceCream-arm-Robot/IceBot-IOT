using System;
using IceBot.Config;
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
    }
}
