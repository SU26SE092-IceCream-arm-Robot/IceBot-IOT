using System;
using IceBot.Cli;
using IceBot.Config;
using IceBot.Workflow;

namespace IceBot
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            SiteConfigStore.Load();

            if (args.Length > 0)
            {
                RunCommand(args[0]);
                return;
            }

            ConsoleMenu.Run();
        }

        private static void RunCommand(string command)
        {
            switch (command.ToLowerInvariant())
            {
                case "setup":
                case "config":
                    ConfigSetupWizard.Run();
                    ConsoleMenu.Pause();
                    break;
                case "serve":
                    ConsoleMenu.RunServeMode();
                    break;
                case "test":
                    ConsoleMenu.RunTestMode();
                    break;
                case "test-machine":
                    ConsoleMenu.RunMachineTestMode();
                    break;
                case "provision":
                    WorkflowProvisioner.RunInteractive();
                    ConsoleMenu.Pause();
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    Console.WriteLine("Usage: IceBot [setup|serve|test|test-machine|provision]");
                    ConsoleMenu.Pause();
                    break;
            }
        }
    }
}
