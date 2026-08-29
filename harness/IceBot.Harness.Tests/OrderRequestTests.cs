using System;
using IceBot.Workflow;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class OrderRequestTests
    {
        [Theory]
        [InlineData("not-json")]
        [InlineData("{}")]
        [InlineData("{\"orderId\":\"ORD-1\",\"steps\":[\"one.lua\"]}")]
        public void Validate_RejectsLegacyLocalApiPayloads(string json)
        {
            Assert.Throws<FormatException>(() => EdgeOrderInbox.Validate(Guid.NewGuid(), json));
        }
    }
}