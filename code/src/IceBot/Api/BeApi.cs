using System;
using System.Collections.Generic;

namespace IceBot.Api
{
    internal sealed class LuaScript
    {
        public LuaScript(string fileName, string content)
        {
            FileName = fileName;
            Content = content;
        }

        public string FileName { get; }
        public string Content { get; }
    }

    /// <summary>
    /// Cloud BE API client.
    /// Contract: one request may return 1 or many .lua files in a single response.
    /// TODO: replace MockResolve with real HTTP when BE is ready.
    /// </summary>
    internal static class BeApi
    {
        public static IReadOnlyList<LuaScript> GetLua(string model)
        {
            return GetLua(new[] { model });
        }

        /// <summary>
        /// Single API call. BE response: { "files": [{ "name", "content" }, ...] } — 1 or N files.
        /// </summary>
        public static IReadOnlyList<LuaScript> GetLua(IReadOnlyList<string> models)
        {
            if (models == null || models.Count == 0)
            {
                throw new ArgumentException("Chua nhap model.", nameof(models));
            }

            var normalized = NormalizeModels(models);
            if (normalized.Count == 0)
            {
                throw new ArgumentException("Chua nhap model.", nameof(models));
            }

            // TODO: POST { machineIds: normalized } -> parse response.files[]
            var files = MockResolve(normalized);
            if (files.Count == 0)
            {
                throw new InvalidOperationException("BE khong tra ve file nao.");
            }

            return files;
        }

        private static IReadOnlyList<LuaScript> MockResolve(IReadOnlyList<string> models)
        {
            var files = new List<LuaScript>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var model in models)
            {
                var key = model.ToLowerInvariant();

                if (BundleCatalog.TryGetValue(key, out var bundle))
                {
                    foreach (var bundleFile in bundle)
                    {
                        AddFile(files, seen, model, bundleFile);
                    }

                    continue;
                }

                if (ModelCatalog.TryGetValue(key, out var mappedFile))
                {
                    AddFile(files, seen, model, mappedFile);
                    continue;
                }

                AddFile(files, seen, model, SanitizeFileName(key));
            }

            return files;
        }

        private static void AddFile(List<LuaScript> files, HashSet<string> seen, string model, string fileName)
        {
            if (!seen.Add(fileName))
            {
                return;
            }

            files.Add(new LuaScript(fileName, BuildStubContent(model, fileName)));
        }

        private static List<string> NormalizeModels(IReadOnlyList<string> models)
        {
            var list = new List<string>();
            foreach (var model in models)
            {
                var trimmed = model?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(trimmed))
                {
                    list.Add(trimmed);
                }
            }

            return list;
        }

        private static readonly Dictionary<string, string> ModelCatalog =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["lay_coc"] = "lay_coc.lua",
                ["cup_s"] = "cup_s.lua",
                ["ice_chocolate_s"] = "ice_chocolate_s.lua",
                ["topping_keo_com"] = "topping_keo_com.lua",
                ["deliver_tray"] = "deliver_tray.lua",
            };

        /// <summary>Models that return a full workflow bundle in one API response.</summary>
        private static readonly Dictionary<string, string[]> BundleCatalog =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["fr5"] = new[]
                {
                    "lay_coc.lua",
                    "cup_s.lua",
                    "ice_chocolate_s.lua",
                    "topping_keo_com.lua",
                    "deliver_tray.lua",
                },
                ["full"] = new[]
                {
                    "lay_coc.lua",
                    "cup_s.lua",
                    "ice_chocolate_s.lua",
                    "topping_keo_com.lua",
                    "deliver_tray.lua",
                },
            };

        private static string SanitizeFileName(string model)
        {
            var safe = model.Replace(' ', '_').Replace('-', '_');
            return safe.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) ? safe : safe + ".lua";
        }

        private static string BuildStubContent(string model, string fileName)
        {
            return $@"-- Mock Lua from BeApi.GetLua
-- Model: {model}
-- File: {fileName}
-- TODO: replace with real BE API response

local function toDouble(v)
    return tonumber(tostring(v)) + 0.0
end

-- Home pose (stub)
MoveJ({{toDouble(""0.000""), toDouble(""-96.600""), toDouble(""-83.500""), toDouble(""0.000""), toDouble(""0.000""), toDouble(""0.000"")}}, 0, 0, toDouble(""30.0""), toDouble(""30.0""), toDouble(""-1.0""), toDouble(""-1.0""))
";
        }
    }
}
