namespace Rtpdf.Core.IO;

internal sealed class MemoryPdfByteSource : PdfByteSource
{
    private byte[]? _source;

    public MemoryPdfByteSource(ReadOnlyMemory<byte> source)
    {
        _source = source.ToArray();
    }

    internal MemoryPdfByteSource(byte[] source, bool takeOwnership)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = takeOwnership ? source : (byte[])source.Clone();
    }

    public override long Length => GetSource().Length;

    public override int Read(long offset, Span<byte> destination)
    {
        var source = GetSource();
        if (offset < 0 || offset > source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var count = Math.Min(destination.Length, source.Length - (int)offset);
        source.AsSpan((int)offset, count).CopyTo(destination);
        return count;
    }

    public override void Dispose()
    {
        _source = null;
        base.Dispose();
    }

    private byte[] GetSource() =>
        _source ?? throw new ObjectDisposedException(nameof(PdfByteSource));
}
