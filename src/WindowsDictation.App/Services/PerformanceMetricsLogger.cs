using System.IO;

namespace WindowsDictation.App.Services;

public sealed class PerformanceMetricsLogger
{
    private readonly object sync = new();
    private readonly string logPath;

    public PerformanceMetricsLogger(AppPaths paths)
    {
        logPath = Path.Combine(paths.RootDirectory, "performance.log");
    }

    public void Log(string operation, TimeSpan elapsed, string details)
    {
        string line = $"{DateTimeOffset.Now:O}\t{operation}\t{elapsed.TotalMilliseconds:F0} ms\t{details}{Environment.NewLine}";
        lock (sync)
        {
            File.AppendAllText(logPath, line);
        }
    }
}
