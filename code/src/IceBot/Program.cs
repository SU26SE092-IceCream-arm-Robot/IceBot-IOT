using System;
using System.IO;
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
                RunCommand(args);
                return;
            }

            // Production-first default: launching IceBot.exe starts the local server and BE
            // order receiver immediately. Operators can still open the administration UI with
            // `IceBot.exe menu` when configuration or diagnostics are needed.
            ConsoleMenu.RunServeMode();
        }

        private static void RunCommand(string[] args)
        {
            switch (args[0].ToLowerInvariant())
            {
                case "serve":
                    ConsoleMenu.RunServeMode();
                    break;
                case "run-workflow":
                    if (args.Length != 2)
                        throw new ArgumentException("Usage: IceBot run-workflow <file.lua>");
                    var workflowPath = Path.GetFullPath(args[1]);
                    if (!File.Exists(workflowPath) || !string.Equals(Path.GetExtension(workflowPath), ".lua", StringComparison.OrdinalIgnoreCase))
                        throw new FileNotFoundException("Workflow Lua file not found.", workflowPath);
                    WorkflowRunner.RunQueue(new[] { Path.GetFileName(workflowPath) }, AppConfig.RobotIp,
                        Path.GetDirectoryName(workflowPath) ?? Environment.CurrentDirectory);
                    break;
                default:
                    Console.WriteLine($"Unknown command: {args[0]}");
                    Console.WriteLine("Usage: IceBot [serve | run-workflow <file.lua>]");
                    Console.WriteLine("Dung InitIceBot.exe de cau hinh, dang ky va test may.");
                    ConsoleMenu.Pause();
                    break;
            }
        }
    }
}
