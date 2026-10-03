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
            Assert.Equal("EDGE-PC-01", ExecutionEndpointRegistrationApi.BuildEndpointCode("edge pc.01"));
        }

        [Theory]
        [InlineData("Betea", "Betea")]
        [InlineData("Betea Store 01", "Betea-Store-01")]
        public void BuildEndpointCodeFromLocation_UsesEnteredLocation(string location, string expected)
        {
            var valid = ExecutionEndpointRegistrationApi.TryBuildEndpointCodeFromLocation(
                location, out var code, out var error);

            Assert.True(valid, error);
            Assert.Equal(expected, code);
        }

        [Theory]
        [InlineData("")]
        [InlineData("---")]
        [InlineData("BAD=LOCATION")]
        public void BuildEndpointCodeFromLocation_RejectsInvalidValues(string location)
        {
            Assert.False(ExecutionEndpointRegistrationApi.TryBuildEndpointCodeFromLocation(
                location, out _, out _));
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
        public void SelectUnambiguousProvisioningFullEdgeEndpoint_ReusesSingleSeededEndpoint()
        {
            var kioskId = Guid.NewGuid();
            var seededEndpoint = new BackendExecutionEndpoint
            {
                Id = Guid.NewGuid(),
                KioskId = kioskId,
                EndpointCode = "ICEBOT-DEMO-EDGE",
                ExecutionProfile = "FullEdge",
                Status = "Provisioning"
            };

            var selected = ExecutionEndpointRegistrationApi.SelectUnambiguousProvisioningFullEdgeEndpoint(
                new[] { seededEndpoint }, kioskId, out var ambiguous);

            Assert.False(ambiguous);
            Assert.Same(seededEndpoint, selected);
        }

        [Fact]
        public void SelectUnambiguousProvisioningFullEdgeEndpoint_RejectsAmbiguousProvisioningEndpoints()
        {
            var kioskId = Guid.NewGuid();
            var selected = ExecutionEndpointRegistrationApi.SelectUnambiguousProvisioningFullEdgeEndpoint(
                new[]
                {
                    new BackendExecutionEndpoint { Id = Guid.NewGuid(), KioskId = kioskId, ExecutionProfile = "FullEdge", Status = "Provisioning" },
                    new BackendExecutionEndpoint { Id = Guid.NewGuid(), KioskId = kioskId, ExecutionProfile = "FullEdge", Status = "Provisioning" }
                },
                kioskId,
                out var ambiguous);

            Assert.True(ambiguous);
            Assert.Null(selected);
        }

        [Fact]
        public void SelectUnambiguousProvisioningFullEdgeEndpoint_IgnoresOtherProfilesAndStates()
        {
            var kioskId = Guid.NewGuid();
            var selected = ExecutionEndpointRegistrationApi.SelectUnambiguousProvisioningFullEdgeEndpoint(
                new[]
                {
                    new BackendExecutionEndpoint { Id = Guid.NewGuid(), KioskId = kioskId, ExecutionProfile = "LowCostController", Status = "Provisioning" },
                    new BackendExecutionEndpoint { Id = Guid.NewGuid(), KioskId = kioskId, ExecutionProfile = "FullEdge", Status = "Active" }
                },
                kioskId,
                out var ambiguous);

            Assert.False(ambiguous);
            Assert.Null(selected);
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
        [Theory]
        [InlineData("Active", "Operational", true, true)]
        [InlineData("Provisioning", "Operational", true, false)]
        [InlineData("active", "operational", true, true)]
        [InlineData("provisioning", "operational", true, false)]
        [InlineData("Disabled", "Operational", false, false)]
        [InlineData("Retired", "Operational", false, false)]
        [InlineData("Active", "OutOfService", false, false)]
        [InlineData("Provisioning", "OutOfService", false, false)]
        [InlineData("Active", "Maintenance", false, false)]
        public void BackendKiosk_EligibilityAndOperationalState_AreAccurate(
            string status,
            string operationalState,
            bool expectedEligible,
            bool expectedOperational)
        {
            var kiosk = new BackendKiosk
            {
                Status = status,
                OperationalState = operationalState
            };

            Assert.Equal(expectedEligible, kiosk.IsEligibleForSetup);
            Assert.Equal(expectedOperational, kiosk.IsOperational);
        }

        [Fact]
        public void ShutdownStatusPayload_MatchesBackendContract()
        {
            var executorId = Guid.NewGuid();
            var reportedAt = DateTimeOffset.UtcNow;

            var readinessJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                sourceExecutorId = executorId,
                stateRevision = 1L,
                executorReportedAt = reportedAt,
                readiness = "NotReady",
                activity = "Idle",
                safety = "Unknown",
                physicalOutputState = "Unknown",
                faultCode = "EDGE_SHUTDOWN",
                localPersistenceHealth = new
                {
                    storageWritable = true,
                    freeSpaceBytes = 1024L * 1024 * 1024,
                    minimumRequiredFreeSpaceBytes = 256L * 1024 * 1024,
                    localDatabaseHealth = "Healthy",
                    pendingEventCount = 0,
                    maximumPendingEventCount = 1000
                },
                capabilities = new object[0]
            });

            Assert.Contains("\"readiness\":\"NotReady\"", readinessJson);
            Assert.Contains("\"activity\":\"Idle\"", readinessJson);
            Assert.Contains("\"faultCode\":\"EDGE_SHUTDOWN\"", readinessJson);

            var heartbeatJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                originNodeId = executorId,
                heartbeatSequence = 123456789L,
                reportedAt = reportedAt,
                status = "Offline",
                robotStatus = "Stopped",
                networkStatus = "Offline",
                appVersion = "1.0.0.0",
                pendingSyncEventCount = 0
            });

            Assert.Contains("\"status\":\"Offline\"", heartbeatJson);
            Assert.Contains("\"robotStatus\":\"Stopped\"", heartbeatJson);
        }
    }
}
