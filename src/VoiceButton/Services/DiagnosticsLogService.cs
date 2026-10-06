using System.IO;

namespace VoiceButton.Services;

public sealed class DiagnosticsLogService
{
    private const long MaxLogBytes = 5L * 1024 * 1024;
    private const int ArchiveCount = 2;
    private static readonly TimeSpan MaxLogAge = TimeSpan.FromDays(3);
    private readonly object _gate = new();

    public string LogPath { get; }

    public DiagnosticsLogService()
    {
        LogPath = Path.Combine(AppStorage.DataDirectory, "diagnostics.log");
        PruneOldLogs();
    }

    public void Info(string area, string message)
    {
        Write("INFO", area, message);
    }

    public void Error(string area, Exception exception)
    {
        Write("ERROR", area, exception.ToString());
    }

    private void Write(string level, string area, string message)
    {
        lock (_gate)
        {
            try
            {
                var directory = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                RotateIfNeeded();
                var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {area}: {message.ReplaceLineEndings(" ")}";
                File.AppendAllLines(LogPath, [line]);
            }
            catch
            {
                // Diagnostics logging must never break the main workflow.
            }
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(LogPath) || new FileInfo(LogPath).Length < MaxLogBytes)
        {
            return;
        }

        for (var index = ArchiveCount; index >= 1; index--)
        {
            var source = index == 1 ? LogPath : GetArchivePath(index - 1);
            var destination = GetArchivePath(index);
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            if (File.Exists(source))
            {
                File.Move(source, destination);
            }
        }
    }

    private void PruneOldLogs()
    {
        try
        {
            var cutoff = DateTime.UtcNow - MaxLogAge;
            foreach (var path in Enumerable.Range(0, ArchiveCount + 1)
                         .Select(index => index == 0 ? LogPath : GetArchivePath(index)))
            {
                if (File.Exists(path) && File.GetLastWriteTimeUtc(path) < cutoff)
                {
                    File.Delete(path);
                }
            }
        }
        catch
        {
            // Old diagnostics will be retried on the next launch.
        }
    }

    private string GetArchivePath(int index)
    {
        var directory = Path.GetDirectoryName(LogPath) ?? string.Empty;
        return Path.Combine(directory, $"diagnostics.{index}.log");
    }
}
