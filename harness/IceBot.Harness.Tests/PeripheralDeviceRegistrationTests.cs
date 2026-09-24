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
        public void ParseDeviceListResponse_ReturnsExistingKioskDevices()
        {
            var deviceId = Guid.NewGuid();
            var result = PeripheralDeviceApi.ParseDeviceListResponse(
                HttpStatusCode.OK,
                $"{{\"succeeded\":true,\"data\":[{{\"id\":\"{deviceId:D}\",\"kioskId\":\"{Guid.NewGuid():D}\",\"deviceTypeId\":1,\"deviceTypeCode\":\"SOFT-SERVE-MACHINE\",\"code\":\"ice_cream\",\"name\":\"May lam kem\",\"status\":\"Provisioning\"}}]}}");

            Assert.True(result.Success);
            Assert.Single(result.Devices);
            Assert.Equal(deviceId, result.Devices[0].Id);
            Assert.Equal("ice_cream", result.Devices[0].Code);
            Assert.Equal("Provisioning", result.Devices[0].Status);
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
