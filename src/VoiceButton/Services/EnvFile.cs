using System.IO;

namespace VoiceButton.Services;

public static class EnvFile
{
    public static string? LoadNearest()
    {
        foreach (var candidate in EnumerateCandidateFiles())
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            Load(candidate);
            return candidate;
        }

        return null;
    }

    public static void DeletePortableFiles()
    {
        if (!AppStorage.IsPortable)
        {
            return;
        }

        DeleteIfExists(Path.Combine(AppContext.BaseDirectory, ".env"));
        DeleteIfExists(Path.Combine(AppContext.BaseDirectory, ".env.local"));
    }

    private static void Load(string path)
    {
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"');
            if (name.Length == 0 || Environment.GetEnvironmentVariable(name) is { Length: > 0 })
            {
                continue;
            }

            Environment.SetEnvironmentVariable(name, value);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static IEnumerable<string> EnumerateCandidateFiles()
    {
        var startDirectories = new[]
        {
            Environment.CurrentDirectory,
            AppContext.BaseDirectory
        };

        foreach (var start in startDirectories)
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                yield return Path.Combine(directory.FullName, ".env");
                yield return Path.Combine(directory.FullName, ".env.local");
                directory = directory.Parent;
            }
        }
    }
}
