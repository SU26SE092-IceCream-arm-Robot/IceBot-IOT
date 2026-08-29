using System;
using System.Collections.Generic;
using System.IO;
using IceBot.Api;
using IceBot.Config;
using IceBot.Robot;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class SiteSettingsTests
    {
        [Fact]
        public void ReadinessCapabilityProfile_SimulatedModeReportsAvailableRobotArm()
        {
            var capabilities = EdgeReadinessCapabilityProfile.For(RobotExecutionMode.Simulated);

            var capability = Assert.Single(capabilities);
            Assert.Equal("ROBOT_ARM", capability.CapabilityCode);
            Assert.Equal("ARM_PRIMARY", capability.WorkcellCode);
            Assert.True(capability.IsAvailable);
        }

        [Fact]
        public void ReadinessCapabilityProfile_PhysicalModeDoesNotClaimUnverifiedCapability()
        {
            Assert.Empty(EdgeReadinessCapabilityProfile.For(RobotExecutionMode.Fairino));
        }

        [Fact]
        public void ReadinessSafetyProfile_UsesSafeForSimulationOrVerifiedPhysicalTelemetry()
        {
            Assert.Equal("Safe", EdgeReadinessSafetyProfile.For(RobotExecutionMode.Simulated));
            Assert.Equal("Unknown", EdgeReadinessSafetyProfile.For(RobotExecutionMode.Fairino));
            Assert.Equal("Safe", EdgeReadinessSafetyProfile.For(RobotExecutionMode.Fairino, FairinoSafetySnapshot.Safe()));
            Assert.Equal("Unsafe", EdgeReadinessSafetyProfile.For(RobotExecutionMode.Fairino, FairinoSafetySnapshot.Unsafe("E-stop")));
        }

        [Fact]
        public void ReadinessCapabilityProfile_PhysicalModeReportsRobotOnlyWhenSafetyIsVerified()
        {
            Assert.Empty(EdgeReadinessCapabilityProfile.For(RobotExecutionMode.Fairino, "Unknown"));
            Assert.Empty(EdgeReadinessCapabilityProfile.For(RobotExecutionMode.Fairino, "Unsafe"));

            var capability = Assert.Single(EdgeReadinessCapabilityProfile.For(RobotExecutionMode.Fairino, "Safe"));
            Assert.Equal("ROBOT_ARM", capability.CapabilityCode);
            Assert.True(capability.IsAvailable);
        }

        [Fact]
        public void ActiveWorkflowPeripheralDiscovery_UsesTriggerDeviceMachineTypesInsteadOfArtifactFileNames()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-peripheral-discovery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, Guid.NewGuid() + ".lua"),
                    @"TriggerDevice(""bt_cup_l90"", ""ON"")
