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
    }
}
