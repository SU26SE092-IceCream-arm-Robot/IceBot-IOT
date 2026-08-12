using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using IceBot.Api;
using IceBot.Config;
using IceBot.Machines;
using IceBot.Networking;
using IceBot.Robot;
using IceBot.Workflow;

namespace IceBot.Cli
{
    // Interactive console UI: main menu + the long-running "modes" (serve, test, test-machine).
    // Program.cs only dispatches into here; this class owns all Console I/O.
    internal static class ConsoleMenu
    {
        public static void Run()
        {
            PrintBanner();
            EnsureNetBirdConnected();
            Pause();

            while (true)
            {
                SafeClear();
                PrintBanner();
                var settings = SiteConfigStore.Load();
                Console.WriteLine(settings.IsConfigured
                    ? "Trang thai cau hinh: OK"
                    : "Trang thai cau hinh: CHUA DU (chon 1)");
                Console.WriteLine();
                Console.WriteLine("1. Cau hinh");
                Console.WriteLine("2. Test may");
                Console.WriteLine("3. Chay he thong");
                Console.WriteLine("0. Thoat");
                Console.WriteLine();
                Console.Write("Chon: ");
                var choice = Console.ReadLine()?.Trim();

                switch (choice)
                {
                    case "1":
                        RunConfigMenu();
                        break;
                    case "2":
                        RunTestMenu();
                        break;
                    case "3":
                        RunServeMode();
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("Lua chon khong hop le.");
                        Pause();
                        break;
                }
            }
        }

        private static void RunConfigMenu()
        {
            while (true)
            {
                SafeClear();
                PrintBanner();
                var settings = SiteConfigStore.Load();
                Console.WriteLine(settings.IsConfigured
                    ? "Trang thai cau hinh: OK"
                    : "Trang thai cau hinh: CHUA DU");
                Console.WriteLine();
                Console.WriteLine("CAU HINH");
                Console.WriteLine("1. Cau hinh NetBird");
                Console.WriteLine("2. Cau hinh he thong (robot IP, tai khoan cua hang, cong COM)");
                Console.WriteLine("3. Xem cau hinh hien tai");
                Console.WriteLine("4. Dong bo deployment Lua tu BE (mTLS)");
                Console.WriteLine("5. Dang ky may ngoai vi voi BE");
                Console.WriteLine("6. Danh sach may ngoai vi");
                Console.WriteLine("0. Quay lai");
                Console.WriteLine();
                Console.Write("Chon: ");
                var choice = Console.ReadLine()?.Trim();

                switch (choice)
                {
                    case "1":
                        ConfigSetupWizard.RunNetBird();
                        Pause();
                        break;
                    case "2":
                        ConfigSetupWizard.RunSystemSettings();
                        Pause();
                        break;
                    case "3":
                        ConfigSetupWizard.PrintSummary(settings);
                        Pause();
                        break;
                    case "4":
                        WorkflowProvisioner.RunInteractive();
                        Pause();
                        break;
                    case "5":
                        StoreAuth.RequireLogin();
                        PeripheralDeviceRegistrationWizard.Run();
                        Pause();
                        break;
                    case "6":
                        PeripheralDeviceRegistrationWizard.PrintDeviceList();
                        Pause();
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("Lua chon khong hop le.");
                        Pause();
                        break;
                }
            }
        }

        private static void RunTestMenu()
        {
            while (true)
            {
                SafeClear();
                PrintBanner();
                Console.WriteLine("TEST MAY");
                Console.WriteLine("1. Test tay Robot");
                Console.WriteLine("2. Test ket noi may ngoai vi (Serial)");
                Console.WriteLine("0. Quay lai");
                Console.WriteLine();
                Console.Write("Chon: ");
                var choice = Console.ReadLine()?.Trim();

                switch (choice)
                {
                    case "1":
                        RunTestMode();
                        break;
                    case "2":
                        RunPeripheralConnectionTestMode();
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("Lua chon khong hop le.");
                        Pause();
                        break;
                }
            }
        }

