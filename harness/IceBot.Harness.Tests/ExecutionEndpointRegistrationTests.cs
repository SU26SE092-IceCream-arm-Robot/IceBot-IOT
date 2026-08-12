using System;
using System.Net;
using IceBot.Api;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class ExecutionEndpointRegistrationTests
    {
        [Fact]
        public void BuildEndpointCode_IsStableAndBackendSafe()
        {
            Assert.Equal("EDGE-EDGE-PC-01", ExecutionEndpointRegistrationApi.BuildEndpointCode("edge pc.01"));
        }

        [Fact]
        public void NormalizeKioskCode_PreservesPrintedCodeAndUppercasesIt()
        {
            var valid = ExecutionEndpointRegistrationApi.TryNormalizeKioskCode(
                "  ice-kiosk-001  ", out var code, out var error);

            Assert.True(valid, error);
            Assert.Equal("ICE-KIOSK-001", code);
        }

        [Theory]
        [InlineData("")]
        [InlineData("A")]
        [InlineData("BAD=CODE")]
        public void NormalizeKioskCode_RejectsValuesUnsafeForBackendOrLocalConfig(string input)
        {
            Assert.False(ExecutionEndpointRegistrationApi.TryNormalizeKioskCode(input, out _, out _));
        }

        [Fact]
        public void ParseKioskCreateResponse_ReturnsBackendKioskId()
        {
            var kioskId = Guid.NewGuid();
            var result = ExecutionEndpointRegistrationApi.ParseKioskCreateResponse(
                HttpStatusCode.Created,
                $"{{\"succeeded\":true,\"message\":\"created\",\"data\":{{\"id\":\"{kioskId:D}\",\"code\":\"KIOSK-ABC\"}}}}");

            Assert.True(result.Success);
            Assert.True(result.Created);
            Assert.Equal(kioskId, result.KioskId);
        }

        [Fact]
        public void ParseCreateResponse_ReturnsExecutionEndpointId()
        {
            var endpointId = Guid.NewGuid();
            var kioskId = Guid.NewGuid();
            var result = ExecutionEndpointRegistrationApi.ParseCreateResponse(
                HttpStatusCode.Created,
                $"{{\"succeeded\":true,\"message\":\"created\",\"data\":{{\"id\":\"{endpointId:D}\",\"kioskId\":\"{kioskId:D}\",\"endpointCode\":\"EDGE-PC\",\"status\":\"Provisioning\"}}}}");

            Assert.True(result.Success);
            Assert.True(result.Created);
            Assert.Equal(endpointId, result.EndpointId);
            Assert.Equal("Provisioning", result.Status);
        }

        [Fact]
        public void ParseCreateResponse_UsesBackendError()
        {
            var result = ExecutionEndpointRegistrationApi.ParseCreateResponse(
                HttpStatusCode.Forbidden,
                "{\"succeeded\":false,\"message\":\"Access denied.\"}");

            Assert.False(result.Success);
            Assert.Contains("Access denied", result.Message);
        }

        [Fact]
        public void ParseManagementResponse_ReturnsActiveProfileIdentity()
        {
            var profileIdentity = Guid.NewGuid();
            var result = ExecutionEndpointRegistrationApi.ParseManagementResponse(
                HttpStatusCode.OK,
                $"{{\"succeeded\":true,\"data\":{{\"status\":\"Active\",\"profileIdentity\":\"{profileIdentity:D}\"}}}}");

            Assert.True(result.Success);
            Assert.Equal("Active", result.Status);
            Assert.Equal(profileIdentity, result.ProfileIdentity);
        }

        [Fact]
        public void ParseKioskManagementResponse_RequiresActiveOperationalKioskData()
        {
            var result = ExecutionEndpointRegistrationApi.ParseKioskManagementResponse(
                HttpStatusCode.OK,
                "{\"succeeded\":true,\"data\":{\"status\":\"Active\",\"operationalState\":\"Operational\"}}");

            Assert.True(result.Success);
            Assert.Equal("Active", result.Status);
            Assert.Equal("Operational", result.OperationalState);
        }
    }
}
