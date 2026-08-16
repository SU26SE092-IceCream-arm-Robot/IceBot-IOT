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
        // Current hardware identity is reported by Edge after mTLS authentication. These defaults
        // keep the single-FR5 demo usable without making provisioning own robot compatibility.
        public string PrimaryRobotSourceDeviceKey { get; set; } = "arm-primary";
        public string PrimaryRobotRuntimeTargetCode { get; set; } = "FAIRINO_LUA_V1";
        public string PrimaryRobotMachineModelCode { get; set; } = "FR5";
        public long ReportedDevicesSnapshotRevision { get; set; }
        public string ReportedDevicesSnapshotSignature { get; set; } = string.Empty;

        // Operator tokens are separate from the Edge device credential. StorePassword remains
        // only for migration from older config and is cleared after a successful login.
        public string StoreAccount { get; set; } = string.Empty;
        public string StorePassword { get; set; } = string.Empty;
        public string OperatorAccessToken { get; set; } = string.Empty;
        public string OperatorRefreshToken { get; set; } = string.Empty;

        // The Edge PC and kiosk are the same physical machine. KioskId is returned by BE when
        // this machine is registered. KioskCode is the unique code printed on its physical case;
        // the technician enters it once and Edge persists it together with the returned KioskId.
        public string KioskCode { get; set; } = string.Empty;
        public Guid KioskId { get; set; }
        public Dictionary<string, Guid> MachineDeviceIds { get; set; } =
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        public Guid GetMachineDeviceId(string machineType) =>
            MachineDeviceIds.TryGetValue(machineType, out var deviceId) ? deviceId : Guid.Empty;

        // Full Edge execution identity. BE_API_URL must later be set to the private HTTPS URL
        // reachable through NetBird. The PFX password is environment-only and is never persisted.
        public Guid ExecutionEndpointId { get; set; }
        public Guid FullEdgeRuntimeId { get; set; }
        public string ExecutionClientCertificatePath { get; set; } = string.Empty;
        public Guid ActiveConfigurationDeploymentId { get; set; }
        public Guid ActiveConfigurationReleaseId { get; set; }
        public string ActiveConfigurationReleaseChecksum { get; set; } = string.Empty;
        public string ActiveWorkflowDirectory { get; set; } = string.Empty;
        public long ExecutionReportSequence { get; set; }
        public long ExecutionReadinessRevision { get; set; }

        // Development simulator only. This simulates an optional sensor gateway reporting
        // observations for a Cloud-owned dispenser topology; it does not make the dispenser
        // a machine controlled by this Edge process.
        public List<SimulatedInventoryObservationSettings> SimulatedInventoryObservations { get; set; } =
            new List<SimulatedInventoryObservationSettings>();

        // Peripheral machines directly controlled by this Edge over serial, keyed by machine
        // type (for example "cup_dropping" -> "COM3"). This is not the kiosk inventory
        // topology: an independent ice-cream dispenser is configured in Cloud separately.
        // See IceBot.Machines.MachineRegistry.
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

    internal sealed class SimulatedInventoryObservationSettings
    {
        public Guid IngredientDispenserStateId { get; set; }
        public Guid DeviceId { get; set; }
        public string Level { get; set; } = "Full";
    }
}
