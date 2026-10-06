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
TestHighPassResponse(format);
TestNoxAddressing();

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
    $"PASS: {pcmCycles} PCM sessions, memory growth {memoryGrowth / 1024d / 1024d:F1} MiB, 100 Hz high-pass response, Nox routing, temp cleanup and bounded MP3 cache verified.");

static void TestNoxAddressing()
{
    Require(
        NoksIntegrationService.TryExtractAddressedMessage("Nox, включи вечерний режим", out var currentName)
        && currentName == "включи вечерний режим",
        "Current Nox address was not recognized.");
    Require(
        NoksIntegrationService.TryExtractAddressedMessage("Noks, проверь календарь", out var englishName)
        && englishName == "проверь календарь",
        "Legacy Noks address was not removed from a routed dictation.");
    Require(
        NoksIntegrationService.TryExtractAddressedMessage("Привет Нокс: напомни позвонить", out var russianName)
        && russianName == "напомни позвонить",
        "Russian Nox address was not recognized.");
    Require(
        NoksIntegrationService.TryExtractAddressedMessage("Hi Knox send the summary", out var asrVariant)
        && asrVariant == "send the summary",
        "Common ASR variant of Nox was not recognized.");
    Require(
        !NoksIntegrationService.TryExtractAddressedMessage("Сегодня обсуждали Noks в Codex", out _),
        "Nox mentioned inside ordinary dictation must not trigger routing.");
}

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

static void TestHighPassResponse(WaveFormat format)
{
    var lowFrequencyRms = MeasureFilteredRms(format, 40);
    var voiceFrequencyRms = MeasureFilteredRms(format, 1000);
    Require(
        lowFrequencyRms < voiceFrequencyRms * 0.22,
        $"100 Hz Linkwitz-Riley filter attenuation is too weak: 40 Hz={lowFrequencyRms:F4}, 1 kHz={voiceFrequencyRms:F4}.");
}

static double MeasureFilteredRms(WaveFormat format, double frequency)
{
    const int durationSeconds = 3;
    var sampleCount = format.SampleRate * durationSeconds;
    var source = new byte[sampleCount * sizeof(short)];
    for (var index = 0; index < sampleCount; index++)
    {
        var value = (short)(Math.Sin(2 * Math.PI * frequency * index / format.SampleRate) * short.MaxValue * 0.5);
        BitConverter.TryWriteBytes(source.AsSpan(index * sizeof(short), sizeof(short)), value);
    }

    using var provider = new ProgressiveWaveProvider(format);
    provider.Append(source, 0, source.Length);
    provider.Complete();
    var output = new byte[source.Length];
    var totalRead = 0;
    while (totalRead < output.Length)
    {
        var read = provider.Read(output, totalRead, output.Length - totalRead);
        if (read == 0)
        {
            break;
        }

        totalRead += read;
    }

    var skipSamples = format.SampleRate / 2;
    double squareSum = 0;
    var measuredSamples = 0;
    for (var index = skipSamples; index < totalRead / sizeof(short); index++)
    {
        var sample = BitConverter.ToInt16(output, index * sizeof(short)) / 32768d;
        squareSum += sample * sample;
        measuredSamples++;
    }

    return Math.Sqrt(squareSum / measuredSamples);
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
