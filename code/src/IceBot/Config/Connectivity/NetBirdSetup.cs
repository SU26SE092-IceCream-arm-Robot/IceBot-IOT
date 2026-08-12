using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace IceBot.Config
{
    /// <summary>
    /// Connects an already-installed NetBird client with the technician-provided setup key.
    /// Installing system prerequisites belongs exclusively to Setup.exe; InitIceBot and the
    /// production runtime never mutate the Windows software environment.
    /// </summary>
    internal static class NetBirdSetup
    {
        private const int UpTimeoutMs = 60 * 1000;

        // Safe to call again later (e.g. every app startup): `netbird up` is idempotent and
        // confirms the existing connection if NetBird is already up.
        public static bool RunUp(string setupKey, out string message)
        {
            if (string.IsNullOrWhiteSpace(setupKey))
            {
                message = "Thieu setup key.";
                return false;
            }

            var exePath = ResolveExecutable();
            if (exePath == null)
            {
                message = "Chua cai NetBird. Hay chay Setup.exe truoc khi khoi tao hoac van hanh IceBot.";
                return false;
            }

            var upArgs = $"up --setup-key \"{EscapeArg(setupKey)}\"";
            return RunProcess(exePath, upArgs, UpTimeoutMs, out message);
        }

        // Bare command name if it resolves via PATH; otherwise check the standard install path.
        private static string? ResolveExecutable()
        {
            if (RunProcess("netbird", "version", 10_000, out _))
            {
                return "netbird";
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var candidate = Path.Combine(programFiles, "Netbird", "netbird.exe");
            return File.Exists(candidate) ? candidate : null;
        }

        private static bool RunProcess(string fileName, string arguments, int timeoutMs, out string message)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
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
                        message = $"Khong khoi dong duoc tien trinh '{fileName}'.";
                        return false;
                    }

                    var stdout = process.StandardOutput.ReadToEnd();
                    var stderr = process.StandardError.ReadToEnd();
                    var exited = process.WaitForExit(timeoutMs);
                    if (!exited)
                    {
                        TryKill(process);
                        message = $"'{fileName}' qua thoi gian cho ({timeoutMs / 1000}s) — co the dang cho quyen admin (UAC). Chay IceBot voi quyen Administrator roi thu lai.";
                        return false;
                    }

                    var output = Combine(stdout, stderr);
                    if (process.ExitCode == 0)
                    {
                        message = output;
                        return true;
                    }

                    message = string.IsNullOrWhiteSpace(output)
                        ? $"'{fileName}' thoat voi ma loi {process.ExitCode}."
                        : output;
                    return false;
                }
            }
            catch (Exception ex)
            {
                message = $"Khong chay duoc lenh '{fileName}' ({ex.Message}).";
                return false;
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // Best-effort only — process may have exited between the timeout and here.
            }
        }

        private static string EscapeArg(string value) => value.Replace("\"", "\\\"");

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
