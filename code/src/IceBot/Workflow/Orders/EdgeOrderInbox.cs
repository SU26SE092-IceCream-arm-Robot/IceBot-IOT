using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IceBot.Workflow
{
    internal sealed class ReceivedOrderCommand
    {
        public int SchemaVersion { get; set; }
        public Guid CommandId { get; set; }
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public Guid KioskId { get; set; }
        public Guid TargetExecutionEndpointId { get; set; }
        public Guid ConfigurationReleaseId { get; set; }
        public string ReleaseChecksum { get; set; } = string.Empty;
        public long? ActiveSetVersion { get; set; }
        public string? ActiveSetChecksum { get; set; }
        public DateTimeOffset CommandExpiryAt { get; set; }
        public DateTimeOffset ReceivedAt { get; set; }
        public List<ReceivedOrderLine> OrderLines { get; set; } = new List<ReceivedOrderLine>();
        public string PayloadJson { get; set; } = string.Empty;

        public int TotalQuantity => OrderLines.Sum(line => line.Quantity);
    }

    internal sealed class ReceivedOrderLine
    {
        public Guid OrderItemId { get; set; }
        public int Quantity { get; set; }
        public int ProductionUnitStartNo { get; set; } = 1;
        public List<ReceivedRobotProgram> RobotPrograms { get; set; } = new List<ReceivedRobotProgram>();
    }

    internal sealed class ReceivedRobotProgram
    {
        public int BindingOrder { get; set; }
        public List<ReceivedArtifact> Artifacts { get; set; } = new List<ReceivedArtifact>();
    }

    internal sealed class ReceivedArtifact
    {
        public Guid RobotArtifactId { get; set; }
        public int RunOrder { get; set; }
        public string ArtifactChecksum { get; set; } = string.Empty;
        public string RuntimeTargetCode { get; set; } = string.Empty;
        public string MachineModelCode { get; set; } = string.Empty;
        public string ScriptFileName => RobotArtifactId.ToString("D") + ".lua";
    }

    internal static class EdgeOrderInbox
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            WriteIndented = true
        };

        public static ReceivedOrderCommand Validate(Guid commandId, string payloadJson)
        {
            if (commandId == Guid.Empty) throw new FormatException("ExecuteOrder commandId is missing.");
            ReceivedOrderCommand? payload;
            try { payload = JsonSerializer.Deserialize<ReceivedOrderCommand>(payloadJson, JsonOptions); }
            catch (JsonException ex) { throw new FormatException("ExecuteOrder payload is invalid JSON.", ex); }
            if (payload == null) throw new FormatException("ExecuteOrder payload is empty.");
            if (payload.SchemaVersion != 3 && payload.SchemaVersion != 4 && payload.SchemaVersion != 5)
                throw new FormatException($"Unsupported ExecuteOrder schema version: {payload.SchemaVersion}.");
            if (payload.CommandId != commandId) throw new FormatException("ExecuteOrder command identity does not match its envelope.");
            if (payload.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(payload.OrderNumber) ||
                payload.KioskId == Guid.Empty || payload.TargetExecutionEndpointId == Guid.Empty ||
                payload.ConfigurationReleaseId == Guid.Empty || string.IsNullOrWhiteSpace(payload.ReleaseChecksum) ||
                payload.CommandExpiryAt == default)
                throw new FormatException("ExecuteOrder is missing required identity, release, or expiry data.");
            if (payload.ActiveSetVersion.HasValue != !string.IsNullOrWhiteSpace(payload.ActiveSetChecksum))
                throw new FormatException("ExecuteOrder active-set provenance is incomplete.");
            if (payload.OrderLines == null || payload.OrderLines.Count == 0)
                throw new FormatException("ExecuteOrder contains no order lines.");
            foreach (var line in payload.OrderLines)
            {
                if (line.OrderItemId == Guid.Empty || line.Quantity <= 0 || line.ProductionUnitStartNo <= 0)
                    throw new FormatException("ExecuteOrder line identity and quantity must be valid.");
                if (line.RobotPrograms == null || line.RobotPrograms.Count == 0)
                    throw new FormatException("ExecuteOrder line contains no robot programs.");
                foreach (var program in line.RobotPrograms)
                {
                    if (program.BindingOrder <= 0 || program.Artifacts == null || program.Artifacts.Count == 0)
                        throw new FormatException("ExecuteOrder robot program is invalid.");
                    foreach (var artifact in program.Artifacts)
                    {
                        if (artifact.RobotArtifactId == Guid.Empty || artifact.RunOrder <= 0 ||
                            string.IsNullOrWhiteSpace(artifact.ArtifactChecksum))
                            throw new FormatException("ExecuteOrder artifact is invalid.");
                    }
                }
            }

            payload.ReceivedAt = DateTimeOffset.UtcNow;
            payload.PayloadJson = payloadJson;
            return payload;
        }

        public static void ValidateForThisEdge(ReceivedOrderCommand command, Guid kioskId, Guid endpointId,
            Guid releaseId, string releaseChecksum, string workflowDirectory, DateTimeOffset now)
        {
            if (command.KioskId != kioskId) throw new OrderRejectionException("WrongKiosk", "Order belongs to another kiosk.");
            if (command.TargetExecutionEndpointId != endpointId) throw new OrderRejectionException("WrongEndpoint", "Order targets another execution endpoint.");
            if (command.ConfigurationReleaseId != releaseId ||
                !string.Equals(command.ReleaseChecksum, releaseChecksum, StringComparison.OrdinalIgnoreCase))
                throw new OrderRejectionException("ReleaseMismatch", "Order configuration release is not active on this Edge.");
            if (command.CommandExpiryAt <= now) throw new OrderRejectionException("CommandExpired", "Order command has expired.");
            if (command.TotalQuantity > Config.AppConfig.MaxProductionUnitsPerOrder)
                throw new OrderRejectionException("OrderQuantityLimit", "An order may contain at most 4 production units.");

            foreach (var artifact in OrderedArtifacts(command))
            {
                var path = Path.Combine(workflowDirectory, artifact.ScriptFileName);
                if (!File.Exists(path)) throw new OrderRejectionException("ArtifactMissing", $"Lua artifact {artifact.RobotArtifactId:D} is not installed.");
                using (var sha = SHA256.Create())
                using (var stream = File.OpenRead(path))
                {
                    var actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
                    if (!string.Equals(actual, artifact.ArtifactChecksum, StringComparison.OrdinalIgnoreCase))
                        throw new OrderRejectionException("ArtifactChecksumMismatch", $"Lua artifact {artifact.RobotArtifactId:D} failed checksum verification.");
                }
            }
        }

        public static IReadOnlyList<ReceivedArtifact> OrderedArtifacts(ReceivedOrderCommand command) =>
            command.OrderLines.SelectMany(line => line.RobotPrograms.OrderBy(program => program.BindingOrder)
                .SelectMany(program => program.Artifacts.OrderBy(artifact => artifact.RunOrder))).ToArray();

        public static bool TryStore(ReceivedOrderCommand command, string inboxDirectory)
        {
            Directory.CreateDirectory(inboxDirectory);
            var destination = Path.Combine(inboxDirectory, command.CommandId.ToString("D") + ".json");
            if (File.Exists(destination)) return false;
            var temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(command, JsonOptions), new UTF8Encoding(false));
                if (File.Exists(destination)) return false;
                File.Move(temporary, destination);
                return true;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    internal sealed class OrderRejectionException : Exception
    {
        public OrderRejectionException(string code, string message) : base(message) { Code = code; }
        public string Code { get; }
    }
}
