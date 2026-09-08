using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using IceBot.Workflow;

namespace IceBot.Cli
{
    internal sealed class DiagnosticEntry
    {
        public DateTimeOffset Time { get; set; }
        public string Category { get; set; } = "";
        public string Order { get; set; } = "";
        public string Detail { get; set; } = "";
        public override string ToString() => $"{Time.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz} | {Category} | {Order}\n{Detail}";
    }

    internal static class LocalDiagnostics
    {
        internal static List<DiagnosticEntry> Read(string dataDirectory)
        {
            var entries = new List<DiagnosticEntry>();
            var log = Path.Combine(dataDirectory, "logs", "runtime-events.jsonl");
            if (File.Exists(log))
            {
                try
                {
                    using (var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(stream))
                    {
                        string? line;
                        var number = 0;
                        while ((line = reader.ReadLine()) != null)
                        {
                            number++;
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            try
                            {
                                using (var json = JsonDocument.Parse(line))
                                {
                                    var root = json.RootElement;
                                    var kind = root.GetProperty("kind").GetString() ?? "";
                                    entries.Add(new DiagnosticEntry
                                    {
                                        Time = root.GetProperty("timestamp").GetDateTimeOffset(), Category = "App",
                                        Detail = kind == "UncleanShutdownDetected"
                                            ? "Phat hien phien truoc ket thuc bat thuong; chua xac dinh nguyen nhan."
                                            : kind + ": " + root.GetProperty("detail").GetString()
                                    });
                                }
                            }
                            catch (Exception ex) when (IsDataError(ex))
                            { entries.Add(ReadError("runtime-events.jsonl, dong " + number)); }
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                { entries.Add(ReadError("runtime-events.jsonl")); }
            }
            var jobs = Path.Combine(dataDirectory, "order-jobs");
            if (Directory.Exists(jobs))
            {
                foreach (var path in Directory.GetFiles(jobs, "*.json"))
                {
                    try
                    {
                        DurableOrderJob job;
                        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        using (var reader = new StreamReader(stream))
                            job = JsonSerializer.Deserialize<DurableOrderJob>(reader.ReadToEnd()) ?? throw new InvalidDataException();
                        foreach (var unit in job.Units)
                        {
                            var detail = new StringBuilder($"Cay {unit.ProductionUnitNo}: {unit.Status}; so lan bat dau: {unit.Attempt}\n");
                            detail.AppendLine($"Bat dau: {unit.StartedAt?.ToLocalTime():O}; hoan thanh: {unit.CompletedAt?.ToLocalTime():O}");
                            if (!string.IsNullOrEmpty(unit.ErrorCode)) detail.AppendLine(unit.ErrorCode + ": " + unit.ErrorMessage);
                            foreach (var attempt in unit.Interruptions)
                                detail.AppendLine($"Gian doan lan {attempt.Attempt}: bat dau {attempt.StartedAt?.ToLocalTime():O}; phat hien {attempt.DetectedAt.ToLocalTime():O}; {attempt.Reason}");
                            entries.Add(new DiagnosticEntry
                            {
                                Time = new[] { job.AcceptedAt, unit.StartedAt ?? job.AcceptedAt, unit.CompletedAt ?? job.AcceptedAt }
                                    .Concat(unit.Interruptions.Select(item => item.DetectedAt)).Max(),
                                Category = "Order", Order = job.OrderNumber + " / " + job.OrderId,
                                Detail = detail.ToString()
                            });
                        }
                    }
                    catch (Exception ex) when (IsDataError(ex) || ex is IOException || ex is UnauthorizedAccessException || ex is NullReferenceException)
                    { entries.Add(ReadError(Path.GetFileName(path))); }
                }
            }
            return entries.OrderByDescending(item => item.Time).ToList();
        }

        private static bool IsDataError(Exception ex) => ex is JsonException || ex is KeyNotFoundException ||
            ex is InvalidOperationException || ex is FormatException;
        private static DiagnosticEntry ReadError(string file) => new DiagnosticEntry
        { Time = DateTimeOffset.Now, Category = "ReadError", Detail = "Khong doc duoc du lieu (co the dang ghi hoac bi hong): " + file };

        internal static List<DiagnosticEntry> Filter(IEnumerable<DiagnosticEntry> entries, string category, string order, DateTime? date) =>
            entries.Where(item => (category == "" || item.Category == category || item.Category == "ReadError") &&
                (order == "" || item.Order.IndexOf(order, StringComparison.OrdinalIgnoreCase) >= 0) &&
                (!date.HasValue || item.Time.LocalDateTime.Date >= date.Value.Date)).ToList();

        internal static string Export(IEnumerable<DiagnosticEntry> entries, string directory)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "icebot-diagnostics-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(path, string.Join(Environment.NewLine + Environment.NewLine, entries.Select(item => item.ToString())), new UTF8Encoding(false));
            return path;
        }
    }

    internal static class DiagnosticsMenu
    {
        internal static void Run()
        {
            var data = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            while (true)
            {
                Console.WriteLine("NHAT KY VA SU CO\n1. Nhat ky ung dung\n2. Lich su don hang\n3. Tat ca\n0. Quay lai");
                var choice = Console.ReadLine()?.Trim();
                if (choice == null || choice == "0") return;
                if (choice != "1" && choice != "2" && choice != "3") continue;
                Console.Write("Tu ngay (yyyy-MM-dd, Enter = tat ca): ");
                var input = Console.ReadLine();
                if (input == null) return;
                DateTime? date = null;
                if (!string.IsNullOrWhiteSpace(input))
                {
                    if (!DateTime.TryParseExact(input.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    { Console.WriteLine("Ngay khong hop le."); continue; }
                    date = parsed;
                }
                Console.Write("Ma don (Enter = tat ca; loc ma don chi ap dung ban ghi don hang): ");
                var order = Console.ReadLine();
                if (order == null) return;
                try
                {
                    var entries = LocalDiagnostics.Filter(LocalDiagnostics.Read(data), choice == "1" ? "App" : choice == "2" ? "Order" : "", order.Trim(), date);
                    Console.WriteLine($"{entries.Count} ban ghi. Du lieu: {data}");
                    for (var offset = 0; offset < entries.Count; offset += 10)
                    {
                        foreach (var entry in entries.Skip(offset).Take(10)) Console.WriteLine(entry + "\n");
                        if (offset + 10 < entries.Count)
                        {
                            Console.Write("Enter = trang tiep, q = dung xem: ");
                            var next = Console.ReadLine();
                            if (next == null) return;
                            if (next.Trim().Equals("q", StringComparison.OrdinalIgnoreCase)) break;
                        }
                    }
                    Console.Write("Nhap e de xuat toan bo ket qua loc, Enter de quay lai: ");
                    if (Console.ReadLine()?.Trim().Equals("e", StringComparison.OrdinalIgnoreCase) == true)
                        Console.WriteLine("Da xuat: " + LocalDiagnostics.Export(entries, Path.Combine(data, "diagnostic-exports")));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                { Console.WriteLine("Khong doc/xuat duoc log: " + ex.Message); }
            }
        }
    }
}
