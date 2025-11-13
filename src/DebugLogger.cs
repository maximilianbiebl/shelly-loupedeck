using System;
using System.IO;

namespace ShellyLoupedeckPlugin
{
    public static class DebugLogger
    {
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Loupedeck",
            "Logs",
            "ShellyPlugin_Debug.log"
        );

        private static readonly object LockObject = new object();

        public static void Log(string message)
        {
            try
            {
                lock (LockObject)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logMessage = $"{timestamp} | {message}{Environment.NewLine}";
                    File.AppendAllText(LogFilePath, logMessage);
                }
            }
            catch
            {
                // Ignore logging errors
            }
        }

        public static void Clear()
        {
            try
            {
                lock (LockObject)
                {
                    if (File.Exists(LogFilePath))
                    {
                        File.Delete(LogFilePath);
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
