using System;
using System.Collections.Generic;

namespace IceBot.Config
{
    internal sealed class SiteSettings
    {
        // Ingress tunnel — NextBird replaces the old DuckDNS + Cloudflare Tunnel setup. IceBot
        // only needs the setup key; NextBird handles opening the path in and assigning PublicUrl.
        public string NextBirdSetupKey { get; set; } = string.Empty;
        public string PublicUrl { get; set; } = string.Empty;
        public string BeApiUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string RobotIp { get; set; } = AppConfig.DefaultRobotIp;

        // Per-store BE login (see IceBot.Api.StoreAuth). StoreAccount/StorePassword are the
        // store's own credentials; BeSessionKey is the key BE returns on successful login —
        // this is distinct from ApiKey above (ApiKey authenticates inbound BE->Edge requests;
        // BeSessionKey is what Edge attaches to outbound Edge->BE requests once BE is real).
        public string StoreAccount { get; set; } = string.Empty;
        public string StorePassword { get; set; } = string.Empty;
        public string BeSessionKey { get; set; } = string.Empty;

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
            !string.IsNullOrWhiteSpace(NextBirdSetupKey)
            && !string.IsNullOrWhiteSpace(PublicUrl);
    }
}
