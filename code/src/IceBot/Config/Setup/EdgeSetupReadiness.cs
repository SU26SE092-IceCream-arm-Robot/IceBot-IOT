using System;
using System.IO;

namespace IceBot.Config
{
    internal static class EdgeSetupReadiness
    {
        public static bool IsSetupComplete(SiteSettings settings) =>
            settings.KioskId != Guid.Empty &&
            settings.ExecutionEndpointId != Guid.Empty &&
            settings.FullEdgeRuntimeId != Guid.Empty &&
            IsHttpsUrl(settings.BeApiUrl) &&
            !string.IsNullOrWhiteSpace(settings.NetBirdSetupKey) &&
            !string.IsNullOrWhiteSpace(settings.ExecutionClientCertificatePath) &&
            File.Exists(settings.ExecutionClientCertificatePath) &&
            !string.IsNullOrWhiteSpace(settings.RobotIp) &&
            !string.IsNullOrWhiteSpace(settings.PrimaryRobotSourceDeviceKey) &&
            !string.IsNullOrWhiteSpace(settings.PrimaryRobotRuntimeTargetCode) &&
            !string.IsNullOrWhiteSpace(settings.PrimaryRobotMachineModelCode);

        public static bool IsProductionReady(SiteSettings settings) =>
            IsSetupComplete(settings) &&
            settings.ActiveConfigurationDeploymentId != Guid.Empty &&
            settings.ActiveConfigurationReleaseId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(settings.ActiveConfigurationReleaseChecksum) &&
            !string.IsNullOrWhiteSpace(settings.ActiveWorkflowDirectory) &&
            Directory.Exists(settings.ActiveWorkflowDirectory);

        internal static bool IsHttpsUrl(string value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }
}