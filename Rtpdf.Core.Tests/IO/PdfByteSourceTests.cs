using Rtpdf.Core.IO;
using Xunit;

namespace Rtpdf.Core.Tests.IO;

public sealed class PdfByteSourceTests
{
    [Fact]
    public void MemorySourceCopiesInputAndReadsBoundedRanges()
    {
        var input = new byte[] { 1, 2, 3, 4 };
        using var source = PdfByteSource.FromMemory(input);
        input[1] = 9;
        var output = new byte[4];

        var count = source.Read(1, output);

        Assert.Equal(3, count);
        Assert.Equal(new byte[] { 2, 3, 4, 0 }, output);
        Assert.Equal(0, source.Read(source.Length, output));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.Read(-1, output));
    }

    [Fact]
    public void StreamSourceReadsRelativeToTheInitialPositionAndRestoresIt()
    {
        using var stream = new MemoryStream([9, 8, 1, 2, 3]);
        stream.Position = 2;
        using var source = PdfByteSource.FromStream(stream, leaveOpen: true);
        Span<byte> output = stackalloc byte[2];

        source.ReadExactly(1, output);

        Assert.Equal(new byte[] { 2, 3 }, output.ToArray());
        Assert.Equal(2, stream.Position);
    }

    [Fact]
    public void ReadExactlyRejectsAnOutOfRangeOrShortRead()
    {
        using var source = PdfByteSource.FromMemory(new byte[] { 1, 2 });
        Assert.Throws<EndOfStreamException>(() => source.ReadExactly(1, new byte[2]));

        using var shortSource = new ShortByteSource();
        Assert.Throws<EndOfStreamException>(() => shortSource.ReadExactly(0, new byte[1]));
    }

    [Fact]
    public void DisposedSourceCannotBeRead()
    {
        var source = PdfByteSource.FromMemory(new byte[] { 1 });
        source.Dispose();

        Assert.Throws<ObjectDisposedException>(() => source.Read(0, new byte[1]));
        Assert.Throws<ObjectDisposedException>(() => _ = source.Length);
    }

    private sealed class ShortByteSource : PdfByteSource
    {
        public override long Length => 1;

        public override int Read(long offset, Span<byte> destination) => 0;
    }
}
