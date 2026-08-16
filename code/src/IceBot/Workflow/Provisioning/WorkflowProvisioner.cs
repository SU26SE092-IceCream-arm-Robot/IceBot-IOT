using System;
using System.Collections.Generic;
using IceBot.Config;

namespace IceBot.Workflow
{
    internal sealed class ProvisionResult
    {
        public bool Success { get; set; }
        public bool Retryable { get; set; }
        public string Message { get; set; } = string.Empty;
        public IReadOnlyList<string> SavedFiles { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Pulls an authenticated Full Edge deployment and installs its verified Lua bundle.
    /// </summary>
    internal static class WorkflowProvisioner
    {
        public static void RunInteractive()
        {
            Console.WriteLine();
            Console.WriteLine("=== Dong bo deployment Lua tu BE ===");
            Console.WriteLine("API: execution endpoint command pull (mTLS)");
            Console.WriteLine($"BE private URL: {(string.IsNullOrWhiteSpace(AppConfig.BeApiUrl) ? "CHUA DAT - can bo sung dia chi HTTPS qua NetBird" : AppConfig.BeApiUrl)}");
            Console.WriteLine($"Workflow: {AppConfig.GetWorkflowDirectory()}");
            Console.WriteLine();
            var result = FullEdgeConfigurationInstaller.PullAndInstall();
            PrintResult(result);
            foreach (var file in result.SavedFiles) Console.WriteLine("  [OK] " + file);
        }

        // Records which peripheral machine step names this store actually has, based on the
        // .lua files BE just returned (not the raw model strings typed in — a bundle keyword
        // like "FR5" expands to several files, and it's the resulting step names that
        // MachineRegistry.TryGetModule can resolve). "Test may > 2 Test ket noi may ngoai vi"
        // reads this list back to know which machines to check.
        private static void PrintResult(ProvisionResult result)
        {
            Console.WriteLine();
            Console.WriteLine(result.Success ? "[OK] " + result.Message : "[ERROR] " + result.Message);
        }
    }
}
