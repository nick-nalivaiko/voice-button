using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VoiceButton.Services;

internal readonly record struct SpeechAudioCacheTarget(string TemporaryPath, string FinalPath);

internal sealed class SpeechAudioCache
{
    private const int ManifestVersion = 1;
    private readonly object _gate = new();
    private readonly string _rootDirectory;
    private readonly int _maxEntries;
    private readonly long _maxBytes;
    private readonly TimeSpan _maxAge;

    public SpeechAudioCache() : this(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoiceButton",
            "AudioCache"),
        maxEntries: 20,
        maxBytes: 250L * 1024 * 1024,
        maxAge: TimeSpan.FromDays(3))
    {
    }

    internal SpeechAudioCache(string rootDirectory, int maxEntries, long maxBytes, TimeSpan maxAge)
    {
        _rootDirectory = rootDirectory;
        _maxEntries = maxEntries;
        _maxBytes = maxBytes;
        _maxAge = maxAge;
        Prune();
    }

    public string CreateKey(string text, string configuration)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{configuration}\n{text}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public bool TryGetEntry(string key, int expectedChunkCount, out IReadOnlyList<string> paths)
    {
        lock (_gate)
        {
            paths = [];
            try
            {
                var directory = GetEntryDirectory(key);
                var manifestPath = Path.Combine(directory, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    return false;
                }

                var manifest = JsonSerializer.Deserialize<CacheManifest>(File.ReadAllText(manifestPath));
                if (manifest is null
                    || manifest.Version != ManifestVersion
                    || manifest.ChunkCount != expectedChunkCount
                    || DateTime.UtcNow - manifest.CreatedUtc > _maxAge)
                {
                    TryDeleteDirectory(directory);
                    return false;
                }

                var files = Enumerable.Range(0, expectedChunkCount)
                    .Select(index => Path.Combine(directory, $"{index:D3}.mp3"))
                    .ToArray();
                if (files.Any(path => !File.Exists(path) || new FileInfo(path).Length == 0))
                {
                    TryDeleteDirectory(directory);
                    return false;
                }

                manifest.LastAccessUtc = DateTime.UtcNow;
                WriteManifest(manifestPath, manifest);
                paths = files;
                return true;
            }
            catch
            {
                paths = [];
                return false;
            }
        }
    }

    public void BeginEntry(string key)
    {
        lock (_gate)
        {
            var directory = GetEntryDirectory(key);
            TryDeleteDirectory(directory);
            Directory.CreateDirectory(directory);
        }
    }

    public SpeechAudioCacheTarget GetChunkTarget(string key, int index)
    {
        var directory = GetEntryDirectory(key);
        return new SpeechAudioCacheTarget(
            Path.Combine(directory, $"{index:D3}.mp3.part"),
            Path.Combine(directory, $"{index:D3}.mp3"));
    }

    public void CompleteEntry(string key, int chunkCount)
    {
        lock (_gate)
        {
            var directory = GetEntryDirectory(key);
            var now = DateTime.UtcNow;
            WriteManifest(
                Path.Combine(directory, "manifest.json"),
                new CacheManifest(ManifestVersion, chunkCount, now, now));
            PruneLocked(key);
        }
    }

    public void AbortEntry(string key)
    {
        lock (_gate)
        {
            TryDeleteDirectory(GetEntryDirectory(key));
        }
    }

    public void Prune()
    {
        lock (_gate)
        {
            PruneLocked(protectedKey: null);
        }
    }

    private void PruneLocked(string? protectedKey)
    {
        try
        {
            Directory.CreateDirectory(_rootDirectory);
            var now = DateTime.UtcNow;
            var entries = new List<CacheEntry>();
            foreach (var directory in Directory.EnumerateDirectories(_rootDirectory))
            {
                var manifestPath = Path.Combine(directory, "manifest.json");
                CacheManifest? manifest = null;
                try
                {
                    if (File.Exists(manifestPath))
                    {
                        manifest = JsonSerializer.Deserialize<CacheManifest>(File.ReadAllText(manifestPath));
                    }
                }
                catch
                {
                    // Invalid entries are removed below.
                }

                if (manifest is null
                    || manifest.Version != ManifestVersion
                    || now - manifest.CreatedUtc > _maxAge)
                {
                    TryDeleteDirectory(directory);
                    continue;
                }

                var size = Directory.EnumerateFiles(directory, "*.mp3")
                    .Sum(path => new FileInfo(path).Length);
                entries.Add(new CacheEntry(
                    directory,
                    Path.GetFileName(directory),
                    manifest.LastAccessUtc,
                    size));
            }

            var totalBytes = entries.Sum(entry => entry.Size);
            protectedKey ??= entries.MaxBy(entry => entry.LastAccessUtc)?.Key;
            var removable = entries
                .Where(entry => !string.Equals(entry.Key, protectedKey, StringComparison.Ordinal))
                .OrderBy(entry => entry.LastAccessUtc)
                .ToList();
            while ((entries.Count > _maxEntries || totalBytes > _maxBytes) && removable.Count > 0)
            {
                var entry = removable[0];
                removable.RemoveAt(0);
                TryDeleteDirectory(entry.Directory);
                entries.Remove(entry);
                totalBytes -= entry.Size;
            }
        }
        catch
        {
            // Cache maintenance must never prevent speech playback.
        }
    }

    private string GetEntryDirectory(string key) => Path.Combine(_rootDirectory, key);

    private static void WriteManifest(string path, CacheManifest manifest)
    {
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(manifest));
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Locked files will be retried by a later prune.
        }
    }

    private sealed record CacheEntry(string Directory, string Key, DateTime LastAccessUtc, long Size);

    private sealed class CacheManifest
    {
        public CacheManifest(int version, int chunkCount, DateTime createdUtc, DateTime lastAccessUtc)
        {
            Version = version;
            ChunkCount = chunkCount;
            CreatedUtc = createdUtc;
            LastAccessUtc = lastAccessUtc;
        }

        public int Version { get; set; }
        public int ChunkCount { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime LastAccessUtc { get; set; }
    }
}
