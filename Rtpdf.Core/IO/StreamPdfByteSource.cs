namespace Rtpdf.Core.IO;

internal sealed class StreamPdfByteSource : PdfByteSource
{
    private readonly object _syncRoot = new();
    private readonly bool _leaveOpen;
    private Stream? _source;
    private readonly long _startPosition;
    private readonly long _length;

    public StreamPdfByteSource(Stream source, bool leaveOpen)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead)
        {
            throw new ArgumentException("The PDF source stream must be readable", nameof(source));
        }

        if (!source.CanSeek)
        {
            throw new ArgumentException("The PDF byte source stream must be seekable", nameof(source));
        }

        _source = source;
        _leaveOpen = leaveOpen;
        _startPosition = source.Position;
        _length = source.Length - _startPosition;
    }

    public override long Length
    {
        get
        {
            ThrowIfDisposed();
            return _length;
        }
    }

    public override int Read(long offset, Span<byte> destination)
    {
        lock (_syncRoot)
        {
            var source = GetSource();
            if (offset < 0 || offset > _length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            var count = (int)Math.Min(destination.Length, _length - offset);
            if (count == 0)
            {
                return 0;
            }

            var originalPosition = source.Position;
            try
            {
                source.Position = _startPosition + offset;
                return source.Read(destination[..count]);
            }
            finally
            {
                source.Position = originalPosition;
            }
        }
    }

    public override void Dispose()
    {
        lock (_syncRoot)
        {
            var source = _source;
            _source = null;
            if (source is not null && !_leaveOpen)
            {
                source.Dispose();
            }
        }

        base.Dispose();
    }

    internal static PdfByteSource Create(
        Stream source,
        bool leaveOpen,
        long maximumBufferedLength)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBufferedLength);
        if (!source.CanRead)
        {
            throw new ArgumentException("The PDF source stream must be readable", nameof(source));
        }

        if (source.CanSeek)
        {
            return new StreamPdfByteSource(source, leaveOpen);
        }

        try
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            while (true)
            {
                var read = source.Read(chunk);
                if (read == 0)
                {
                    break;
                }

                if (buffer.Length > maximumBufferedLength - read)
                {
                    throw new InvalidDataException(
                        $"PDF source exceeds the configured limit of {maximumBufferedLength} bytes");
                }

                buffer.Write(chunk, 0, read);
            }

            return new MemoryPdfByteSource(buffer.ToArray(), takeOwnership: true);
        }
        finally
        {
            if (!leaveOpen)
            {
                source.Dispose();
            }
        }
    }

    private Stream GetSource()
    {
        ThrowIfDisposed();
        return _source!;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_source is null, this);
    }
}
