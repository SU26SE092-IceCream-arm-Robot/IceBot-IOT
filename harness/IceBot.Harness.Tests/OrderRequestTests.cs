using System;
using IceBot.Workflow;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class OrderRequestTests
    {
        [Fact]
        public void Parse_PreservesBackendStepOrder()
        {
            var order = OrderRequest.Parse("{\"orderId\":\"ORD-1\",\"steps\":[\"b.lua\",\"a.lua\"]}");

            Assert.Equal("ORD-1", order.OrderId);
            Assert.Equal(new[] { "b.lua", "a.lua" }, order.Steps);
        }

        [Theory]
        [InlineData("not-json")]
        [InlineData("{}")]
        [InlineData("{\"orderId\":\"ORD-1\",\"steps\":[]}")]
        public void Parse_RejectsMalformedOrIncompleteOrders(string json)
        {
            Assert.Throws<FormatException>(() => OrderRequest.Parse(json));
        }

        [Fact]
        public void Parse_IsCaseInsensitiveForLegacyApiCompatibility()
        {
            var order = OrderRequest.Parse("{\"ORDERID\":\"ORD-2\",\"STEPS\":[\"one.lua\"]}");
            Assert.Equal("ORD-2", order.OrderId);
        }
    }
}
