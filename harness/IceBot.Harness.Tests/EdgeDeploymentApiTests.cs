using System;
using System.IO;
using System.Text.Json;
using IceBot.Api;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class EdgeDeploymentApiTests
    {
        [Fact]
        public void ParseFullEdgeDeployment_AcceptsCompletePayload()
        {
            var deploymentId = Guid.NewGuid();
            var releaseId = Guid.NewGuid();
            var command = new EdgeCommandData
            {
                CommandType = "DeployConfiguration",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    DeploymentId = deploymentId,
                    ConfigurationReleaseId = releaseId,
                    ReleaseChecksum = "release",
                    FullEdgeBundle = new { FormatVersion = 1, Checksum = "bundle", ContentLengthBytes = 10, ArtifactCount = 1 }
                })
            };

            var result = EdgeDeploymentApi.ParseFullEdgeDeployment(command);

            Assert.Equal(deploymentId, result.DeploymentId);
            Assert.Equal(releaseId, result.ConfigurationReleaseId);
            Assert.NotNull(result.FullEdgeBundle);
        }

        [Fact]
        public void ParseFullEdgeDeployment_RejectsWrongCommandType()
        {
            Assert.Throws<InvalidOperationException>(() => EdgeDeploymentApi.ParseFullEdgeDeployment(
                new EdgeCommandData { CommandType = "ExecuteOrder", PayloadJson = "{}" }));
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("null")]
        public void ParseFullEdgeDeployment_RejectsIncompletePayload(string json)
        {
            Assert.Throws<InvalidDataException>(() => EdgeDeploymentApi.ParseFullEdgeDeployment(
                new EdgeCommandData { CommandType = "DeployConfiguration", PayloadJson = json }));
        }
    }
}
