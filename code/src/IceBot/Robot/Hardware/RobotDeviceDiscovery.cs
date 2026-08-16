using System;
using System.Collections.Generic;
using IceBot.Config;

namespace IceBot.Robot.Hardware
{
    internal sealed class ReportedRobotDevice
    {
        public string SourceDeviceKey { get; set; } = string.Empty;
        public Guid? DeviceId { get; set; }
        public string RuntimeTargetCode { get; set; } = string.Empty;
        public string MachineModelCode { get; set; } = string.Empty;
    }

    internal interface IRobotDeviceDiscovery
    {
        IReadOnlyList<ReportedRobotDevice> Discover(SiteSettings settings);
    }

    // The demo owns one Fairino arm. Keeping this behind discovery avoids baking FR5 into
    // endpoint registration and lets later simulators/controllers report several devices.
    internal sealed class ConfiguredRobotDeviceDiscovery : IRobotDeviceDiscovery
    {
        public IReadOnlyList<ReportedRobotDevice> Discover(SiteSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.PrimaryRobotSourceDeviceKey) ||
                string.IsNullOrWhiteSpace(settings.PrimaryRobotRuntimeTargetCode) ||
                string.IsNullOrWhiteSpace(settings.PrimaryRobotMachineModelCode))
            {
                return Array.Empty<ReportedRobotDevice>();
            }

            var mappedDeviceId = settings.GetMachineDeviceId(settings.PrimaryRobotSourceDeviceKey);
            return new[]
            {
                new ReportedRobotDevice
                {
                    SourceDeviceKey = settings.PrimaryRobotSourceDeviceKey.Trim(),
                    DeviceId = mappedDeviceId == Guid.Empty ? (Guid?)null : mappedDeviceId,
                    RuntimeTargetCode = settings.PrimaryRobotRuntimeTargetCode.Trim(),
                    MachineModelCode = settings.PrimaryRobotMachineModelCode.Trim()
                }
            };
        }
    }
}
