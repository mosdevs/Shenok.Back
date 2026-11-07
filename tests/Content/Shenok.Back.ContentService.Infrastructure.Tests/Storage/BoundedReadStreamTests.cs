using Shenok.Back.ContentService.Infrastructure.Storage;

namespace Shenok.Back.ContentService.Infrastructure.Tests.Storage;

public sealed class BoundedReadStreamTests
{
    [Fact]
    public async Task Read_stops_at_the_declared_limit_and_leaves_the_rest_unread()
    {
        var inner = new MemoryStream(Enumerable.Repeat((byte)9, 100).ToArray());
        var bounded = new BoundedReadStream(inner, limit: 16);
        var buffer = new byte[64];

        var first = await bounded.ReadAsync(buffer);
        var second = await bounded.ReadAsync(buffer);

        Assert.Equal(16, first);
        Assert.Equal(0, second);
        Assert.Equal(16, bounded.BytesRead);
        Assert.Equal(16, inner.Position);
        Assert.All(buffer.Take(first), value => Assert.Equal(9, value));
    }

    [Fact]
    public void Read_reports_a_short_stream_without_inventing_bytes()
    {
        var inner = new MemoryStream([1, 2, 3, 4]);
        var bounded = new BoundedReadStream(inner, limit: 8);
        var buffer = new byte[8];

        var read = bounded.Read(buffer, 0, buffer.Length);

        Assert.Equal(4, read);
        Assert.Equal(4, bounded.BytesRead);
        Assert.Equal([1, 2, 3, 4], buffer.Take(4));
    }

    [Fact]
    public void Dispose_leaves_the_inner_stream_open()
    {
        var inner = new MemoryStream([1, 2, 3]);
        var bounded = new BoundedReadStream(inner, limit: 3);

        bounded.Dispose();

        Assert.Equal(1, inner.ReadByte());
    }

    [Fact]
    public void Constructor_rejects_a_missing_stream_or_a_non_positive_limit()
    {
        Assert.Throws<ArgumentNullException>(() => new BoundedReadStream(null!, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedReadStream(new MemoryStream([1]), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedReadStream(new MemoryStream([1]), -1));
    }

    [Fact]
    public void Stream_is_forward_only_and_not_writable()
    {
        var bounded = new BoundedReadStream(new MemoryStream([1]), 1);

        Assert.True(bounded.CanRead);
        Assert.False(bounded.CanSeek);
        Assert.False(bounded.CanWrite);
        Assert.Throws<NotSupportedException>(() => bounded.Length);
        Assert.Throws<NotSupportedException>(() => bounded.Position);
        Assert.Throws<NotSupportedException>(() => bounded.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => bounded.Write([1], 0, 1));
    }

    [Fact]
    public async Task Cancelled_read_does_not_advance_the_count()
    {
        var bounded = new BoundedReadStream(new BlockingStream(), 8);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            bounded.ReadAsync(new byte[4], 0, 4, cancellation.Token));
        Assert.Equal(0, bounded.BytesRead);
    }

    private sealed class BlockingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(0);
        }
    }
}
