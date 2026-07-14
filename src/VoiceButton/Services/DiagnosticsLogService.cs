using System.IO;

namespace VoiceButton.Services;

public sealed class DiagnosticsLogService
{
    public string LogPath { get; }

    public DiagnosticsLogService()
    {
        LogPath = Path.Combine(AppStorage.DataDirectory, "diagnostics.log");
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
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {area}: {message.ReplaceLineEndings(" ")}";
            File.AppendAllLines(LogPath, [line]);
        }
        catch
        {
            // Diagnostics logging must never break the main workflow.
        }
    }
}
