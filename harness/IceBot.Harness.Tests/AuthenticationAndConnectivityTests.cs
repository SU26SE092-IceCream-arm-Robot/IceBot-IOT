using IceBot.Api;
using IceBot.Config;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class AuthenticationAndConnectivityTests
    {
        [Theory]
        [InlineData("", "password")]
        [InlineData("store", "")]
        public void Login_RejectsMissingCredentialsWithoutCallingBackend(string account, string password)
        {
            var result = BeApi.Login(account, password);
            Assert.False(result.Success);
        }

        [Fact]
        public void Refresh_RejectsMissingTokenWithoutCallingBackend()
        {
            Assert.False(BeApi.Refresh(string.Empty).Success);
        }

        [Fact]
        public void NetBirdRunUp_RejectsMissingSetupKeyWithoutStartingProcess()
        {
            Assert.False(NetBirdSetup.RunUp(" ", out var message));
            Assert.Contains("setup key", message);
        }
    }
}
