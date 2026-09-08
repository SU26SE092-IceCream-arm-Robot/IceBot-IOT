using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace IceBot
{
    // Opening the production executable is the operator's authorization after clearing the workcell.
    // Do not configure unattended restart of this executable.
    internal sealed class RuntimeSession : IDisposable
    {
        private readonly Mutex _mutex;
        private readonly string _marker;
        private readonly string _log;
        private readonly object _gate = new object();
        private bool _faulted;

        internal RuntimeSession(string directory, string mutexName = @"Global\IceBot-Production-Runtime")
        {
            _mutex = new Mutex(false, mutexName);
            bool acquired;
            try { acquired = _mutex.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired)
            {
                _mutex.Dispose();
                throw new InvalidOperationException("IceBot is already running. A second runtime cannot start.");
            }
            _marker = Path.Combine(directory, "session-state.txt");
            _log = Path.Combine(directory, "runtime-events.jsonl");
            try
            {
                Directory.CreateDirectory(directory);
                if (File.Exists(_marker) && File.ReadAllText(_marker) != "CleanShutdown")
                    Record("UncleanShutdownDetected", "Previous process did not finish normally; exact cause is unknown.");
                WriteMarker("Running");
                Record("Startup", "Operator launch; interrupted units will restart from the beginning after device checks.");
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            }
            catch { _mutex.ReleaseMutex(); _mutex.Dispose(); throw; }
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            _faulted = true;
            try { Record("UnhandledException", args.ExceptionObject.ToString() ?? "Unknown error"); }
            catch { /* The startup marker remains available even if logging fails. */ }
        }

        internal void RecordFailure(Exception error)
        {
            _faulted = true;
            Record("RuntimeFailure", error.ToString());
        }

        internal void Record(string kind, string detail)
        {
            lock (_gate)
            {
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    timestamp = DateTimeOffset.UtcNow, kind, detail,
                    processId = System.Diagnostics.Process.GetCurrentProcess().Id,
                    appVersion = typeof(RuntimeSession).Assembly.GetName().Version?.ToString()
                }) + Environment.NewLine);
                using (var stream = new FileStream(_log, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
            }
        }

        private void WriteMarker(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            using (var stream = new FileStream(_marker, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            try
            {
                if (!_faulted)
                {
                    Record("CleanShutdown", "Runtime entry point exited normally; unfinished units remain recoverable.");
                    WriteMarker("CleanShutdown");
                }
            }
            finally { _mutex.ReleaseMutex(); _mutex.Dispose(); }
        }
    }
}
