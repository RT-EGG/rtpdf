using System.Text;
using Rtpdf.Core.Documents;
using Rtpdf.Core.IO;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Storage;
using Xunit;

namespace Rtpdf.Core.Tests.Documents;

public sealed class PdfDocumentReaderTests
{
    [Fact]
    public void OpenMemoryBuildsTheDocumentFacade()
    {
        var source = CreateDocument();

        using var document = PdfDocumentReader.Open(source.AsMemory());

        Assert.Equal(source.Length, document.SourceLength);
        Assert.Equal(4, document.CrossReferences.Count);
        Assert.Equal(1, document.Catalog.ObjectId.ObjectNumber);
        Assert.Empty(document.Catalog.PageTree.Pages);
        Assert.NotNull(document.StreamDecoder);
    }

    [Fact]
    public void OpenStreamReadsFromItsCurrentPositionAndHonorsLeaveOpen()
    {
        var source = CreateDocument();
        var stream = new MemoryStream([1, 2, 3, .. source]);
        stream.Position = 3;

        using var document = PdfDocumentReader.Open(stream, leaveOpen: true);

        Assert.Equal(source.Length, document.SourceLength);
        Assert.Equal(3, stream.Position);
        Assert.True(stream.CanRead);

        var ownedStream = new MemoryStream(source);
        using var ownedDocument = PdfDocumentReader.Open(ownedStream);
        Assert.True(ownedStream.CanRead);
        ownedDocument.Dispose();
        Assert.False(ownedStream.CanRead);
    }

    [Fact]
    public void OpenSupportsANonSeekableStream()
    {
        var stream = new NonSeekableReadStream(CreateDocument());

        using var document = PdfDocumentReader.Open(stream, leaveOpen: true);

        Assert.Equal(1, document.Catalog.ObjectId.ObjectNumber);
        Assert.False(stream.IsDisposed);
        stream.Dispose();
    }

    [Fact]
    public void OpenPathKeepsTheFileForLazyReadsUntilDocumentIsDisposed()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, CreateDocument());

            using var document = PdfDocumentReader.Open(path);

            Assert.Equal(1, document.Catalog.ObjectId.ObjectNumber);
            document.Dispose();
            File.Delete(path);
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void OpenReadsAChunkedCallerOwnedByteSourceWithoutDisposingIt()
    {
        var source = new ChunkedByteSource(CreateDocument());

        using var document = PdfDocumentReader.Open(source);

        Assert.Equal(1, document.Catalog.ObjectId.ObjectNumber);
        Assert.True(source.ReadCount > 1);
        Assert.False(source.IsDisposed);
        source.Dispose();
    }

    [Fact]
    public void ObjectStoreReadsAnUncachedObjectFromItsXrefRangeOnDemand()
    {
        var source = new ChunkedByteSource(CreateDocument());
        using var document = PdfDocumentReader.Open(source);
        Assert.False(document.ObjectStore.TryGetCachedObject(new PdfObjectId(3), out _));
        source.ResetReadCount();

        var value = document.ObjectStore.Resolve<PdfString>(new PdfIndirectReference(3));

        Assert.Equal("lazy", value.GetText());
        Assert.True(source.ReadCount > 0);
    }

    [Fact]
    public void OpenRejectsSourcesAboveTheConfiguredLimitBeforeReading()
    {
        var source = new ChunkedByteSource(CreateDocument());
        var options = new PdfDocumentReaderOptions
        {
            MaximumSourceLength = source.Length - 1,
        };

        Assert.Throws<InvalidDataException>(() =>
            PdfDocumentReader.Open(source, options));
        Assert.Equal(0, source.ReadCount);
    }

    [Fact]
    public void OpenStopsBufferingANonSeekableStreamAtTheSourceLimit()
    {
        var stream = new NonSeekableReadStream(CreateDocument());
        var options = new PdfDocumentReaderOptions { MaximumSourceLength = 10 };

        Assert.Throws<InvalidDataException>(() =>
            PdfDocumentReader.Open(stream, options));
        Assert.True(stream.IsDisposed);
    }

    [Fact]
    public void OpenRejectsASeekableStreamAboveTheConfiguredLimitBeforeReading()
    {
        var stream = new MemoryStream(CreateDocument());
        var options = new PdfDocumentReaderOptions { MaximumSourceLength = 10 };

        Assert.Throws<InvalidDataException>(() =>
            PdfDocumentReader.Open(stream, options, leaveOpen: true));
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void OpenForwardsNestedReaderOptions()
    {
        var options = new PdfDocumentReaderOptions
        {
            ObjectStoreOptions = new PdfObjectStoreOptions
            {
                MaximumCachedObjects = 1,
            },
        };

        Assert.ThrowsAny<IOException>(() =>
            PdfDocumentReader.Open(CreateDocument().AsMemory(), options));
    }

    [Fact]
    public void OpenValidatesReaderOptions()
    {
        var options = new PdfDocumentReaderOptions { MaximumSourceLength = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PdfDocumentReader.Open(CreateDocument().AsMemory(), options));
    }

    private static byte[] CreateDocument()
    {
        var source = new StringBuilder("%PDF-1.7\n");
        var catalogOffset = Encoding.ASCII.GetByteCount(source.ToString());
        source.Append("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        var pagesOffset = Encoding.ASCII.GetByteCount(source.ToString());
        source.Append("2 0 obj\n<< /Type /Pages /Kids [] /Count 0 >>\nendobj\n");
        var lazyOffset = Encoding.ASCII.GetByteCount(source.ToString());
        source.Append("3 0 obj\n(lazy)\nendobj\n");
        var xrefOffset = Encoding.ASCII.GetByteCount(source.ToString());
        source.Append("xref\n0 4\n")
            .Append("0000000000 65535 f \n")
            .Append(catalogOffset.ToString("D10")).Append(" 00000 n \n")
            .Append(pagesOffset.ToString("D10")).Append(" 00000 n \n")
            .Append(lazyOffset.ToString("D10")).Append(" 00000 n \n")
            .Append("trailer\n<< /Size 4 /Root 1 0 R >>\n")
            .Append("startxref\n").Append(xrefOffset).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(source.ToString());
    }

    private sealed class ChunkedByteSource(byte[] source) : PdfByteSource
    {
        public bool IsDisposed { get; private set; }

        public int ReadCount { get; private set; }

        public override long Length => source.Length;

        public override int Read(long offset, Span<byte> destination)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            ReadCount++;
            var count = Math.Min(Math.Min(destination.Length, 7), source.Length - (int)offset);
            source.AsSpan((int)offset, count).CopyTo(destination);
            return count;
        }

        public override void Dispose()
        {
            IsDisposed = true;
            base.Dispose();
        }

        public void ResetReadCount() => ReadCount = 0;
    }

    private sealed class NonSeekableReadStream(byte[] source) : Stream
    {
        private readonly MemoryStream _inner = new(source);

        public bool IsDisposed { get; private set; }

        public override bool CanRead => !IsDisposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => _inner.Read(buffer);

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
