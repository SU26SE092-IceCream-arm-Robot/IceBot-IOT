using System.Collections.Generic;
using System.Linq;
using IceBot.Workflow;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class WorkflowQueueBuilderTests
    {
        [Fact]
        public void BuildQueue_SortsMachineStepBeforeUnregisteredSteps()
        {
            // cup_s belongs to CupDroppingMachineModule (Position 1); the others have no
            // registered machine and sort after, keeping their input order.
            var result = WorkflowQueueBuilder.BuildQueue(new[] { "deliver_tray", "ice_chocolate_s", "cup_s" });

            Assert.Equal("cup_s", result[0]);
        }

        [Fact]
        public void BuildQueue_KeepsRelativeOrderOfUnregisteredSteps()
        {
            var result = WorkflowQueueBuilder.BuildQueue(new[] { "deliver_tray", "ice_chocolate_s" });

            // Neither has a registered machine → stable order preserved.
            Assert.Equal(new[] { "deliver_tray", "ice_chocolate_s" }, result.ToArray());
        }

        [Fact]
        public void BuildQueue_ResolvesStepNamesWithOrWithoutLuaExtension()
        {
            var result = WorkflowQueueBuilder.BuildQueue(new[] { "deliver_tray.lua", "cup_s.lua" });

            Assert.Equal("cup_s.lua", result[0]);
        }

        [Fact]
        public void BuildQueue_ReturnsEmptyForEmptyInput()
        {
            var result = WorkflowQueueBuilder.BuildQueue(new List<string>());

            Assert.Empty(result);
        }
    }
}
