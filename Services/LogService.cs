namespace QuickLaunch.Services;

public static class LogService
{
    private static readonly object Gate = new();
    private static string PathName => System.IO.Path.Combine(AppContext.BaseDirectory, "logs", "quicklaunch.log");
    public static void Info(string message) => Write("INFO", message);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", ex is null ? message : $"{message} | {ex}");
    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathName)!);
                File.AppendAllText(PathName, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
