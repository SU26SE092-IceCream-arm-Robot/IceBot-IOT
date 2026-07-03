using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using IceBot.Api;
using IceBot.Config;

namespace IceBot.Workflow
{
    internal sealed class ProvisionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public IReadOnlyList<string> SavedFiles { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Fetches .lua files via a single BeApi.GetLua() call and saves them to workflow/.
    /// </summary>
    internal static class WorkflowProvisioner
    {
        public static void RunInteractive()
        {
            Console.WriteLine();
            Console.WriteLine("=== Tai file Lua tu BE (mock) ===");
            Console.WriteLine("API: BeApi.GetLua(models) — 1 lan goi, tra ve 1 hoac nhieu file");
            Console.WriteLine($"Workflow: {AppConfig.GetWorkflowDirectory()}");
            Console.WriteLine();
            Console.WriteLine("Model co san:");
            Console.WriteLine("  FR5 / full     -> BE tra ve full bundle (5 file)");
            Console.WriteLine("  cup_s, ...     -> BE tra ve 1 file / model");
            Console.WriteLine();
            Console.Write("Nhap model (phan cach dau phay): ");
            var input = Console.ReadLine()?.Trim() ?? string.Empty;

            Console.WriteLine();
            var result = FetchAndSave(SplitModels(input));
            PrintResult(result);
        }

        public static ProvisionResult FetchAndSave(IEnumerable<string> models)
        {
            var normalized = NormalizeModels(models);
            if (normalized.Count == 0)
            {
                return Fail("Chua nhap model.");
            }

            try
            {
                var label = string.Join(", ", normalized);
                Console.WriteLine($"  BeApi.GetLua([{label}])");

                var scripts = BeApi.GetLua(normalized);
                Console.WriteLine($"  BE tra ve {scripts.Count} file");

                var workflowDir = AppConfig.GetWorkflowDirectory();
                Directory.CreateDirectory(workflowDir);

                var saved = new List<string>();
                foreach (var script in scripts)
                {
                    var path = Path.Combine(workflowDir, script.FileName);
                    File.WriteAllText(path, script.Content, Encoding.UTF8);
                    saved.Add(script.FileName);
                    Console.WriteLine($"  [OK] {script.FileName}");
                }

                return new ProvisionResult
                {
                    Success = true,
                    Message = $"Da luu {saved.Count} file vao {workflowDir}",
                    SavedFiles = saved
                };
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        public static IReadOnlyList<string> SplitModels(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return Array.Empty<string>();
            }

            return input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static List<string> NormalizeModels(IEnumerable<string> models)
        {
            var list = new List<string>();
            foreach (var model in models)
            {
                var trimmed = model.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    list.Add(trimmed);
                }
            }

            return list;
        }

        private static ProvisionResult Fail(string message)
        {
            return new ProvisionResult { Success = false, Message = message };
        }

        private static void PrintResult(ProvisionResult result)
        {
            Console.WriteLine();
            Console.WriteLine(result.Success ? "[OK] " + result.Message : "[ERROR] " + result.Message);
        }
    }
}
