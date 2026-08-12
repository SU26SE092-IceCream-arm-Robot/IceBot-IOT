using System;
using IceBot.Api;
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

            // Production-first default: launching IceBot.exe starts the local server and BE
            // order receiver immediately. Operators can still open the administration UI with
            // `IceBot.exe menu` when configuration or diagnostics are needed.
            ConsoleMenu.RunServeMode();
        }

        private static void RunCommand(string command)
        {
            switch (command.ToLowerInvariant())
            {
                case "menu":
                    ConsoleMenu.Run();
                    break;
                case "setup":
                case "config":
                    ConfigSetupWizard.RunNetBird();
                    ConfigSetupWizard.RunSystemSettings();
                    ConsoleMenu.Pause();
                    break;
                case "login":
                    StoreAuth.RunInteractive();
                    ConsoleMenu.Pause();
                    break;
                case "serve":
                    ConsoleMenu.RunServeMode();
                    break;
                case "test":
                    ConsoleMenu.RunTestMode();
                    break;
                case "test-machine":
                    ConsoleMenu.RunPeripheralConnectionTestMode();
                    break;
                case "provision":
                    WorkflowProvisioner.RunInteractive();
                    ConsoleMenu.Pause();
                    break;
                case "register-device":
                    StoreAuth.RequireLogin();
                    PeripheralDeviceRegistrationWizard.Run();
                    ConsoleMenu.Pause();
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    Console.WriteLine("Usage: IceBot [menu|setup|login|serve|test|test-machine|provision|register-device]");
                    ConsoleMenu.Pause();
                    break;
            }
        }
    }
}
