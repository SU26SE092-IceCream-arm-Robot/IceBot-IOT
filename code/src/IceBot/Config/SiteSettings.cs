namespace IceBot.Config
{
    internal sealed class SiteSettings
    {
        public string DuckDnsSubdomain { get; set; } = string.Empty;
        public string DuckDnsToken { get; set; } = string.Empty;
        public string TunnelName { get; set; } = "icebot";
        public string PublicUrl { get; set; } = string.Empty;
        public string BeApiUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string RobotIp { get; set; } = AppConfig.DefaultRobotIp;

        public string DuckDnsDomain =>
            string.IsNullOrWhiteSpace(DuckDnsSubdomain)
                ? string.Empty
                : $"{DuckDnsSubdomain.Trim()}.duckdns.org";

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(DuckDnsSubdomain)
            && !string.IsNullOrWhiteSpace(DuckDnsToken)
            && !string.IsNullOrWhiteSpace(PublicUrl);
    }
}