        // Test may > 2: connection-only check for every peripheral machine this store actually
        // has, per SiteSettings.ProvisionedSteps (recorded by WorkflowProvisioner whenever a
        // provisioning call succeeds). Each provisioned step name is resolved back to its
        // machine via MachineRegistry.TryGetModule — machines with no RS485 driver (e.g. pure
        // arm-motion steps) are silently skipped, since there is nothing to connect to.
        public static void RunPeripheralConnectionTestMode()
        {
            PrintBanner();
            Console.WriteLine("TEST KET NOI MAY NGOAI VI (Serial)");
            Console.WriteLine();

            var settings = SiteConfigStore.Load();
            if (settings.ProvisionedSteps.Count == 0)
            {
                Console.WriteLine("Chua co Lua artifact nao duoc ghi nhan. Vao Cau hinh > 4 de dong bo deployment tu BE truoc.");
                Pause();
                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var stepName in settings.ProvisionedSteps)
            {
                if (!MachineRegistry.TryGetModule(stepName, out var module) || !(module is IMachineTrigger trigger))
                {
                    continue;
                }

                if (!seen.Add(trigger.MachineType))
                {
                    continue;
                }

                var comPort = settings.GetMachinePort(trigger.MachineType);
                if (string.IsNullOrWhiteSpace(comPort))
                {
                    Console.WriteLine($"{stepName} : disconnect (chua cau hinh cong COM)");
                    continue;
                }

                try
                {
                    trigger.TestConnection(comPort);
                    Console.WriteLine($"{stepName} : connect");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{stepName} : disconnect ({ex.Message})");
                }
            }

            Pause();
        }

