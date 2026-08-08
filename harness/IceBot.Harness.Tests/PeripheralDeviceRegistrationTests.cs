using System;
using System.Collections.Generic;
using System.Net;
using IceBot.Api;
using IceBot.Config;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class PeripheralDeviceRegistrationTests
    {
        [Fact]
        public void ParseRegistrationResponse_ReturnsBackendDeviceId()
        {
            var deviceId = Guid.NewGuid();
            var result = PeripheralDeviceApi.ParseRegistrationResponse(
                HttpStatusCode.Created,
                $"{{\"succeeded\":true,\"message\":\"created\",\"data\":{{\"id\":\"{deviceId:D}\"}}}}");

            Assert.True(result.Success);
            Assert.Equal(deviceId, result.DeviceId);
        }

        [Fact]
        public void ParseRegistrationResponse_UsesBackendErrorMessage()
        {
            var result = PeripheralDeviceApi.ParseRegistrationResponse(
                HttpStatusCode.Conflict,
                "{\"succeeded\":false,\"message\":\"Device code already exists.\"}");

            Assert.False(result.Success);
            Assert.Contains("already exists", result.Message);
        }

        [Fact]
        public void MachineDeviceIds_RoundTripByStableMachineType()
        {
            var expected = Guid.NewGuid();
            var serialized = SiteConfigStore.SerializeMachineDeviceIds(
                new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ice_cream"] = expected
                });

            var parsed = SiteConfigStore.ParseMachineDeviceIds(serialized);

            Assert.Equal(expected, parsed["ICE_CREAM"]);
        }
    }
}
