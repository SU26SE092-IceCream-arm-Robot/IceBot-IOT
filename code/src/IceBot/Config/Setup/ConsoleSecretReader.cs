using System;
using System.Text;

namespace IceBot.Config
{
    internal static class ConsoleSecretReader
    {
        public static string Read(string current)
        {
            if (Console.IsInputRedirected)
            {
                var redirected = Console.ReadLine()?.Trim() ?? string.Empty;
                return redirected.Length == 0 ? current : redirected;
            }

            var value = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    var entered = value.ToString().Trim();
                    return entered.Length == 0 ? current : entered;
                }
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (value.Length > 0) value.Length--;
                    continue;
                }
                if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
            }
        }
    }
}