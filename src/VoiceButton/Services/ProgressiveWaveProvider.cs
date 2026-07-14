using NAudio.Wave;

namespace VoiceButton.Services;

internal readonly record struct ProgressiveAudioState(
    TimeSpan Position,
    TimeSpan DownloadedDuration,
    TimeSpan BufferedDuration,
    bool IsComplete,
    bool HasRemainingAudio);

internal sealed class ProgressiveWaveProvider : IWaveProvider
{
    private readonly object _gate = new();
    private byte[] _audio;
    private int _length;
    private int _position;
    private bool _isComplete;

    public ProgressiveWaveProvider(WaveFormat waveFormat)
    {
        WaveFormat = waveFormat;
        _audio = new byte[Math.Max(waveFormat.AverageBytesPerSecond * 12, 4096)];
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
            var requiredLength = checked(_length + count);
            EnsureCapacity(requiredLength);
            Buffer.BlockCopy(source, offset, _audio, _length, count);
            _length = requiredLength;
        }
    }

    public void Complete()
    {
        lock (_gate)
        {
            _isComplete = true;
        }
    }

    public void Seek(double progress)
    {
        lock (_gate)
        {
            var target = (int)Math.Round(_length * Math.Clamp(progress, 0, 1));
            var blockAlign = Math.Max(1, WaveFormat.BlockAlign);
            target -= target % blockAlign;
            _position = Math.Clamp(target, 0, _length);
        }
    }

    public ProgressiveAudioState GetState()
    {
        lock (_gate)
        {
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
            var available = Math.Max(0, _length - _position);
            if (available == 0 && _isComplete)
            {
                return 0;
            }

            var copied = Math.Min(count, available);
            if (copied > 0)
            {
                Buffer.BlockCopy(_audio, _position, buffer, offset, copied);
                _position += copied;
            }

            if (copied == count || _isComplete)
            {
                return copied;
            }

            Array.Clear(buffer, offset + copied, count - copied);
            return count;
        }
    }

    private void EnsureCapacity(int requiredLength)
    {
        if (requiredLength <= _audio.Length)
        {
            return;
        }

        var newLength = _audio.Length;
        while (newLength < requiredLength)
        {
            newLength = checked(newLength * 2);
        }

        Array.Resize(ref _audio, newLength);
    }

    private TimeSpan DurationFromBytes(int byteCount)
    {
        return WaveFormat.AverageBytesPerSecond <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds((double)byteCount / WaveFormat.AverageBytesPerSecond);
    }
}
