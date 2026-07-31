using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace IceBot.Config
{
    /// <summary>
    /// Runs the NetBird CLI on behalf of the operator so entering a setup key is enough — no
    /// need to open a separate terminal, install NetBird by hand, or run
    /// `netbird up --setup-key ...` manually. If `netbird` isn't found on this machine yet,
    /// it's installed automatically (via winget) before `up` is attempted.
    /// </summary>
    internal static class NetBirdSetup
    {
        private const string WingetPackageId = "Netbird.Netbird";
        private const int InstallTimeoutMs = 3 * 60 * 1000;
        private const int UpTimeoutMs = 60 * 1000;

        // Resolves the setup key into a running NetBird connection — installs NetBird first if
        // this machine doesn't have it yet (first run on a fresh Edge PC), then runs
        // `netbird up --setup-key <key>`. Safe to call again later (e.g. every app startup) —
        // `netbird up` is idempotent, it just confirms the existing connection if already up.
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
                Console.WriteLine("Chua thay NetBird CLI tren may nay — dang tu cai dat qua winget...");
                if (!InstallViaWinget(out var installMessage))
                {
                    message = $"Khong tu cai duoc NetBird ({installMessage}). Cai thu cong tu https://netbird.io/ roi thu lai.";
                    return false;
                }

                Console.WriteLine($"[OK] Da cai NetBird. {installMessage}".Trim());

                exePath = ResolveExecutable();
                if (exePath == null)
                {
                    message = "Da cai NetBird nhung chua tim thay file thuc thi — thu khoi dong lai IceBot.";
                    return false;
                }
            }

            var upArgs = $"up --setup-key \"{EscapeArg(setupKey)}\"";
            return RunProcess(exePath, upArgs, UpTimeoutMs, out message);
        }

        // Bare command name if it already resolves via this process's PATH; otherwise the known
        // install location (covers "just installed in this same process" — Windows doesn't
        // refresh an already-running process's PATH after an installer updates it).
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

        private static bool InstallViaWinget(out string message)
        {
            var args = $"install --id {WingetPackageId} --silent --accept-package-agreements --accept-source-agreements";
            return RunProcess("winget", args, InstallTimeoutMs, out message);
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
