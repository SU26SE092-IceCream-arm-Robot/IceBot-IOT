using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using IceBot.Api;
using IceBot.Config;
using IceBot.Machines;
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

            while (true)
            {
                SafeClear();
                PrintBanner();
                var settings = SiteConfigStore.Load();
                Console.WriteLine(GetConfigurationStatusLabel(settings));
                Console.WriteLine();
                Console.WriteLine("1. Cau hinh");
                Console.WriteLine("2. Test may");
                Console.WriteLine("0. Thoat");
                Console.WriteLine();
                Console.Write("Chon: ");
                var input = Console.ReadLine();
                if (input == null) return;
                var choice = input.Trim();

                switch (choice)
                {
                    case "1":
                        RunConfigMenu();
                        break;
                    case "2":
                        RunTestMenu();
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

        internal static void RunConfigMenu()
        {
            while (true)
            {
                SafeClear();
                PrintBanner();
                var settings = SiteConfigStore.Load();
                Console.WriteLine(GetConfigurationStatusLabel(settings));
                Console.WriteLine();
                Console.WriteLine("CAU HINH");
                Console.WriteLine("1. Thiet lap Edge lan dau");
                Console.WriteLine("2. Cau hinh ket noi");
                Console.WriteLine("3. Cau hinh thiet bi");
                Console.WriteLine("4. Xem trang thai cau hinh");
                Console.WriteLine("5. Cong cu nang cao");
                Console.WriteLine("0. Quay lai");
                Console.WriteLine();
                Console.Write("Chon: ");
                var input = Console.ReadLine();
                if (input == null) return;

                switch (input.Trim())
                {
                    case "1": RunFirstTimeSetupMenu(); break;
                    case "2": RunConnectionConfigMenu(); break;
                    case "3": RunDeviceConfigMenu(); break;
                    case "4": PrintConfigurationStatus(); Pause(); break;
                    case "5": RunAdvancedToolsMenu(); break;
                    case "0": return;
                    default: Console.WriteLine("Lua chon khong hop le."); Pause(); break;
                }
            }
        }

        private static void RunFirstTimeSetupMenu()
        {
            while (true)
            {
                SafeClear();
                PrintBanner();
                Console.WriteLine("THIET LAP EDGE LAN DAU");
                Console.WriteLine("1. Bat dau / tiep tuc thiet lap tu dong");
                Console.WriteLine("2. Kiem tra dieu kien va tien do thiet lap");
                Console.WriteLine("0. Quay lai");
                Console.WriteLine();
                Console.Write("Chon: ");
                var input = Console.ReadLine();
                if (input == null) return;
                switch (input.Trim())
                {
                    case "1": EdgeInitializationWizard.Run(); Pause(); break;
                    case "2": PrintConfigurationStatus(); Pause(); break;
                    case "0": return;
                    default: Console.WriteLine("Lua chon khong hop le."); Pause(); break;
                }
            }
        }

        private static void RunConnectionConfigMenu()
        {
            while (true)
            {
                SafeClear();
                PrintBanner();
                Console.WriteLine("CAU HINH KET NOI");
                Console.WriteLine("1. Backend API URL");
                Console.WriteLine("2. NetBird");
                Console.WriteLine("3. Kiem tra ket noi Backend qua mTLS");
                Console.WriteLine("4. Xem thong tin chung chi mTLS");
                Console.WriteLine("0. Quay lai");
                Console.WriteLine();
                Console.Write("Chon: ");
                var input = Console.ReadLine();
                if (input == null) return;
                switch (input.Trim())
                {
                    case "1": ConfigSetupWizard.RunBackendSettings(); Pause(); break;
                    case "2": ConfigSetupWizard.RunNetBird(); Pause(); break;
                    case "3": TestBackendConnection(); Pause(); break;
                    case "4": PrintCertificateStatus(); Pause(); break;
                    case "0": return;
                    default: Console.WriteLine("Lua chon khong hop le."); Pause(); break;
                }
            }
        }

        private static void RunDeviceConfigMenu()
        {
            while (true)
            {
                SafeClear();
                PrintBanner();
                Console.WriteLine("CAU HINH THIET BI");
                Console.WriteLine("1. Cau hinh Robot");
                Console.WriteLine("2. Quan ly may ngoai vi");
                Console.WriteLine("3. Cau hinh cong COM");
                Console.WriteLine("4. Bao cao lai hardware profile");
                Console.WriteLine("0. Quay lai");
                Console.WriteLine();
                Console.Write("Chon: ");
                var input = Console.ReadLine();
                if (input == null) return;
                switch (input.Trim())
                {
                    case "1": ConfigSetupWizard.RunRobotSettings(); Pause(); break;
                    case "2": RunPeripheralManagementMenu(); break;
                    case "3": ConfigSetupWizard.RunMachinePortSettings(); Pause(); break;
                    case "4": ReportHardwareProfile(); Pause(); break;
                    case "0": return;
                    default: Console.WriteLine("Lua chon khong hop le."); Pause(); break;
                }
            }
        }

        private static void RunPeripheralManagementMenu()
        {
            while (true)
            {
                SafeClear();
                PrintBanner();
                Console.WriteLine("QUAN LY MAY NGOAI VI");
                Console.WriteLine("1. Danh sach thiet bi");
                Console.WriteLine("2. Dang ky thiet bi moi voi Backend");
                Console.WriteLine("3. Kiem tra ket noi Serial");
                Console.WriteLine("0. Quay lai");
                Console.WriteLine();
                Console.Write("Chon: ");
                var input = Console.ReadLine();
                if (input == null) return;
                switch (input.Trim())
                {
                    case "1": PeripheralDeviceRegistrationWizard.PrintDeviceList(); Pause(); break;
                    case "2": StoreAuth.RequireLogin(); PeripheralDeviceRegistrationWizard.Run(); Pause(); break;
                    case "3": RunPeripheralConnectionTestMode(); break;
                    case "0": return;
                    default: Console.WriteLine("Lua chon khong hop le."); Pause(); break;
                }
            }
        }

        private static void RunAdvancedToolsMenu()
        {
            while (true)
            {
                SafeClear();
                PrintBanner();
                Console.WriteLine("CONG CU NANG CAO");
                Console.WriteLine("1. Dong bo deployment ngay");
                Console.WriteLine("2. Gui lai hardware snapshot");
                Console.WriteLine("3. Gui lai report dang cho");
                Console.WriteLine("4. Gui heartbeat va readiness ngay");
                Console.WriteLine("0. Quay lai");
                Console.WriteLine();
                Console.Write("Chon: ");
                var input = Console.ReadLine();
                if (input == null) return;
                switch (input.Trim())
                {
                    case "1": WorkflowProvisioner.RunInteractive(); Pause(); break;
                    case "2": ReportHardwareProfile(); Pause(); break;
                    case "3": FlushPendingReports(); Pause(); break;
                    case "4": TestBackendConnection(); Pause(); break;
                    case "0": return;
                    default: Console.WriteLine("Lua chon khong hop le."); Pause(); break;
                }
            }
        }

        private static void TestBackendConnection()
        {
            Console.WriteLine();
            var heartbeat = EdgeMtlsProbe.SendHeartbeatAndReportedDevices(out var heartbeatMessage);
            Console.WriteLine((heartbeat ? "[OK] " : "[ERROR] ") + heartbeatMessage);
            var readiness = EdgeMtlsProbe.SendReadiness(out var readinessMessage);
            Console.WriteLine((readiness ? "[OK] " : "[ERROR] ") + readinessMessage);
        }

        private static void ReportHardwareProfile()
        {
            Console.WriteLine();
            var success = EdgeMtlsProbe.SendReportedDevices(out var message);
            Console.WriteLine((success ? "[OK] " : "[ERROR] ") + message);
        }

        private static void FlushPendingReports()
        {
            Console.WriteLine();
            ProductionReportOutbox.Flush();
            DeploymentReportOutbox.Flush();
            Console.WriteLine("[OK] Da hoan tat mot luot gui lai report. Report loi mang van duoc giu de thu lai.");
        }

        private static void PrintCertificateStatus()
        {
            var settings = SiteConfigStore.Load();
            Console.WriteLine();
            Console.WriteLine("=== CHUNG CHI mTLS ===");
            Console.WriteLine($"Duong dan : {(string.IsNullOrWhiteSpace(settings.ExecutionClientCertificatePath) ? "(chua dat)" : settings.ExecutionClientCertificatePath)}");
            Console.WriteLine($"Ton tai    : {(!string.IsNullOrWhiteSpace(settings.ExecutionClientCertificatePath) && File.Exists(settings.ExecutionClientCertificatePath) ? "CO" : "KHONG")}");
            Console.WriteLine("Mat khau   : bien moi truong hoac DPAPI theo tai khoan Windows; khong luu trong site config");
        }

        private static void PrintConfigurationStatus()
        {
            var settings = SiteConfigStore.Load();
            Console.WriteLine();
            Console.WriteLine("=== TRANG THAI CAU HINH EDGE ===");
            PrintCheck("Kiosk identity", settings.KioskId != Guid.Empty, settings.KioskCode);
            PrintCheck("Execution endpoint", settings.ExecutionEndpointId != Guid.Empty, settings.ExecutionEndpointId == Guid.Empty ? string.Empty : settings.ExecutionEndpointId.ToString("D"));
            PrintCheck("Full Edge runtime", settings.FullEdgeRuntimeId != Guid.Empty, settings.FullEdgeRuntimeId == Guid.Empty ? string.Empty : settings.FullEdgeRuntimeId.ToString("D"));
            PrintCheck("Backend HTTPS", EdgeSetupReadiness.IsHttpsUrl(settings.BeApiUrl), settings.BeApiUrl);
            PrintCheck("NetBird setup key", !string.IsNullOrWhiteSpace(settings.NetBirdSetupKey), string.Empty);
            PrintCheck("Client certificate", !string.IsNullOrWhiteSpace(settings.ExecutionClientCertificatePath) && File.Exists(settings.ExecutionClientCertificatePath), settings.ExecutionClientCertificatePath);
            PrintCheck("Robot IP", !string.IsNullOrWhiteSpace(settings.RobotIp), settings.RobotIp);
            PrintCheck("Robot hardware profile", !string.IsNullOrWhiteSpace(settings.PrimaryRobotSourceDeviceKey) && !string.IsNullOrWhiteSpace(settings.PrimaryRobotRuntimeTargetCode) && !string.IsNullOrWhiteSpace(settings.PrimaryRobotMachineModelCode), $"{settings.PrimaryRobotRuntimeTargetCode}/{settings.PrimaryRobotMachineModelCode}");
            PrintCheck("Active deployment", settings.ActiveConfigurationDeploymentId != Guid.Empty, settings.ActiveConfigurationDeploymentId == Guid.Empty ? string.Empty : settings.ActiveConfigurationDeploymentId.ToString("D"));
            PrintCheck("Active workflow", !string.IsNullOrWhiteSpace(settings.ActiveWorkflowDirectory) && Directory.Exists(settings.ActiveWorkflowDirectory), settings.ActiveWorkflowDirectory);
            Console.WriteLine();
            if (!EdgeSetupReadiness.IsSetupComplete(settings))
                Console.WriteLine("[SETUP CHUA HOAN TAT] Hay xu ly cac muc WARN cua Setup o tren.");
            else if (!EdgeSetupReadiness.IsProductionReady(settings))
                Console.WriteLine("[SETUP HOAN TAT] Chua co deployment Lua hop le; chua san sang san xuat.");
            else
                Console.WriteLine("[SAN SANG SAN XUAT] Setup va deployment da day du.");
        }

        private static void PrintCheck(string label, bool success, string detail)
        {
            Console.WriteLine($"[{(success ? "OK" : "WARN")}] {label,-24}{(string.IsNullOrWhiteSpace(detail) ? string.Empty : ": " + detail)}");
        }

        internal static string GetConfigurationStatusLabel(SiteSettings settings)
        {
            if (EdgeSetupReadiness.IsProductionReady(settings)) return "Trang thai: SAN SANG SAN XUAT";
            if (EdgeSetupReadiness.IsSetupComplete(settings)) return "Trang thai: SETUP HOAN TAT - CHUA CO DEPLOYMENT";
            return "Trang thai: SETUP CHUA HOAN TAT";
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
                var input = Console.ReadLine();
                if (input == null) return;
                var choice = input.Trim();

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
            if (!EdgeSetupReadiness.IsSetupComplete(settings))
            {
                Console.WriteLine("[WARN] Setup Edge chua hoan tat. Chay InitIceBot.exe -> Cau hinh -> Thiet lap Edge lan dau.");
                Console.WriteLine();
            }
            else if (!EdgeSetupReadiness.IsProductionReady(settings))
            {
                Console.WriteLine("[WARN] Setup da hoan tat nhung chua co deployment Lua hop le; Order pull se chua the san xuat.");
                Console.WriteLine();
            }

            PrintIngressInfo();

            var heartbeatReported = EdgeMtlsProbe.SendHeartbeat(out var heartbeatMessage);
            Console.WriteLine(heartbeatReported ? "[OK] Heartbeat: " + heartbeatMessage : "[WARN] Heartbeat: " + heartbeatMessage);
            var hardwareReported = EdgeMtlsProbe.SendReportedDevices(out var hardwareMessage);
            Console.WriteLine(hardwareReported ? "[OK] Hardware profile: " + hardwareMessage : "[WARN] Hardware profile: " + hardwareMessage);
            var readinessReported = EdgeMtlsProbe.SendReadiness(out var readinessMessage);
            Console.WriteLine(readinessReported ? "[OK] " + readinessMessage : "[WARN] " + readinessMessage);

            using (var orderReceiver = new EdgeOrderCommandReceiver())
            {
                orderReceiver.Start();
                TrySetConsoleTitle("IceBot - SERVER RUNNING");
                Console.WriteLine();
                Console.WriteLine("========================================");
                Console.WriteLine("[RUNNING] ICEBOT SERVER DANG HOAT DONG");
                Console.WriteLine($"Order pull : {(orderReceiver.IsRunning ? "RUNNING" : "DISABLED - KIEM TRA CAU HINH mTLS")}");
                Console.WriteLine("========================================");
                Console.WriteLine("Lenh: test = chay lua | exit = thoat");
                Console.WriteLine();

                using (var statusTimer = new Timer(_ =>
                {
                    var heartbeat = EdgeMtlsProbe.SendHeartbeat(out var heartbeatMessage);
                    var hardware = EdgeMtlsProbe.SendReportedDevices(out var hardwareMessage);
                    var inventory = EdgeMtlsProbe.SendSimulatedInventoryObservations(out var inventoryMessage);
                    var readiness = EdgeMtlsProbe.SendReadiness(out var readinessMessage);
                    Console.WriteLine($"\n[STATUS {DateTime.Now:HH:mm:ss}] Server=RUNNING | OrderPull={(orderReceiver.IsRunning ? "RUNNING" : "DISABLED")} | Heartbeat={(heartbeat ? "REPORTED" : "FAILED")}: {heartbeatMessage} | HardwareProfile={(hardware ? "REPORTED" : "FAILED")}: {hardwareMessage} | Inventory={(inventory ? "REPORTED" : "FAILED")}: {inventoryMessage} | Readiness={(readiness ? "REPORTED" : "FAILED")}: {readinessMessage}");
                },
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
                using (var executor = RobotWorkflowExecutorFactory.Create(AppConfig.RobotIp))
                {
                    executor.Connect();
                }

                Console.WriteLine(AppConfig.RobotExecutionMode == RobotExecutionMode.Simulated
                    ? "   Simulator: connected (khong co ket noi FR5 that)."
                    : $"   Tay may ({AppConfig.RobotIp}): connect");
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
        // doesn't have the NetBird CLI, the operator is directed back to Setup.exe. Runtime only
        // reconnects NetBird; it never installs system dependencies. Non-blocking: a failure
        // here only warns, it does not stop the app from starting.
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
            Console.WriteLine($"Execution mode : {AppConfig.RobotExecutionMode}");
            if (AppConfig.RobotExecutionMode == RobotExecutionMode.Simulated)
                Console.WriteLine("[WARN] Simulated mode does not connect to, validate, or move a physical robot.");
            Console.WriteLine($"Workflow : {AppConfig.GetWorkflowDirectory()}");
            Console.WriteLine();
        }

        private static void PrintIngressInfo()
        {
            Console.WriteLine("Ket noi Edge (NetBird/mTLS):");
            Console.WriteLine($"  NetBird setup key : {(string.IsNullOrEmpty(AppConfig.NetBirdSetupKey) ? "chua dat" : "da dat")}");
            Console.WriteLine($"  BE private URL : {AppConfig.BeApiUrl}");
            Console.WriteLine($"  Execution ID   : {(AppConfig.ExecutionEndpointId == Guid.Empty ? "chua dat" : AppConfig.ExecutionEndpointId.ToString("D"))}");
            Console.WriteLine($"  Client PFX     : {(string.IsNullOrEmpty(AppConfig.ExecutionClientCertificatePath) ? "chua dat" : "da dat")}");
            Console.WriteLine($"  Dang nhap BE   : {(string.IsNullOrEmpty(AppConfig.OperatorAccessToken) ? "CHUA (IceBot.exe login)" : "da dang nhap")}");
            Console.WriteLine();
            Console.WriteLine("Edge nhan command tu BE qua mTLS pull; khong mo HTTP order API local.");
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
