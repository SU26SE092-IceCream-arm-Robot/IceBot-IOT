using System;
using System.Diagnostics;
using System.Text;

namespace IceBot.Config
{
    /// <summary>
    /// Runs the NetBird CLI on behalf of the operator so entering a setup key in the config
    /// wizard is enough — no need to open a separate terminal and run
    /// `netbird up --setup-key ...` by hand. Requires the `netbird` CLI to already be installed
    /// and on PATH; IceBot does not install it.
    /// </summary>
    internal static class NetBirdSetup
    {
        public static bool RunUp(string setupKey, out string message)
        {
            if (string.IsNullOrWhiteSpace(setupKey))
            {
                message = "Thieu setup key.";
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "netbird",
                Arguments = $"up --setup-key \"{setupKey.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            try
            {
                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        message = "Khong khoi dong duoc tien trinh 'netbird'.";
                        return false;
                    }

                    var stdout = process.StandardOutput.ReadToEnd();
                    var stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    var output = Combine(stdout, stderr);
                    if (process.ExitCode == 0)
                    {
                        message = string.IsNullOrWhiteSpace(output) ? "NetBird da ket noi." : output;
                        return true;
                    }

                    message = string.IsNullOrWhiteSpace(output)
                        ? $"'netbird up' thoat voi ma loi {process.ExitCode}."
                        : output;
                    return false;
                }
            }
            catch (Exception ex)
            {
                message = $"Khong chay duoc lenh 'netbird'. Kiem tra da cai NetBird CLI va co trong PATH chua ({ex.Message}).";
                return false;
            }
        }

        private static string Combine(string stdout, string stderr)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(stdout))
            {
                sb.Append(stdout.Trim());
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.Append(stderr.Trim());
            }

            return sb.ToString();
        }
    }
}
