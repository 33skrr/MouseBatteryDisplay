using System;
using System.IO;
using System.Text;

namespace MouseBatteryDisplay
{
    internal static class Logger
    {
        private static readonly object Sync = new object();
        private static string _logPath;
        private static bool _initialized;

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MouseBatteryDisplay");
                Directory.CreateDirectory(dir);
                _logPath = Path.Combine(dir, "app.log");
            }
            catch
            {
                try
                {
                    _logPath = Path.Combine(Path.GetTempPath(), "MouseBatteryDisplay.log");
                }
                catch
                {
                    _logPath = null;
                }
            }
        }

        internal static void Info(string message)
        {
            Write("INFO", message, null);
        }

        internal static void Warn(string message)
        {
            Write("WARN", message, null);
        }

        internal static void Error(string message, Exception exception)
        {
            Write("ERROR", message, exception);
        }

        private static void Write(string level, string message, Exception exception)
        {
            lock (Sync)
            {
                try
                {
                    Initialize();
                    if (string.IsNullOrEmpty(_logPath)) return;

                    FileInfo info = new FileInfo(_logPath);
                    if (info.Exists && info.Length > 512 * 1024)
                    {
                        string old = _logPath + ".old";
                        try
                        {
                            if (File.Exists(old)) File.Delete(old);
                            File.Move(_logPath, old);
                        }
                        catch
                        {
                        }
                    }

                    using (StreamWriter writer = new StreamWriter(_logPath, true, new UTF8Encoding(false)))
                    {
                        writer.WriteLine(
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + message);
                        if (exception != null)
                        {
                            writer.WriteLine(exception.ToString());
                        }
                    }
                }
                catch
                {
                    // Logging must never break the tray application.
                }
            }
        }
    }
}
