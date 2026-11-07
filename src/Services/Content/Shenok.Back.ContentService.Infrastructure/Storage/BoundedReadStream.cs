namespace Shenok.Back.ContentService.Infrastructure.Storage;

/// <summary>
/// Reads at most a declared number of bytes and leaves the inner stream open.
/// </summary>
internal sealed class BoundedReadStream : Stream
{
    private readonly Stream _inner;
    private readonly long _limit;
    private long _read;

    public BoundedReadStream(Stream inner, long limit)
    {
        ArgumentNullException.ThrowIfNull(inner);
        if (limit <= 0)
            throw new ArgumentOutOfRangeException(nameof(limit));

        _inner = inner;
        _limit = limit;
    }

    public long BytesRead => _read;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var allowed = Allow(count);
        if (allowed == 0)
            return 0;

        var read = _inner.Read(buffer, offset, allowed);
        _read += read;
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        var allowed = Allow(buffer.Length);
        if (allowed == 0)
            return 0;

        var read = _inner.Read(buffer[..allowed]);
        _read += read;
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var allowed = Allow(buffer.Length);
        if (allowed == 0)
            return 0;

        var read = await _inner.ReadAsync(buffer[..allowed], cancellationToken);
        _read += read;
        return read;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        var allowed = Allow(count);
        if (allowed == 0)
            return Task.FromResult(0);

        return ReadArrayAsync(buffer, offset, allowed, cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
    }

    private async Task<int> ReadArrayAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var read = await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        _read += read;
        return read;
    }

    private int Allow(int requested)
    {
        if (requested < 0)
            throw new ArgumentOutOfRangeException(nameof(requested));

        var remaining = _limit - _read;
        if (remaining <= 0 || requested == 0)
            return 0;

        return requested < remaining ? requested : (int)Math.Min(remaining, int.MaxValue);
    }
}
