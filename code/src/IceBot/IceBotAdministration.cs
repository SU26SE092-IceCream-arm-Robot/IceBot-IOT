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
            while (true)
            {
                System.Console.WriteLine("INIT ICEBOT\n1. Cau hinh va kiem tra may (dang nhap)\n2. Nhat ky va su co (offline)\n0. Thoat");
                var choice = System.Console.ReadLine();
                if (choice == null || choice.Trim() == "0") return;
                if (choice.Trim() == "2") { DiagnosticsMenu.Run(); continue; }
                if (choice.Trim() != "1") continue;
                SiteConfigStore.Load();
                StoreAuth.RequireLogin();
                ConsoleMenu.Run();
            }
        }
    }
}
