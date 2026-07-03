using System;
using System.Threading;
using IceBot.Config;
using IceBot.Networking;
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

            RunMainMenu();
        }

        private static void RunCommand(string command)
        {
            switch (command.ToLowerInvariant())
            {
                case "setup":
                case "config":
                    ConfigSetupWizard.Run();
                    Pause();
                    break;
                case "serve":
                    RunServeMode();
                    break;
                case "test":
                    RunTestMode();
                    break;
                case "provision":
                    WorkflowProvisioner.RunInteractive();
                    Pause();
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    Console.WriteLine("Usage: IceBot [setup|serve|test|provision]");
                    Pause();
                    break;
            }
        }

        private static void RunMainMenu()
        {
            while (true)
            {
                Console.Clear();
                PrintBanner();
                var settings = SiteConfigStore.Load();
                Console.WriteLine(settings.IsConfigured
                    ? "Trang thai cau hinh: OK"
                    : "Trang thai cau hinh: CHUA DU (chon menu 1)");
                Console.WriteLine();
                Console.WriteLine("1. Cau hinh DuckDNS + Cloudflare Tunnel");
                Console.WriteLine("2. Xem cau hinh hien tai");
                Console.WriteLine("3. Tai file Lua tu BE (mock: BeApi.GetLua)");
                Console.WriteLine("4. Chay server (nhan don tu BE)");
                Console.WriteLine("5. Test robot (chay file lua)");
                Console.WriteLine("6. Thoat");
                Console.WriteLine();
                Console.Write("Chon: ");
                var choice = Console.ReadLine()?.Trim();

                switch (choice)
                {
                    case "1":
                        ConfigSetupWizard.Run();
                        Pause();
                        break;
                    case "2":
                        ConfigSetupWizard.PrintSummary(settings);
                        Pause();
                        break;
                    case "3":
                        WorkflowProvisioner.RunInteractive();
                        Pause();
                        break;
                    case "4":
                        RunServeMode();
                        break;
                    case "5":
                        RunTestMode();
                        break;
                    case "6":
                        return;
                    default:
                        Console.WriteLine("Lua chon khong hop le.");
                        Pause();
                        break;
                }
            }
        }

        private static void RunServeMode()
        {
            PrintBanner();
            var settings = SiteConfigStore.Load();
            if (!settings.IsConfigured)
            {
                Console.WriteLine("[WARN] Chua cau hinh day du. Chon menu 1 de nhap DuckDNS + Cloudflare.");
                Console.WriteLine();
            }

            PrintIngressInfo();

            using (var api = new LocalApiServer())
            {
                api.Start();
                Console.WriteLine();
                Console.WriteLine("Server dang chay. Cho don tu BE qua Cloudflare Tunnel.");
                Console.WriteLine("Lenh: test = chay lua | exit = thoat");
                Console.WriteLine();

                while (true)
                {
                    Console.Write("> ");
                    var line = Console.ReadLine();
                    if (line == null)
                    {
                        Thread.Sleep(500);
                        continue;
                    }

                    if (string.Equals(line, "exit", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (!string.Equals(line, "test", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Lenh khong hop le. Dung: test | exit");
                        continue;
                    }

                    try
                    {
                        WorkflowRunner.RunQueue(AppConfig.TestScriptQueue, AppConfig.RobotIp);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] {ex.Message}");
                    }
                }
            }
        }

        private static void RunTestMode()
        {
            PrintBanner();
            Console.WriteLine("Nhan ENTER de chay test workflow tren robot...");
            Console.ReadLine();

            try
            {
                WorkflowRunner.RunQueue(AppConfig.TestScriptQueue, AppConfig.RobotIp);
                Console.WriteLine();
                Console.WriteLine("Xong. Nhan ENTER de quay lai menu.");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"[ERROR] {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("Nhan ENTER de quay lai menu.");
            }

            Console.ReadLine();
        }

        private static void PrintBanner()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  IceBot-IOT  |  Fairino FR5 Controller");
            Console.WriteLine("========================================");
            Console.WriteLine($"Robot IP : {AppConfig.RobotIp}");
            Console.WriteLine($"Workflow : {AppConfig.GetWorkflowDirectory()}");
            Console.WriteLine();
        }

        private static void PrintIngressInfo()
        {
            Console.WriteLine("Ingress (DuckDNS + Cloudflare Tunnel):");
            Console.WriteLine($"  DuckDNS domain : {AppConfig.DuckDnsDomain}");
            Console.WriteLine($"  Tunnel name    : {AppConfig.TunnelName}");
            Console.WriteLine($"  Public URL     : {AppConfig.PublicUrl}");
            Console.WriteLine($"  Local API      : {AppConfig.ApiListenPrefix}");
            Console.WriteLine($"  API key        : {(string.IsNullOrEmpty(AppConfig.ApiKey) ? "chua dat" : "da dat")}");
            Console.WriteLine();
            Console.WriteLine("BE endpoints:");
            Console.WriteLine($"  POST {AppConfig.PublicUrl.TrimEnd('/')}/api/orders");
            Console.WriteLine($"  GET  {AppConfig.PublicUrl.TrimEnd('/')}/health");
        }

        private static void Pause()
        {
            Console.WriteLine();
            Console.WriteLine("Nhan ENTER de tiep tuc...");
            Console.ReadLine();
        }
    }
}
