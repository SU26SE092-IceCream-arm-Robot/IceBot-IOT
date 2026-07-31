using System;
using System.IO;

namespace IceBot.Config
{
    internal static class AppConfig
    {
        public const string DefaultRobotIp = "192.168.58.2";

        public const int ApiListenPort = 5080;

        public static string ApiListenPrefix => $"http://localhost:{ApiListenPort}/";

        public static string ApiKey =>
            FirstNonEmpty(Environment.GetEnvironmentVariable("ICEBOT_API_KEY"), SiteConfigStore.Load().ApiKey);

        // Setup key for NextBird (replaces the old DuckDNS + Cloudflare Tunnel ingress) —
        // NextBird uses this to identify the store and open the path in to this Edge PC.
        public static string NextBirdSetupKey =>
            FirstNonEmpty(Environment.GetEnvironmentVariable("ICEBOT_NEXTBIRD_SETUP_KEY"), SiteConfigStore.Load().NextBirdSetupKey);

        public static string PublicUrl =>
            FirstNonEmpty(Environment.GetEnvironmentVariable("ICEBOT_PUBLIC_URL"), SiteConfigStore.Load().PublicUrl, "https://your-shop.example.com");

        public static string BeApiUrl =>
            FirstNonEmpty(Environment.GetEnvironmentVariable("ICEBOT_BE_API_URL"), SiteConfigStore.Load().BeApiUrl);

        public static string RobotIp =>
            FirstNonEmpty(Environment.GetEnvironmentVariable("ICEBOT_ROBOT_IP"), SiteConfigStore.Load().RobotIp, DefaultRobotIp);

        public static string StoreAccount =>
            FirstNonEmpty(Environment.GetEnvironmentVariable("ICEBOT_STORE_ACCOUNT"), SiteConfigStore.Load().StoreAccount);

        // Key BE returned on successful store login (IceBot.Api.StoreAuth) — attach this to
        // outbound Edge->BE requests once BeApi talks to a real BE over HTTP. Empty until the
        // store has logged in at least once (mandatory at app startup, or `IceBot.exe login`).
        public static string BeSessionKey =>
            FirstNonEmpty(Environment.GetEnvironmentVariable("ICEBOT_BE_SESSION_KEY"), SiteConfigStore.Load().BeSessionKey);

        public static readonly string[] TestScriptQueue =
        {
            "lay_coc.lua"
        };

        // Sample .lua file for "Test may > 1 Test tay Robot" — deliberately separate from
        // workflow/ (which only ever holds files downloaded from BE). Drop the sample file in
        // as test-workflow/robot_test.lua; if it's missing, the robot connection check still
        // runs, the sample-run step is just skipped.
        public const string TestSampleScriptName = "robot_test.lua";

        public static string GetWorkflowDirectory()
        {
            var workflowNextToExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workflow");
            if (Directory.Exists(workflowNextToExe))
            {
                return workflowNextToExe;
            }

            var repoWorkflow = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "workflow"));
            if (Directory.Exists(repoWorkflow))
            {
                return repoWorkflow;
            }

            return workflowNextToExe;
        }

        public static string GetTestWorkflowDirectory()
        {
            var nextToExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-workflow");
            if (Directory.Exists(nextToExe))
            {
                return nextToExe;
            }

            var repoTestWorkflow = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "test-workflow"));
            if (Directory.Exists(repoTestWorkflow))
            {
                return repoTestWorkflow;
            }

            return nextToExe;
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }
    }
}
