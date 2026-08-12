using IceBot.Api;
using IceBot.Cli;
using IceBot.Config;

namespace IceBot
{
    /// <summary>Public entry point consumed only by the InitIceBot technician application.</summary>
    public static class IceBotAdministration
    {
        public static void Run()
        {
            SiteConfigStore.Load();
            StoreAuth.RequireLogin();
            ConsoleMenu.Run();
        }
    }
}