        public static void RunServeMode()
        {
            try
            {
                RunServeModeCore();
            }
            catch (Exception ex)
            {
                TrySetConsoleTitle("IceBot - SERVER ERROR");
                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine("[FATAL] SERVER KHONG THE KHOI DONG");
                Console.WriteLine($"Thoi gian : {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
                Console.WriteLine($"Loi       : {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine("========================================");
                Console.WriteLine("Cua so nay se giu nguyen de nguoi van hanh doc loi.");
                Console.WriteLine("Nhan ENTER de dong.");
                Console.ReadLine();
            }
        }

        private static void RunServeModeCore()
        {
            TrySetConsoleTitle("IceBot - STARTING");
            PrintBanner();
            Console.WriteLine($"Process ID : {System.Diagnostics.Process.GetCurrentProcess().Id}");
            Console.WriteLine($"Khoi dong  : {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
            EnsureNetBirdConnected();
            Console.WriteLine();

            var settings = SiteConfigStore.Load();
            if (!settings.IsConfigured)
            {
                Console.WriteLine("[WARN] Chua cau hinh day du. Vao menu Cau hinh -> muc 1 de nhap NetBird.");
                Console.WriteLine();
            }

            PrintIngressInfo();

            using (var orderReceiver = new EdgeOrderCommandReceiver())
            using (var api = new LocalApiServer())
            {
                orderReceiver.Start();
                api.Start();
                TrySetConsoleTitle("IceBot - SERVER RUNNING");
                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine("[RUNNING] ICEBOT SERVER DANG HOAT DONG");
                Console.WriteLine($"API        : {(api.IsRunning ? "RUNNING" : "STOPPED")} - {AppConfig.ApiListenPrefix}");
                Console.WriteLine($"Order pull : {(orderReceiver.IsRunning ? "RUNNING" : "DISABLED - KIEM TRA CAU HINH mTLS")}");
                Console.WriteLine($"Health     : {AppConfig.ApiListenPrefix.TrimEnd('/')}/health");
                Console.WriteLine("========================================");
                Console.WriteLine("Lenh: test = chay lua | exit = thoat");
                Console.WriteLine();

                using (var statusTimer = new Timer(_ =>
                    Console.WriteLine($"\n[STATUS {DateTime.Now:HH:mm:ss}] Server=RUNNING | API={(api.IsRunning ? "RUNNING" : "STOPPED")} | OrderPull={(orderReceiver.IsRunning ? "RUNNING" : "DISABLED")}"),
                    null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30)))
                {

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
            TrySetConsoleTitle("IceBot - SERVER STOPPED");
            Console.WriteLine("[STOPPED] IceBot server da dung.");
        }

        private static void TrySetConsoleTitle(string title)
        {
            try { Console.Title = title; }
            catch (IOException) { }
            catch (PlatformNotSupportedException) { }
        }

        // Test may > 1: two independent checks on the robot arm only —
        //   1. Connection (Connect() over RPC, plain OK/fail report)
        //   2. Load + run a sample .lua file from test-workflow/ (NOT workflow/ — that folder
        //      only ever holds files downloaded from BE). Skipped gracefully if the sample file
        //      hasn't been dropped in yet, or if the connection check already failed.
        public static void RunTestMode()
        {
            PrintBanner();
            Console.WriteLine("TEST TAY ROBOT");
            Console.WriteLine();
            Console.WriteLine("Nhan ENTER de bat dau...");
            Console.ReadLine();

            Console.WriteLine();
            Console.WriteLine("1. Kiem tra ket noi...");
            var connected = false;
            try
            {
                using (var executor = new FairinoLuaExecutor(AppConfig.RobotIp))
                {
                    executor.Connect();
                }

                Console.WriteLine($"   Tay may ({AppConfig.RobotIp}): connect");
                connected = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Tay may ({AppConfig.RobotIp}): disconnect ({ex.Message})");
            }

            Console.WriteLine();
            Console.WriteLine("2. Chay file lua mau...");
            if (!connected)
            {
                Console.WriteLine("   Bo qua vi chua ket noi duoc tay may.");
            }
            else
            {
                var testWorkflowDir = AppConfig.GetTestWorkflowDirectory();
                var samplePath = Path.Combine(testWorkflowDir, AppConfig.TestSampleScriptName);
                if (!File.Exists(samplePath))
                {
                    Console.WriteLine($"   Chua co file mau '{AppConfig.TestSampleScriptName}' trong {testWorkflowDir}, bo qua.");
                }
                else
                {
                    try
                    {
                        WorkflowRunner.RunQueue(new[] { AppConfig.TestSampleScriptName }, AppConfig.RobotIp, testWorkflowDir);
                        Console.WriteLine("   Xong.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   [ERROR] {ex.Message}");
                    }
                }
            }

            Pause();
        }

        // Runs once at the top of both entry points (interactive menu and `serve`), right after
        // the login gate. If a NetBird setup key is already saved but this particular machine
        // doesn't have the NetBird CLI yet (fresh Edge PC image, first run), NetBirdSetup.RunUp
        // installs it automatically before connecting — the operator never has to do this by
        // hand. Non-blocking: a failure here only warns, it does not stop the app from starting
        // (matches the "warn and continue" pattern used for other missing config).
        private static void EnsureNetBirdConnected()
        {
            var setupKey = AppConfig.NetBirdSetupKey;
            if (string.IsNullOrWhiteSpace(setupKey))
            {
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Kiem tra NetBird...");
            var ok = NetBirdSetup.RunUp(setupKey, out var message);
            Console.WriteLine(ok ? $"[OK] {message}" : $"[WARN] {message}");
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
            Console.WriteLine("Ingress (NetBird):");
            Console.WriteLine($"  NetBird setup key : {(string.IsNullOrEmpty(AppConfig.NetBirdSetupKey) ? "chua dat" : "da dat")}");
            Console.WriteLine($"  Public URL     : {AppConfig.PublicUrl}");
            Console.WriteLine($"  Local API      : {AppConfig.ApiListenPrefix}");
            Console.WriteLine($"  API key        : {(string.IsNullOrEmpty(AppConfig.ApiKey) ? "chua dat" : "da dat")}");
            Console.WriteLine($"  Dang nhap BE   : {(string.IsNullOrEmpty(AppConfig.OperatorAccessToken) ? "CHUA (IceBot.exe login)" : "da dang nhap")}");
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

        // Console.Clear() throws IOException when stdout/stdin isn't an attached console
        // buffer (redirected/piped — e.g. a scripted run, or output piped to a log file).
        // Purely cosmetic, so it's safe to just skip clearing in that case.
        private static void SafeClear()
        {
            try
            {
                Console.Clear();
            }
            catch (IOException)
            {
            }
        }
    }
}
