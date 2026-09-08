using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using IceBot.Config;
using IceBot.Machines;
using IceBot.Robot;
using IceBot.Workflow.Execution;

namespace IceBot.Workflow
{
    internal static class OrderExecutionPreflight
    {
        internal static void ValidateArtifacts(DurableProductionUnit unit, string directory)
        {
            foreach (var artifact in unit.Artifacts)
            {
                using (var stream = File.OpenRead(Path.Combine(directory, artifact.ScriptFileName)))
                using (var sha = SHA256.Create())
                {
                    var checksum = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                    if (!string.Equals(checksum, artifact.ArtifactChecksum, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Stored Lua checksum mismatch: " + artifact.ScriptFileName);
                }
            }
        }

        internal static void Validate(DurableProductionUnit unit)
        {
            var directory = AppConfig.GetWorkflowDirectory();
            ValidateArtifacts(unit, directory);
            var plan = WorkflowPlanComposer.Compose(directory, unit.Artifacts.Select(item => item.ScriptFileName).ToArray());
            if (AppConfig.RobotExecutionMode == RobotExecutionMode.Simulated) return;
            var safety = FairinoSafetyProbe.Read(AppConfig.RobotIp);
            if (safety.Safety != "Safe")
                throw new InvalidOperationException("Production blocked: " + safety.Detail);
            foreach (var machineType in plan.Instructions.Where(item => item.Kind == WorkflowInstructionKind.TriggerDevice)
                .Select(item => item.MachineType).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!MachineRegistry.TryGetModuleByMachineType(machineType, out var module) || !(module is IMachineTrigger trigger))
                    throw new InvalidOperationException("Missing peripheral driver: " + machineType);
                trigger.TestConnection(SiteConfigStore.Load().GetMachinePort(module.MachineType));
            }
        }
    }
}
