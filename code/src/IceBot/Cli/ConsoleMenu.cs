using System;
using System.Threading;
using IceBot.Config;
using IceBot.Machines;
using IceBot.Networking;
using IceBot.Workflow;

namespace IceBot.Cli
{
    // Interactive console UI: main menu + the long-running "modes" (serve, test, test-machine).
    // Program.cs only dispatches into here; this class owns all Console I/O.
    internal static class ConsoleMenu
    {
        public static void Run()
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
                Console.WriteLine("6. Test may ngoai vi (serial)");
                Console.WriteLine("7. Thoat");
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
                        RunMachineTestMode();
                        break;
                    case "7":
                        return;
                    default:
                        Console.WriteLine("Lua chon khong hop le.");
                        Pause();
                        break;
                }
            }
        }

        public static void RunServeMode()
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

        public static void RunTestMode()
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

        // Lists every registered machine module (MachineRegistry.Modules) so a newly added
        // machine shows up here with no changes needed in this file.
        public static void RunMachineTestMode()
        {
            PrintBanner();
            var modules = MachineRegistry.Modules;
            if (modules.Count == 0)
            {
                Console.WriteLine("[WARN] Chua co may ngoai vi nao duoc dang ky trong MachineRegistry.");
                Pause();
                return;
            }

            Console.WriteLine("Chon may de test:");
            for (var i = 0; i < modules.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {modules[i].DisplayName} ({modules[i].MachineType})");
            }

            Console.Write("Chon (ENTER = quay lai): ");
            var machineChoice = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(machineChoice)
                || !int.TryParse(machineChoice, out var index)
                || index < 1 || index > modules.Count)
            {
                return;
            }

            var module = modules[index - 1];
            var comPort = SiteConfigStore.Load().GetMachinePort(module.MachineType);
            if (string.IsNullOrWhiteSpace(comPort))
            {
                Console.WriteLine($"[WARN] Chua cau hinh cong COM cho '{module.DisplayName}'. Chon menu 1 de cau hinh.");
                Pause();
                return;
            }

            var diagnostics = module as IMachineDiagnostics;
            Console.WriteLine($"Cong COM: {comPort}");
            Console.WriteLine(diagnostics != null
                ? "1. Query trang thai | 2. Trigger | ENTER = quay lai"
                : "2. Trigger | ENTER = quay lai");
            Console.Write("Chon: ");
            var actionChoice = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(actionChoice))
            {
                return;
            }

            try
            {
                if (actionChoice == "1" && diagnostics != null)
                {
                    Console.WriteLine($"[MACHINE] {diagnostics.GetStatusText(comPort)}");
                }
                else if (actionChoice == "2")
                {
                    module.Trigger(comPort);
                }
                else
                {
                    Console.WriteLine("Lua chon khong hop le.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
            }

            Pause();
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

        public static void Pause()
        {
            Console.WriteLine();
            Console.WriteLine("Nhan ENTER de tiep tuc...");
            Console.ReadLine();
        }
    }
}
