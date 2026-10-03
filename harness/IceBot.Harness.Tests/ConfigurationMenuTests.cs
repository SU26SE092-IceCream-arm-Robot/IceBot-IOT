using System;
using System.IO;
using IceBot.Cli;
using Xunit;

namespace IceBot.Harness.Tests
{
    [CollectionDefinition("ConsoleMenu", DisableParallelization = true)]
    public sealed class ConsoleMenuCollectionDefinition { }

    [Collection("ConsoleMenu")]
    public class ConfigurationMenuTests
    {
        [Fact]
        public void SystemConfigMenu_ShowsTaskBasedGroupsAndReturns()
        {
            var originalIn = Console.In;
            var originalOut = Console.Out;
            using (var input = new StringReader("0" + Environment.NewLine))
            using (var output = new StringWriter())
            {
                try
                {
                    Console.SetIn(input);
                    Console.SetOut(output);
                    ConsoleMenu.RunSystemConfigMenu();

                    var text = output.ToString();
                    Assert.Contains("1. Cau hinh ket noi", text);
                    Assert.Contains("2. Cau hinh thiet bi", text);
                    Assert.Contains("3. Xem trang thai cau hinh", text);
                }
                finally
                {
                    Console.SetIn(originalIn);
                    Console.SetOut(originalOut);
                }
            }
        }
    }
}