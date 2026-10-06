using System.Buffers.Binary;
using System.IO;
using NAudio.Dsp;
using NAudio.Wave;

namespace VoiceButton.Services;

internal readonly record struct ProgressiveAudioState(
    TimeSpan Position,
    TimeSpan DownloadedDuration,
    TimeSpan BufferedDuration,
    bool IsComplete,
    bool HasRemainingAudio);

internal sealed class ProgressiveWaveProvider : IWaveProvider, IDisposable
{
    private const long MaxPcmBytes = 512L * 1024 * 1024;
    private const float HighPassCutoffHz = 100f;
    private const float LinkwitzRileyQ = 0.5f;
    private readonly object _gate = new();
    private readonly FileStream _audio;
    private BiQuadFilter[] _highPassFilters;
    private long _length;
    private long _position;
    private bool _isComplete;
    private bool _disposed;

    public ProgressiveWaveProvider(WaveFormat waveFormat)
    {
        if (waveFormat.Encoding != WaveFormatEncoding.Pcm || waveFormat.BitsPerSample != 16)
        {
            throw new NotSupportedException("Фильтр воспроизведения поддерживает только 16-битный PCM.");
        }

        WaveFormat = waveFormat;
        _highPassFilters = CreateHighPassFilters();
        var directory = Path.Combine(Path.GetTempPath(), "VoiceButton", "pcm");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.pcm");
        _audio = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.Read,
            64 * 1024,
            FileOptions.DeleteOnClose | FileOptions.RandomAccess);
    }

    public WaveFormat WaveFormat { get; }

    public void Append(byte[] source, int offset, int count)
    {
        if (count <= 0)
        {
            return;
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var requiredLength = checked(_length + count);
            if (requiredLength > MaxPcmBytes)
            {
                throw new InvalidDataException("Декодированное аудио превысило безопасный лимит 512 МБ.");
            }

            _audio.Position = _length;
            _audio.Write(source, offset, count);
            _length = requiredLength;
        }
    }

    public void Complete()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _isComplete = true;
        }
    }

    public void Seek(double progress)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var target = (long)Math.Round(_length * Math.Clamp(progress, 0, 1));
            var blockAlign = Math.Max(1, WaveFormat.BlockAlign);
            target -= target % blockAlign;
            _position = Math.Clamp(target, 0, _length);
            _highPassFilters = CreateHighPassFilters();
        }
    }

    public ProgressiveAudioState GetState()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return new ProgressiveAudioState(
                    TimeSpan.Zero,
                    TimeSpan.Zero,
                    TimeSpan.Zero,
                    true,
                    false);
            }

            var remaining = Math.Max(0, _length - _position);
            return new ProgressiveAudioState(
                DurationFromBytes(_position),
                DurationFromBytes(_length),
                DurationFromBytes(remaining),
                _isComplete,
                remaining > 0);
        }
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return 0;
            }

            var available = Math.Max(0, _length - _position);
            if (available == 0 && _isComplete)
            {
                return 0;
            }

            var copied = (int)Math.Min(count, available);
            if (copied > 0)
            {
                _audio.Position = _position;
                copied = _audio.Read(buffer, offset, copied);
                _position += copied;
                ApplyHighPassFilter(buffer, offset, copied);
            }

            if (copied == count || _isComplete)
            {
                return copied;
            }

            Array.Clear(buffer, offset + copied, count - copied);
            return count;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _audio.Dispose();
        }
    }

    private TimeSpan DurationFromBytes(long byteCount)
    {
        return WaveFormat.AverageBytesPerSecond <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds((double)byteCount / WaveFormat.AverageBytesPerSecond);
    }

    private BiQuadFilter[] CreateHighPassFilters()
    {
        return Enumerable.Range(0, WaveFormat.Channels)
            .Select(_ => BiQuadFilter.HighPassFilter(WaveFormat.SampleRate, HighPassCutoffHz, LinkwitzRileyQ))
            .ToArray();
    }

    private void ApplyHighPassFilter(byte[] buffer, int offset, int count)
    {
        var sampleCount = count / sizeof(short);
        for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            var byteOffset = offset + (sampleIndex * sizeof(short));
            var sample = BinaryPrimitives.ReadInt16LittleEndian(buffer.AsSpan(byteOffset, sizeof(short))) / 32768f;
            var filtered = _highPassFilters[sampleIndex % WaveFormat.Channels].Transform(sample);
            var scaled = (int)Math.Round(filtered * 32768f);
            BinaryPrimitives.WriteInt16LittleEndian(
                buffer.AsSpan(byteOffset, sizeof(short)),
                (short)Math.Clamp(scaled, short.MinValue, short.MaxValue));
        }
    }
}
