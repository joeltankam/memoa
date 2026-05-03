namespace Memoa.Internal;

/// <summary>
/// A wrapping stream that captures bytes written to it up to a maximum size,
/// while still forwarding all writes to the inner stream.
/// </summary>
internal sealed class ResponseCaptureStream : Stream
{
    private readonly Stream _innerStream;
    private readonly MemoryStream _buffer;
    private readonly int _maxSize;
    private bool _truncated;

    public ResponseCaptureStream(Stream innerStream, int maxSize)
    {
        _innerStream = innerStream;
        _buffer = new MemoryStream();
        _maxSize = maxSize;
    }

    public bool Truncated => _truncated;

    public byte[] GetCapturedBytes()
    {
        return _buffer.ToArray();
    }

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => _innerStream.CanSeek;
    public override bool CanWrite => _innerStream.CanWrite;
    public override long Length => _innerStream.Length;

    public override long Position
    {
        get => _innerStream.Position;
        set => _innerStream.Position = value;
    }

    public override void Flush()
    {
        _innerStream.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return _innerStream.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _innerStream.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _innerStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _innerStream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        CaptureBytes(buffer.AsSpan(offset, count));
        _innerStream.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        CaptureBytes(buffer);
        _innerStream.Write(buffer);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        CaptureBytes(buffer.AsSpan(offset, count));
        await _innerStream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        CaptureBytes(buffer.Span);
        await _innerStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    private void CaptureBytes(ReadOnlySpan<byte> data)
    {
        if (_buffer.Length >= _maxSize)
        {
            _truncated = true;
            return;
        }

        var remaining = _maxSize - (int)_buffer.Length;
        var toWrite = Math.Min(data.Length, remaining);

        _buffer.Write(data[..toWrite]);

        if (toWrite < data.Length)
        {
            _truncated = true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _buffer.Dispose();
        }

        base.Dispose(disposing);
    }
}
