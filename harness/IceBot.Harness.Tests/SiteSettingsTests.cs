using System;
using System.Collections.Generic;
using IceBot.Config;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class SiteSettingsTests
    {
        [Fact]
        public void DeviceIdSerialization_IgnoresInvalidEntriesAndIsCaseInsensitive()
        {
            var deviceId = Guid.NewGuid();
            var parsed = SiteConfigStore.ParseMachineDeviceIds(
                $"cup_dropping:{deviceId:D},bad:not-a-guid,:{Guid.NewGuid():D}");

            Assert.Single(parsed);
            Assert.Equal(deviceId, parsed["CUP_DROPPING"]);
        }

        [Fact]
        public void DeviceIdSerialization_SkipsEmptyIdsAndMachineTypes()
        {
            var expected = Guid.NewGuid();
            var serialized = SiteConfigStore.SerializeMachineDeviceIds(new Dictionary<string, Guid>
            {
                ["ice_cream"] = expected,
                ["empty"] = Guid.Empty,
                [""] = Guid.NewGuid()
            });

            Assert.Equal($"ice_cream:{expected:D}", serialized);
        }

        [Fact]
        public void IsConfigured_RequiresBothNetBirdKeyAndPublicUrl()
        {
            Assert.False(new SiteSettings().IsConfigured);
            Assert.False(new SiteSettings { NetBirdSetupKey = "key" }.IsConfigured);
            Assert.True(new SiteSettings { NetBirdSetupKey = "key", PublicUrl = "https://edge" }.IsConfigured);
        }

        [Fact]
        public void MachineLookups_AreCaseInsensitiveAndUnknownReturnsEmpty()
        {
            var settings = new SiteSettings();
            var deviceId = Guid.NewGuid();
            settings.MachineDeviceIds["cup_dropping"] = deviceId;
            settings.MachinePorts["cup_dropping"] = "COM7";

            Assert.Equal(deviceId, settings.GetMachineDeviceId("CUP_DROPPING"));
            Assert.Equal("COM7", settings.GetMachinePort("CUP_DROPPING"));
            Assert.Equal(Guid.Empty, settings.GetMachineDeviceId("unknown"));
            Assert.Equal(string.Empty, settings.GetMachinePort("unknown"));
        }
    }
}
