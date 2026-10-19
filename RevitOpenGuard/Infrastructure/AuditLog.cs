using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace RevitOpenGuard.Infrastructure
{
    internal sealed class AuditLog
    {
        private readonly object _gate = new object();
        private readonly string _stem;
        private int _part;
        private long _bytes;
        private const long SegmentLimit = 5L * 1024 * 1024;
        public static string RootDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MoustafaMagdi", "RevitOpenGuard");
        public string LogDirectory { get; } = Path.Combine(RootDirectory, "logs");
        public string CurrentPath => Path.Combine(LogDirectory, _stem + "_" + _part.ToString("D3") + ".log");

        public AuditLog()
        {
            _stem = "OpenGuard_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                + "_" + Process.GetCurrentProcess().Id + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Cleanup();
        }

        public void Write(string level, string eventName, string message, string openingId = "", string model = "", Exception? error = null)
        {
            lock (_gate)
            {
                try
                {
                    Directory.CreateDirectory(LogDirectory);
                    string line = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                        + " [" + level + "] [" + eventName + "] [Open:" + openingId + "] [Model:"
                        + OneLine(model) + "] " + OneLine(message)
                        + (error == null ? "" : " | Exception: " + OneLine(error.ToString())) + Environment.NewLine;
                    if (_bytes >= SegmentLimit) { _part++; _bytes = 0; Cleanup(); }
                    File.AppendAllText(CurrentPath, line, new UTF8Encoding(false));
                    _bytes += Encoding.UTF8.GetByteCount(line);
                }
                catch (Exception ex) { Trace.WriteLine("RevitOpenGuard log unavailable: " + ex.Message); }
            }
        }

        private void Cleanup()
        {
            try
            {
                if (!Directory.Exists(LogDirectory)) return;
                var files = new DirectoryInfo(LogDirectory).GetFiles("OpenGuard_*.log")
                    .OrderByDescending(file => file.LastWriteTimeUtc).ToList();
                for (int i = 0; i < files.Count; i++)
                {
                    if (files[i].FullName == CurrentPath) continue;
                    if (i >= 60 || files[i].LastWriteTimeUtc < DateTime.UtcNow.AddDays(-30))
                    {
                        try { files[i].Delete(); } catch { /* Another session may hold the file. */ }
                    }
                }
            }
            catch (Exception ex) { Trace.WriteLine("RevitOpenGuard log retention: " + ex.Message); }
        }

        private static string OneLine(string? value) => (value ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n");
    }
}
