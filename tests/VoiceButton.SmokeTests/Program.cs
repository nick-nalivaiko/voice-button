using System.Diagnostics;
using NAudio.Wave;
using VoiceButton.Services;

const int pcmCycles = 200;
const int secondsPerCycle = 10;
var pcmDirectory = Path.Combine(Path.GetTempPath(), "VoiceButton", "pcm");
var pcmFilesBefore = ExistingFiles(pcmDirectory);

GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
using var process = Process.GetCurrentProcess();
process.Refresh();
var privateBytesBefore = process.PrivateMemorySize64;

var format = new WaveFormat(16000, 16, 1);
var oneSecond = new byte[format.AverageBytesPerSecond];
var readBuffer = new byte[format.AverageBytesPerSecond];
for (var cycle = 0; cycle < pcmCycles; cycle++)
{
    using var provider = new ProgressiveWaveProvider(format);
    for (var second = 0; second < secondsPerCycle; second++)
    {
        provider.Append(oneSecond, 0, oneSecond.Length);
        var read = provider.Read(readBuffer, 0, readBuffer.Length);
        Require(read == readBuffer.Length, "PCM provider returned an incomplete buffered second.");
    }

    provider.Seek(0.5);
    Require(provider.GetState().Position > TimeSpan.Zero, "PCM seek did not update the position.");
    provider.Complete();
}

GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
process.Refresh();
var privateBytesAfter = process.PrivateMemorySize64;
var memoryGrowth = Math.Max(0, privateBytesAfter - privateBytesBefore);
Require(memoryGrowth < 128L * 1024 * 1024, $"PCM soak grew private memory by {memoryGrowth:N0} bytes.");
Require(ExistingFiles(pcmDirectory).SetEquals(pcmFilesBefore), "PCM temporary files were not removed.");

var testRoot = Path.Combine(Path.GetTempPath(), "VoiceButton", "smoke", Guid.NewGuid().ToString("N"));
try
{
    TestCapturedStream(testRoot);
    TestSpeechCache(testRoot);
}
finally
{
    if (Directory.Exists(testRoot))
    {
        Directory.Delete(testRoot, recursive: true);
    }
}

Console.WriteLine(
    $"PASS: {pcmCycles} PCM sessions, memory growth {memoryGrowth / 1024d / 1024d:F1} MiB, temp cleanup and bounded MP3 cache verified.");

static void TestCapturedStream(string root)
{
    var payload = new byte[1024 * 1024];
    Random.Shared.NextBytes(payload);
    var temporaryPath = Path.Combine(root, "capture", "audio.mp3.part");
    var finalPath = Path.Combine(root, "capture", "audio.mp3");
    using var input = new MemoryStream(payload, writable: false);
    using (var capture = new CapturingReadStream(input, temporaryPath))
    {
        var buffer = new byte[8192];
        while (capture.Read(buffer, 0, buffer.Length) > 0)
        {
        }

        Require(capture.IsComplete, "Captured stream did not observe end-of-stream.");
        capture.CommitTo(finalPath);
    }

    Require(File.Exists(finalPath), "Captured MP3 was not committed.");
    Require(new FileInfo(finalPath).Length == payload.Length, "Captured MP3 length changed.");
    Require(!File.Exists(temporaryPath), "Captured MP3 temporary file remained after commit.");
}

static void TestSpeechCache(string root)
{
    var cacheRoot = Path.Combine(root, "cache");
    var cache = new SpeechAudioCache(
        cacheRoot,
        maxEntries: 5,
        maxBytes: 8L * 1024 * 1024,
        maxAge: TimeSpan.FromDays(3));
    var payload = new byte[2 * 1024 * 1024];
    string? newestKey = null;
    for (var index = 0; index < 8; index++)
    {
        newestKey = cache.CreateKey($"text-{index}", "test-configuration");
        cache.BeginEntry(newestKey);
        var target = cache.GetChunkTarget(newestKey, 0);
        File.WriteAllBytes(target.FinalPath, payload);
        cache.CompleteEntry(newestKey, 1);
    }

    var directories = Directory.GetDirectories(cacheRoot);
    var totalBytes = Directory.GetFiles(cacheRoot, "*.mp3", SearchOption.AllDirectories)
        .Sum(path => new FileInfo(path).Length);
    Require(directories.Length <= 5, "Speech cache exceeded the entry limit.");
    Require(totalBytes <= 8L * 1024 * 1024, "Speech cache exceeded the byte limit.");
    Require(newestKey is not null && Directory.Exists(Path.Combine(cacheRoot, newestKey)), "Newest completed cache entry was evicted.");
    Require(cache.TryGetEntry(newestKey!, 1, out var paths) && paths.Count == 1, "Newest cache entry cannot be replayed.");
}

static HashSet<string> ExistingFiles(string path)
{
    return Directory.Exists(path)
        ? Directory.GetFiles(path).ToHashSet(StringComparer.OrdinalIgnoreCase)
        : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
