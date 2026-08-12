using System;
using System.Collections.Generic;

namespace IceBot.Config
{
    internal sealed class SiteSettings
    {
        // Ingress tunnel — NetBird replaces the old DuckDNS + Cloudflare Tunnel setup. IceBot
        // only needs the setup key; NetBird handles opening the path in and assigning PublicUrl.
        public string NetBirdSetupKey { get; set; } = string.Empty;
        public string PublicUrl { get; set; } = string.Empty;
        // Public management API used by a fresh Edge during login/registration. A deployment may
        // still override this with a private HTTPS NetBird address when mTLS must bypass a proxy.
        public string BeApiUrl { get; set; } = "https://api.icebot.io.vn";
        public string ApiKey { get; set; } = string.Empty;
        public string RobotIp { get; set; } = AppConfig.DefaultRobotIp;

        // Operator tokens are separate from the Edge device credential. StorePassword remains
        // only for migration from older config and is cleared after a successful login.
        public string StoreAccount { get; set; } = string.Empty;
        public string StorePassword { get; set; } = string.Empty;
        public string OperatorAccessToken { get; set; } = string.Empty;
        public string OperatorRefreshToken { get; set; } = string.Empty;

        // The Edge PC and kiosk are the same physical machine. KioskId is returned by BE when
        // this machine is registered and is reused locally on subsequent initialization runs.
        public Guid KioskId { get; set; }
        public Dictionary<string, Guid> MachineDeviceIds { get; set; } =
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        public Guid GetMachineDeviceId(string machineType) =>
            MachineDeviceIds.TryGetValue(machineType, out var deviceId) ? deviceId : Guid.Empty;

        // Full Edge execution identity. BE_API_URL must later be set to the private HTTPS URL
        // reachable through NetBird. The PFX password is environment-only and is never persisted.
        public Guid ExecutionEndpointId { get; set; }
        public string ExecutionClientCertificatePath { get; set; } = string.Empty;
        public Guid ActiveConfigurationDeploymentId { get; set; }
        public Guid ActiveConfigurationReleaseId { get; set; }
        public string ActiveConfigurationReleaseChecksum { get; set; } = string.Empty;
        public long ExecutionReportSequence { get; set; }

        // Peripheral machines wired directly to this PC over serial, keyed by machine type
        // (e.g. "cup_dropping" -> "COM3"). See IceBot.Machines.MachineRegistry.
        public Dictionary<string, string> MachinePorts { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string GetMachinePort(string machineType) =>
            MachinePorts.TryGetValue(machineType, out var port) ? port : string.Empty;

        // Step names (.lua file, without extension) provisioned from BE so far — accumulated
        // across every successful WorkflowProvisioner run for this store, deduped. This is what
        // "Test may > 2 Test ket noi may ngoai vi" iterates to know which peripheral machines
        // this specific store actually has (resolved via MachineRegistry.TryGetModule), instead
        // of testing every machine type ever coded into MachineRegistry.Modules.
        public List<string> ProvisionedSteps { get; set; } = new List<string>();

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(NetBirdSetupKey)
            && !string.IsNullOrWhiteSpace(PublicUrl);
    }
}
