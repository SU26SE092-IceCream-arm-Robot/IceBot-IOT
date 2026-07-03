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

        public static string DuckDnsDomain =>
            FirstNonEmpty(Environment.GetEnvironmentVariable("ICEBOT_DUCKDNS_DOMAIN"), SiteConfigStore.Load().DuckDnsDomain, "your-shop.duckdns.org");

        public static string PublicUrl =>
            FirstNonEmpty(Environment.GetEnvironmentVariable("ICEBOT_PUBLIC_URL"), SiteConfigStore.Load().PublicUrl, "https://your-shop.example.com");

        public static string BeApiUrl =>
            FirstNonEmpty(Environment.GetEnvironmentVariable("ICEBOT_BE_API_URL"), SiteConfigStore.Load().BeApiUrl);

        public static string RobotIp =>
            FirstNonEmpty(Environment.GetEnvironmentVariable("ICEBOT_ROBOT_IP"), SiteConfigStore.Load().RobotIp, DefaultRobotIp);

        public static string TunnelName =>
            FirstNonEmpty(Environment.GetEnvironmentVariable("ICEBOT_TUNNEL_NAME"), SiteConfigStore.Load().TunnelName, "icebot");

        public static readonly string[] TestScriptQueue =
        {
            "lay_coc.lua"
        };

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
