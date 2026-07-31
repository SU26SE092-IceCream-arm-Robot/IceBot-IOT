using System;
using IceBot.Config;

namespace IceBot.Api
{
    /// <summary>
    /// Logs this store into BE using the account/password saved via the config wizard (menu
    /// Cau hinh, item 2 "Cau hinh he thong"), and persists the key BE returns
    /// (SiteSettings.BeSessionKey) for future outbound BE requests.
    /// </summary>
    internal static class StoreAuth
    {
        // Set once RequireLogin() succeeds, for the lifetime of this process. Both
        // ConsoleMenu.Run() (menu entry) and ConsoleMenu.RunServeMode() (serve entry) call
        // RequireLogin() — when serve is reached via the menu (already gated), this flag skips
        // asking a second time; when serve is reached directly (`IceBot.exe serve`, no menu),
        // it still gates normally.
        private static bool _loggedInThisRun;

        // Manual re-login (CLI-only: `IceBot.exe login`) — one attempt, does not block. Useful
        // mid-session (e.g. after changing the account in the config wizard) without restarting
        // the app.
        public static void RunInteractive()
        {
            Console.WriteLine();
            Console.WriteLine("=== Dang nhap tai khoan cua hang (mock BeApi.Login) ===");

            var settings = SiteConfigStore.Load();
            if (string.IsNullOrWhiteSpace(settings.StoreAccount) || string.IsNullOrWhiteSpace(settings.StorePassword))
            {
                Console.WriteLine("[WARN] Chua cau hinh tai khoan/mat khau cua hang. Vao menu Cau hinh -> muc 2 de nhap.");
                return;
            }

            Console.WriteLine($"Tai khoan: {settings.StoreAccount}");
            var result = BeApi.Login(settings.StoreAccount, settings.StorePassword);
            if (!result.Success)
            {
                Console.WriteLine($"[ERROR] {result.Message}");
                return;
            }

            settings.BeSessionKey = result.Key;
            SiteConfigStore.Save(settings);

            Console.WriteLine($"[OK] {result.Message}");
            Console.WriteLine("Da luu key, se dung cho cac request gui len BE sau nay.");
        }

        // Gate before the menu/server can start — loops until login succeeds, so the app cannot
        // be used at all without a valid session. Prompts for account/password inline if not
        // already configured (the config wizard is not a prerequisite). Type "exit" at the
        // account prompt to quit instead. Token expiry/refresh is a later concern (TODO:
        // access+refresh token); for now a successful mock login is enough to proceed.
        public static void RequireLogin()
        {
            if (_loggedInThisRun)
            {
                return;
            }

            Console.WriteLine();
            Console.WriteLine("=== Dang nhap tai khoan cua hang (bat buoc de vao he thong) ===");

            while (true)
            {
                var settings = SiteConfigStore.Load();
                var account = Prompt("Tai khoan cua hang (go 'exit' de thoat)", settings.StoreAccount);
                if (string.Equals(account, "exit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Thoat ung dung.");
                    Environment.Exit(0);
                }

                var password = PromptSecret("Mat khau", settings.StorePassword);
                var result = BeApi.Login(account, password);
                if (result.Success)
                {
                    settings.StoreAccount = account;
                    settings.StorePassword = password;
                    settings.BeSessionKey = result.Key;
                    SiteConfigStore.Save(settings);

                    Console.WriteLine($"[OK] {result.Message}");
                    _loggedInThisRun = true;
                    return;
                }

                Console.WriteLine($"[ERROR] {result.Message} Thu lai.");
            }
        }

        private static string Prompt(string label, string current)
        {
            var suffix = string.IsNullOrWhiteSpace(current) ? string.Empty : $" [{current}]";
            Console.Write($"{label}{suffix}: ");
            var input = Console.ReadLine()?.Trim() ?? string.Empty;
            return string.IsNullOrEmpty(input) ? current : input;
        }

        private static string PromptSecret(string label, string current)
        {
            var hasValue = !string.IsNullOrWhiteSpace(current);
            Console.Write($"{label}{(hasValue ? " [****]" : "")}: ");
            var input = Console.ReadLine()?.Trim() ?? string.Empty;
            return string.IsNullOrEmpty(input) ? current : input;
        }
    }
}
