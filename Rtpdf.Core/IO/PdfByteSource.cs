namespace Rtpdf.Core.IO;

/// <summary>
/// Provides length-bounded random access to the bytes of a PDF source.
/// </summary>
public abstract class PdfByteSource : IDisposable
{
    private const long DefaultMaximumBufferedStreamLength = 256L * 1024 * 1024;

    public abstract long Length { get; }

    /// <summary>
    /// Reads up to <paramref name="destination"/>.Length bytes beginning at the specified offset.
    /// Returns zero only when the offset is at the end of the source.
    /// </summary>
    public abstract int Read(long offset, Span<byte> destination);

    public void ReadExactly(long offset, Span<byte> destination)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var length = Length;
        if (offset > length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (destination.Length > length - offset)
        {
            throw new EndOfStreamException("The requested range extends beyond the PDF byte source");
        }

        var totalRead = 0;
        while (totalRead < destination.Length)
        {
            var read = Read(offset + totalRead, destination[totalRead..]);
            if (read <= 0 || read > destination.Length - totalRead)
            {
                throw new EndOfStreamException("The PDF byte source ended before the requested range was read");
            }

            totalRead += read;
        }
    }

    public static PdfByteSource FromMemory(ReadOnlyMemory<byte> source) =>
        new MemoryPdfByteSource(source);

    public static PdfByteSource FromStream(
        Stream source,
        bool leaveOpen = false,
        long maximumBufferedLength = DefaultMaximumBufferedStreamLength) =>
        StreamPdfByteSource.Create(source, leaveOpen, maximumBufferedLength);

    public static PdfByteSource OpenFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new StreamPdfByteSource(
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read),
            leaveOpen: false);
    }

    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
