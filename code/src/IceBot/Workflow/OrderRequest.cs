using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IceBot.Workflow
{
    // Payload shape sent by BE for POST /api/orders. BE now resolves which .lua files an
    // order needs AND the physical run order itself — IceBot no longer maps order contents
    // (flavor/topping/qty) to steps or reorders them; it just runs Steps as given. See
    // "Order → robot wiring" in CLAUDE.md for why this replaced the old order->step mapper design.
    internal sealed class OrderRequest
    {
        [JsonPropertyName("orderId")]
        public string OrderId { get; set; } = string.Empty;

        // Exact .lua file names (with extension), already in the order they must run —
        // WorkflowRunner.RunQueue runs them as-is, no re-sorting.
        [JsonPropertyName("steps")]
        public List<string> Steps { get; set; } = new List<string>();

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public static OrderRequest Parse(string json)
        {
            OrderRequest order;
            try
            {
                order = JsonSerializer.Deserialize<OrderRequest>(json, Options);
            }
            catch (JsonException ex)
            {
                throw new FormatException($"Don hang khong dung dinh dang JSON: {ex.Message}", ex);
            }

            if (order == null)
            {
                throw new FormatException("Don hang rong.");
            }

            if (string.IsNullOrWhiteSpace(order.OrderId))
            {
                throw new FormatException("Thieu orderId.");
            }

            if (order.Steps == null || order.Steps.Count == 0)
            {
                throw new FormatException("Thieu danh sach steps (ten file .lua theo dung thu tu chay).");
            }

            return order;
        }
    }
}
