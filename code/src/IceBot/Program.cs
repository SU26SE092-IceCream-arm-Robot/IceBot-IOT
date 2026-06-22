using System;
using IceBot.Config;
using IceBot.Workflow;

namespace IceBot
{
    internal static class Program
    {
        private static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            PrintBanner();

            Console.WriteLine("Press ENTER to run workflow test on robot...");
            Console.ReadLine();

            try
            {
                WorkflowRunner.RunQueue(AppConfig.TestScriptQueue, AppConfig.DefaultRobotIp);
                Console.WriteLine();
                Console.WriteLine("Done. Press ENTER to exit.");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"[ERROR] {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("Press ENTER to exit.");
            }

            Console.ReadLine();
        }

        private static void PrintBanner()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  IceBot-IOT  |  Fairino FR5 Controller");
            Console.WriteLine("========================================");
            Console.WriteLine($"Robot IP : {AppConfig.DefaultRobotIp}");
            Console.WriteLine($"Workflow : {AppConfig.GetWorkflowDirectory()}");
            Console.WriteLine();
        }
    }
}
