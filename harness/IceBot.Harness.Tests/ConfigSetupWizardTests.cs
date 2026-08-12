using System;
using IceBot.Config;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class ConfigSetupWizardTests
    {
        [Fact]
        public void PreserveBackendDeviceIdentities_CopiesKioskAndDeviceMappings()
        {
            var kioskId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var current = new SiteSettings { KioskCode = "ICE-KIOSK-001", KioskId = kioskId };
            current.MachineDeviceIds["ice_cream"] = deviceId;
            var updated = new SiteSettings();

            ConfigSetupWizard.PreserveBackendDeviceIdentities(current, updated);

            Assert.Equal(kioskId, updated.KioskId);
            Assert.Equal("ICE-KIOSK-001", updated.KioskCode);
            Assert.Equal(deviceId, updated.GetMachineDeviceId("ICE_CREAM"));
        }

        [Fact]
        public void PreserveBackendDeviceIdentities_CreatesIndependentDictionary()
        {
            var current = new SiteSettings();
            current.MachineDeviceIds["cup_dropping"] = Guid.NewGuid();
            var updated = new SiteSettings();

            ConfigSetupWizard.PreserveBackendDeviceIdentities(current, updated);
            updated.MachineDeviceIds.Clear();

            Assert.Single(current.MachineDeviceIds);
        }
    }
}
