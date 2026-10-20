using System.Text;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Documents;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Writing;
using Xunit;

namespace Rtpdf.Core.Tests.Writing;

public sealed class PdfDocumentWriterTests
{
    [Fact]
    public void WritesANewDocumentThatCanBeReadBack()
    {
        var builder = CreateEmptyDocument();
        using var destination = new MemoryStream();

        PdfDocumentWriter.Write(destination, builder, leaveOpen: true);
        var bytes = destination.ToArray();
        using var document = PdfDocumentReader.Open(bytes.AsMemory());

        Assert.Equal(3, document.CrossReferences.Count);
        Assert.Equal(2, document.Catalog.ObjectId.ObjectNumber);
        Assert.Empty(document.Catalog.PageTree.Pages);
        Assert.StartsWith("%PDF-1.7\n", Encoding.ASCII.GetString(bytes));
        Assert.EndsWith("\n%%EOF\n", Encoding.ASCII.GetString(bytes));

        foreach (var location in document.CrossReferences.Entries.Where(entry => entry.ObjectNumber > 0))
        {
            var expectedHeader = Encoding.ASCII.GetBytes(
                $"{location.ObjectNumber} {location.GenerationNumber} obj");
            Assert.True(bytes.AsSpan((int)location.ByteOffset!.Value).StartsWith(expectedHeader));
        }
    }

    [Fact]
    public void WritesAPathAndValidatesBeforeTruncatingAnExistingFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            PdfDocumentWriter.Write(path, CreateEmptyDocument());
            using (var document = PdfDocumentReader.Open(path))
            {
                Assert.Equal(2, document.Catalog.ObjectId.ObjectNumber);
            }

            var original = File.ReadAllBytes(path);
            Assert.Throws<PdfWriteException>(() =>
                PdfDocumentWriter.Write(path, new PdfDocumentBuilder()));
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void WritesToANonSeekableDestination()
    {
        using var destination = new NonSeekableWriteStream();

        PdfDocumentWriter.Write(destination, CreateEmptyDocument(), leaveOpen: true);

        using var document = PdfDocumentReader.Open(destination.ToArray().AsMemory());
        Assert.Equal(2, document.Catalog.ObjectId.ObjectNumber);
    }

    [Fact]
    public void UsesAbsoluteOffsetsWhenWritingAtTheCurrentStreamPosition()
    {
        using var destination = new MemoryStream();
        destination.Write("prefix\n"u8);

        PdfDocumentWriter.Write(destination, CreateEmptyDocument(), leaveOpen: true);
        destination.Position = 0;
        using var document = PdfDocumentReader.Open(destination, leaveOpen: true);

        Assert.Equal(2, document.Catalog.ObjectId.ObjectNumber);
        Assert.All(
            document.CrossReferences.Entries.Where(entry => entry.ObjectNumber > 0),
            entry => Assert.True(entry.ByteOffset >= "prefix\n"u8.Length));
    }

    [Fact]
    public void RejectsMissingAndDanglingReferences()
    {
        var missingRoot = new PdfDocumentBuilder();
        missingRoot.AddObject(new PdfDictionary());
        Assert.Throws<PdfWriteException>(() =>
            PdfDocumentWriter.Write(new MemoryStream(), missingRoot));

        var dangling = new PdfDocumentBuilder();
        var catalog = dangling.AddObject(new PdfDictionary
        {
            ["Type"] = new PdfName("Catalog"),
            ["Pages"] = new PdfIndirectReference(99),
        });
        dangling.Root = catalog.CreateReference();
        Assert.Throws<PdfWriteException>(() =>
            PdfDocumentWriter.Write(new MemoryStream(), dangling));
    }

    [Fact]
    public void DisposesTheDestinationUnlessLeaveOpenIsRequested()
    {
        var owned = new MemoryStream();
        PdfDocumentWriter.Write(owned, CreateEmptyDocument());
        Assert.False(owned.CanWrite);

        var callerOwned = new MemoryStream();
        PdfDocumentWriter.Write(callerOwned, CreateEmptyDocument(), leaveOpen: true);
        Assert.True(callerOwned.CanWrite);
        callerOwned.Dispose();
    }

    [Theory]
    [InlineData("1.8")]
    [InlineData("2.1")]
    [InlineData("PDF-1.7")]
    public void RejectsUnsupportedHeaderVersions(string version)
    {
        var options = new PdfWriteOptions { Version = version };

        Assert.Throws<ArgumentException>(() =>
            PdfDocumentWriter.Write(new MemoryStream(), CreateEmptyDocument(), options));
    }

    private static PdfDocumentBuilder CreateEmptyDocument()
    {
        var builder = new PdfDocumentBuilder();
        var pages = builder.AddObject(new PdfDictionary
        {
            ["Type"] = new PdfName("Pages"),
            ["Kids"] = new PdfArray(),
            ["Count"] = new PdfInteger(0),
        });
        var catalog = builder.AddObject(new PdfDictionary
        {
            ["Type"] = new PdfName("Catalog"),
            ["Pages"] = pages.CreateReference(),
        });
        builder.Root = catalog.CreateReference();
        return builder;
    }

    private sealed class NonSeekableWriteStream : Stream
    {
        private readonly MemoryStream _inner = new();

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public byte[] ToArray() => _inner.ToArray();

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);

        public override void WriteByte(byte value) => _inner.WriteByte(value);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
