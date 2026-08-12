using System;
using IceBot.Config;

namespace IceBot.Api
{
    /// <summary>
    /// Logs an operator into BE and persists user tokens for operator-authorized requests.
    /// These tokens are not the Edge device identity.
    /// </summary>
    internal static class StoreAuth
    {
        // Set once a protected management action authenticates successfully, avoiding another
        // prompt for later protected actions during the same process.
        private static bool _loggedInThisRun;

        // Manual re-login (CLI-only: `IceBot.exe login`) — one attempt, does not block. Useful
        // mid-session (e.g. after changing the account in the config wizard) without restarting
        // the app.
        public static void RunInteractive()
        {
            Console.WriteLine();
            Console.WriteLine("=== Dang nhap tai khoan cua hang ===");

            var settings = SiteConfigStore.Load();
            if (string.IsNullOrWhiteSpace(settings.StoreAccount))
            {
                Console.WriteLine("[WARN] Chua cau hinh tai khoan cua hang. Vao menu Cau hinh -> muc 2 de nhap.");
                return;
            }

            Console.WriteLine($"Tai khoan: {settings.StoreAccount}");
            var password = PromptSecret("Mat khau", string.Empty);
            var result = BeApi.Login(settings.StoreAccount, password);
            if (!result.Success)
            {
                Console.WriteLine($"[ERROR] {result.Message}");
                return;
            }

            SaveAuthentication(settings, result);
            SiteConfigStore.Save(settings);

            Console.WriteLine($"[OK] {result.Message}");
            Console.WriteLine("Da luu key, se dung cho cac request gui len BE sau nay.");
        }

        // Gate only operator-authorized management actions such as device registration. The
        // server/order receiver deliberately does not call this method: its identity is mTLS and
        // sales must continue even when operator login or the authentication API fails.
        public static void RequireLogin()
        {
            if (_loggedInThisRun)
            {
                return;
            }

            var existingSettings = SiteConfigStore.Load();
            if (!string.IsNullOrWhiteSpace(existingSettings.OperatorRefreshToken))
            {
                Console.WriteLine("Dang khoi phuc phien dang nhap BE...");
                if (TryRefresh(out _))
                {
                    Console.WriteLine("[OK] Da khoi phuc phien dang nhap BE.");
                    _loggedInThisRun = true;
                    return;
                }

                Console.WriteLine("[WARN] Phien BE da het han. Vui long dang nhap lai.");
            }

            Console.WriteLine();
            Console.WriteLine("=== Dang nhap tai khoan cua hang (bat buoc cho thao tac nay) ===");

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
                    SaveAuthentication(settings, result);
                    SiteConfigStore.Save(settings);

                    Console.WriteLine($"[OK] {result.Message}");
                    _loggedInThisRun = true;
                    return;
                }

                Console.WriteLine($"[ERROR] {result.Message} Thu lai.");
            }
        }

        // Rotates both tokens. Call this after an operator-authorized API request returns 401,
        // then retry that request at most once.
        public static bool TryRefresh(out string message)
        {
            var settings = SiteConfigStore.Load();
            var result = BeApi.Refresh(settings.OperatorRefreshToken);
            message = result.Message;
            if (!result.Success)
            {
                settings.OperatorAccessToken = string.Empty;
                settings.OperatorRefreshToken = string.Empty;
                SiteConfigStore.Save(settings);
                _loggedInThisRun = false;
                return false;
            }

            SaveAuthentication(settings, result);
            SiteConfigStore.Save(settings);
            return true;
        }

        private static void SaveAuthentication(SiteSettings settings, LoginResult result)
        {
            settings.StorePassword = string.Empty;
            settings.OperatorAccessToken = result.AccessToken;
            settings.OperatorRefreshToken = result.RefreshToken;
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
