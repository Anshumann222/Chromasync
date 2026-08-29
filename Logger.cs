using System.Diagnostics;

namespace ChromaSync;

public static class Logger
{
    private static readonly object _lock = new();
    private static readonly string _logPath = Path.Combine(AppContext.BaseDirectory, "chromasync.log");
    private const long MaxLogSizeBytes = 2 * 1024 * 1024; // 2 MB

    public static string LogFilePath => _logPath;

    public static void Info(string message) => Log("INFO", message);
    public static void Warn(string message) => Log("WARN", message);
    public static void Error(string message) => Log("ERROR", message);
    public static void Debug(string message) => Log("DEBUG", message);

    private static void Log(string level, string message)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string line = $"[{timestamp}] [{level}] {message}";

        try
        {
            Console.WriteLine(line);
            System.Diagnostics.Debug.WriteLine(line);
        }
        catch { }

        lock (_lock)
        {
            try
            {
                RollLogFileIfNeeded();
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
            catch
            {
                // Silently ignore disk write failures to prevent crashing
            }
        }
    }

    private static void RollLogFileIfNeeded()
    {
        try
        {
            if (File.Exists(_logPath))
            {
                var fi = new FileInfo(_logPath);
                if (fi.Length > MaxLogSizeBytes)
                {
                    string oldPath = _logPath + ".old";
                    if (File.Exists(oldPath))
                    {
                        File.Delete(oldPath);
                    }
                    File.Move(_logPath, oldPath);
                }
            }
        }
        catch
        {
            // Ignore rotation errors
        }
    }
}
