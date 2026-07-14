using System.IO;

namespace VoiceButton.Services;

internal sealed class CapturingReadStream(Stream inner) : Stream
{
    private readonly MemoryStream _captured = new();

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => _captured.Length;
        set => throw new NotSupportedException();
    }

    public byte[] ToArray() => _captured.ToArray();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = inner.Read(buffer, offset, count);
        if (read > 0)
        {
            _captured.Write(buffer, offset, read);
        }

        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        var read = inner.Read(buffer);
        if (read > 0)
        {
            _captured.Write(buffer[..read]);
        }

        return read;
    }

    public override int ReadByte()
    {
        var value = inner.ReadByte();
        if (value >= 0)
        {
            _captured.WriteByte((byte)value);
        }

        return value;
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}