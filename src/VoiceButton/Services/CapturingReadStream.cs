using System.IO;

namespace VoiceButton.Services;

internal sealed class CapturingReadStream : Stream
{
    private readonly Stream _inner;
    private readonly string _temporaryPath;
    private FileStream? _captured;
    private bool _committed;

    public CapturingReadStream(Stream inner, string temporaryPath)
    {
        _inner = inner;
        _temporaryPath = temporaryPath;
        Directory.CreateDirectory(Path.GetDirectoryName(temporaryPath)!);
        _captured = new FileStream(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            64 * 1024,
            FileOptions.SequentialScan);
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => CapturedLength;
        set => throw new NotSupportedException();
    }

    public bool IsComplete { get; private set; }

    public long CapturedLength => _captured?.Length
        ?? (File.Exists(_temporaryPath) ? new FileInfo(_temporaryPath).Length : 0);

    public void CommitTo(string finalPath)
    {
        if (!IsComplete)
        {
            throw new InvalidOperationException("Нельзя сохранить незавершенный аудиопоток.");
        }

        _captured?.Flush(flushToDisk: true);
        _captured?.Dispose();
        _captured = null;
        File.Move(_temporaryPath, finalPath, overwrite: true);
        _committed = true;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        if (read > 0)
        {
            _captured!.Write(buffer, offset, read);
        }
        else
        {
            IsComplete = true;
        }

        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        var read = _inner.Read(buffer);
        if (read > 0)
        {
            _captured!.Write(buffer[..read]);
        }
        else
        {
            IsComplete = true;
        }

        return read;
    }

    public override int ReadByte()
    {
        var value = _inner.ReadByte();
        if (value >= 0)
        {
            _captured!.WriteByte((byte)value);
        }
        else
        {
            IsComplete = true;
        }

        return value;
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _captured?.Dispose();
            _captured = null;
            if (!_committed)
            {
                try
                {
                    File.Delete(_temporaryPath);
                }
                catch
                {
                    // Cache cleanup is best effort and must not mask playback errors.
                }
            }
        }

        base.Dispose(disposing);
    }
}
