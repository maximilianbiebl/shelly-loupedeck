using System;
using System.IO;

namespace ShellyLoupedeckPlugin
{
    public enum LogLevel
    {
        Error = 0,
        Warning = 1,
        Info = 2,
        Verbose = 3
    }

    /// <summary>
    /// Writes to a single log file next to Loupedeck's own logs. The file is cleared
    /// when the plugin loads, and capped during a session so a long-running instance
    /// cannot fill the disk.
    ///
    /// Per-refresh and per-render tracing is <see cref="Verbose"/> and off by default:
    /// the device poll alone would otherwise write several thousand lines an hour with
    /// nothing happening, burying the entries that matter.
    /// </summary>
    public static class DebugLogger
    {
        private const long MaxLogBytes = 5 * 1024 * 1024;

        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Loupedeck",
            "Logs",
            "ShellyPlugin_Debug.log"
        );

        private static readonly object LockObject = new object();
        private static bool _cappedNoticeWritten;

        /// <summary>Entries above this level are dropped. Defaults to <see cref="LogLevel.Info"/>.</summary>
        public static LogLevel MinimumLevel { get; set; } = LogLevel.Info;

        /// <summary>Logs at <see cref="LogLevel.Info"/>.</summary>
        public static void Log(string message) => Write(LogLevel.Info, message);

        public static void Error(string message) => Write(LogLevel.Error, message);

        public static void Warn(string message) => Write(LogLevel.Warning, message);

        /// <summary>Detailed tracing, written only when verbose logging is enabled.</summary>
        public static void Verbose(string message) => Write(LogLevel.Verbose, message);

        public static bool IsVerbose => MinimumLevel >= LogLevel.Verbose;

        private static void Write(LogLevel level, string message)
        {
            if (level > MinimumLevel)
                return;

            try
            {
                lock (LockObject)
                {
                    if (IsCapped())
                        return;

                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var prefix = level == LogLevel.Info ? "" : $"[{level.ToString().ToUpperInvariant()}] ";
                    File.AppendAllText(LogFilePath, $"{timestamp} | {prefix}{message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Logging must never take the plugin down
            }
        }

        /// <summary>
        /// Stops writing once the file reaches its size limit, leaving one final line
        /// saying so. Called with the lock held.
        /// </summary>
        private static bool IsCapped()
        {
            var info = new FileInfo(LogFilePath);
            if (!info.Exists || info.Length < MaxLogBytes)
                return false;

            if (!_cappedNoticeWritten)
            {
                _cappedNoticeWritten = true;
                File.AppendAllText(LogFilePath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | [WARNING] Log size limit reached, " +
                    $"no further entries until the plugin reloads{Environment.NewLine}");
            }

            return true;
        }

        public static void Clear()
        {
            try
            {
                lock (LockObject)
                {
                    _cappedNoticeWritten = false;
                    if (File.Exists(LogFilePath))
                        File.Delete(LogFilePath);
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