TriggerDevice(""ice_cream"", ""ON"")");

                var machineTypes = IceBot.Workflow.Execution.ActiveWorkflowPeripheralDiscovery.DiscoverMachineTypes(directory);

                Assert.Equal(new[] { "bt_cup_l90", "ice_cream" }, machineTypes);
            }
            finally { Directory.Delete(directory, true); }
        }

        [Fact]
        public void DeviceIdSerialization_IgnoresInvalidEntriesAndIsCaseInsensitive()
        {
            var deviceId = Guid.NewGuid();
            var parsed = SiteConfigStore.ParseMachineDeviceIds(
                $"bt_cup_l90:{deviceId:D},bad:not-a-guid,:{Guid.NewGuid():D}");

            Assert.Single(parsed);
            Assert.Equal(deviceId, parsed["BT_CUP_L90"]);
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
        public void SetupCompletion_IsIndependentFromDeploymentReadiness()
        {
            var directory = Path.Combine(Path.GetTempPath(), "icebot-setup-status-" + Guid.NewGuid().ToString("N"));
            var certificatePath = Path.Combine(directory, "edge.pfx");
            var workflowDirectory = Path.Combine(directory, "workflow");
            Directory.CreateDirectory(workflowDirectory);
            File.WriteAllText(certificatePath, "test");
            try
            {
                var settings = new SiteSettings
                {
                    KioskId = Guid.NewGuid(),
                    ExecutionEndpointId = Guid.NewGuid(),
                    FullEdgeRuntimeId = Guid.NewGuid(),
                    BeApiUrl = "https://edge.internal",
                    NetBirdSetupKey = "setup-key",
                    ExecutionClientCertificatePath = certificatePath,
                    RobotIp = "192.168.58.2",
                    PrimaryRobotSourceDeviceKey = "arm-primary",
                    PrimaryRobotRuntimeTargetCode = "FAIRINO_LUA_V1",
                    PrimaryRobotMachineModelCode = "FR5"
                };

                Assert.True(EdgeSetupReadiness.IsSetupComplete(settings));
                Assert.False(EdgeSetupReadiness.IsProductionReady(settings));

                settings.ActiveConfigurationDeploymentId = Guid.NewGuid();
                settings.ActiveConfigurationReleaseId = Guid.NewGuid();
                settings.ActiveConfigurationReleaseChecksum = "sha256";
                settings.ActiveWorkflowDirectory = workflowDirectory;

                Assert.True(EdgeSetupReadiness.IsProductionReady(settings));
            }
            finally { Directory.Delete(directory, true); }
        }
        [Fact]
        public void MachineLookups_AreCaseInsensitiveAndUnknownReturnsEmpty()
        {
            var settings = new SiteSettings();
            var deviceId = Guid.NewGuid();
            settings.MachineDeviceIds["bt_cup_l90"] = deviceId;
            settings.MachinePorts["bt_cup_l90"] = "COM7";

            Assert.Equal(deviceId, settings.GetMachineDeviceId("BT_CUP_L90"));
            Assert.Equal("COM7", settings.GetMachinePort("BT_CUP_L90"));
            Assert.Equal(Guid.Empty, settings.GetMachineDeviceId("unknown"));
            Assert.Equal(string.Empty, settings.GetMachinePort("unknown"));
        }

        [Fact]
        public void ReportedDevicesSnapshot_ReusesRevisionAndObservedAtForSameContent()
        {
            var settings = new SiteSettings();
            var firstTime = DateTimeOffset.Parse("2026-08-17T10:00:00Z");
            var secondTime = firstTime.AddMinutes(5);

            var first = SiteConfigStore.ResolveReportedDevicesSnapshotVersion(
                settings, "arm-primary:FR5", firstTime, out var firstChanged);
            var second = SiteConfigStore.ResolveReportedDevicesSnapshotVersion(
                settings, "arm-primary:FR5", secondTime, out var secondChanged);

            Assert.True(firstChanged);
            Assert.False(secondChanged);
            Assert.Equal(first.Revision, second.Revision);
            Assert.Equal(first.ObservedAt, second.ObservedAt);
        }

        [Fact]
        public void ReportedDevicesSnapshot_IncrementsVersionWhenContentChanges()
        {
            var settings = new SiteSettings();
            var firstTime = DateTimeOffset.Parse("2026-08-17T10:00:00Z");
            var secondTime = firstTime.AddMinutes(5);
            var first = SiteConfigStore.ResolveReportedDevicesSnapshotVersion(
                settings, "arm-primary:FR5", firstTime, out _);

            var second = SiteConfigStore.ResolveReportedDevicesSnapshotVersion(
                settings, "arm-primary:FR10", secondTime, out var changed);

            Assert.True(changed);
            Assert.Equal(first.Revision + 1, second.Revision);
            Assert.Equal(secondTime, second.ObservedAt);
        }

        [Fact]
        public void ReportedDevicesSnapshot_ConflictRecoveryAlwaysCreatesNextRevision()
        {
            var settings = new SiteSettings
            {
                ReportedDevicesSnapshotRevision = 2,
                ReportedDevicesSnapshotSignature = "stale-content",
                ReportedDevicesSnapshotObservedAt = DateTimeOffset.Parse("2026-08-17T10:00:00Z")
            };
            var recoveryTime = DateTimeOffset.Parse("2026-08-17T10:05:00Z");

            var replacement = SiteConfigStore.CreateNextReportedDevicesSnapshotVersion(
                settings, "arm-primary::FAIRINO_LUA_V1:FR5", recoveryTime);

            Assert.Equal(3, replacement.Revision);
            Assert.Equal(recoveryTime, replacement.ObservedAt);
            Assert.Equal("arm-primary::FAIRINO_LUA_V1:FR5", settings.ReportedDevicesSnapshotSignature);
        }

        [Fact]
        public void ReportedDevicesSnapshot_NormalizesTimestampToPostgresMicrosecondPrecision()
        {
            var value = new DateTimeOffset(2026, 8, 17, 10, 5, 0, TimeSpan.Zero).AddTicks(1_234_567);

            var normalized = SiteConfigStore.NormalizeSnapshotObservedAt(value);

            Assert.Equal(0, normalized.Ticks % 10);
            Assert.Equal(value.ToUniversalTime().Ticks - 7, normalized.Ticks);
        }
    }
}
