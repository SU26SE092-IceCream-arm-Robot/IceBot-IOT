using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace IceBot.Workflow
{
    internal sealed class ReceivedOrderCommand
    {
        public Guid CommandId { get; set; }
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTimeOffset ReceivedAt { get; set; }
        public string PayloadJson { get; set; } = string.Empty;
    }

    internal static class EdgeOrderInbox
    {
        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions { WriteIndented = true };

        public static ReceivedOrderCommand Validate(Guid commandId, string payloadJson)
        {
            if (commandId == Guid.Empty) throw new FormatException("ExecuteOrder commandId is missing.");
            JsonDocument document;
            try { document = JsonDocument.Parse(payloadJson); }
            catch (JsonException ex) { throw new FormatException("ExecuteOrder payload is invalid JSON.", ex); }

            using (document)
            {
                var root = document.RootElement;
                var schemaVersion = GetRequiredInt32(root, "SchemaVersion");
                if (schemaVersion != 4) throw new FormatException($"Unsupported ExecuteOrder schema version: {schemaVersion}.");
                var payloadCommandId = GetRequiredGuid(root, "CommandId");
                if (payloadCommandId != commandId) throw new FormatException("ExecuteOrder command identity does not match its envelope.");
                var orderId = GetRequiredGuid(root, "OrderId");
                var orderNumber = GetRequiredString(root, "OrderNumber");
                if (!root.TryGetProperty("OrderLines", out var lines) || lines.ValueKind != JsonValueKind.Array || lines.GetArrayLength() == 0)
                    throw new FormatException("ExecuteOrder contains no order lines.");
                foreach (var line in lines.EnumerateArray())
                {
                    if (GetRequiredInt32(line, "Quantity") <= 0) throw new FormatException("ExecuteOrder line quantity must be positive.");
                    if (!line.TryGetProperty("RobotPrograms", out var programs) || programs.ValueKind != JsonValueKind.Array || programs.GetArrayLength() == 0)
                        throw new FormatException("ExecuteOrder line contains no robot programs.");
                }

                return new ReceivedOrderCommand
                {
                    CommandId = commandId,
                    OrderId = orderId,
                    OrderNumber = orderNumber,
                    ReceivedAt = DateTimeOffset.UtcNow,
                    PayloadJson = payloadJson
                };
            }
        }

        public static bool TryStore(ReceivedOrderCommand command, string inboxDirectory)
        {
            Directory.CreateDirectory(inboxDirectory);
            var destination = Path.Combine(inboxDirectory, command.CommandId.ToString("D") + ".json");
            if (File.Exists(destination)) return false;
            var temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(command, WriteOptions), new UTF8Encoding(false));
                if (File.Exists(destination)) return false;
                File.Move(temporary, destination);
                return true;
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static Guid GetRequiredGuid(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(value.GetString(), out var parsed) || parsed == Guid.Empty)
                throw new FormatException($"ExecuteOrder is missing {propertyName}.");
            return parsed;
        }

        private static int GetRequiredInt32(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value) || !value.TryGetInt32(out var parsed))
                throw new FormatException($"ExecuteOrder is missing {propertyName}.");
            return parsed;
        }

        private static string GetRequiredString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                throw new FormatException($"ExecuteOrder is missing {propertyName}.");
            return value.GetString()!;
        }
    }
}
