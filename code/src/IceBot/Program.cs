using System;
using IceBot.Cli;
using IceBot.Config;

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
                case "serve":
                    ConsoleMenu.RunServeMode();
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    Console.WriteLine("Usage: IceBot [serve]");
                    Console.WriteLine("Dung InitIceBot.exe de cau hinh, dang ky va test may.");
                    ConsoleMenu.Pause();
                    break;
            }
        }
    }
}
