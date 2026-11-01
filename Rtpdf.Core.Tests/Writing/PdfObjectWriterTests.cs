using System.Text;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Writing;
using Xunit;

namespace Rtpdf.Core.Tests.Writing;

public sealed class PdfObjectWriterTests
{
    [Fact]
    public void WritesPrimitiveAndCompositeObjectsWithCanonicalEscaping()
    {
        var value = new PdfDictionary
        {
            [new PdfName([0x41, 0x20])] = new PdfString([0x28, 0x0A, 0xFF]),
            ["Array"] = new PdfArray
            {
                PdfNull.Instance,
                PdfBoolean.True,
                new PdfInteger(-12),
                new PdfReal(1),
                new PdfIndirectReference(7, 2),
            },
            ["Hex"] = new PdfString([0xAB, 0xCD], PdfStringKind.Hexadecimal),
        };

        using var destination = new MemoryStream();
        PdfObjectWriter.Write(destination, value);

        Assert.Equal(
            "<< /A#20 (\\(\\n\\377) /Array [null true -12 1.0 7 2 R] /Hex <ABCD> >>",
            Encoding.ASCII.GetString(destination.ToArray()));
    }

    [Fact]
    public void WritesAStreamWithItsActualEncodedLengthWithoutMutatingTheModel()
    {
        var dictionary = new PdfDictionary
        {
            ["Length"] = new PdfInteger(99),
            ["Filter"] = new PdfName("FlateDecode"),
        };
        var stream = new PdfStream(dictionary, [1, 2, 3]);
        var indirectObject = new PdfIndirectObject(new PdfObjectId(4), stream);

        using var destination = new MemoryStream();
        PdfObjectWriter.WriteIndirectObject(destination, indirectObject);

        var expectedPrefix = Encoding.ASCII.GetBytes(
            "4 0 obj\n<< /Filter /FlateDecode /Length 3 >>\nstream\n");
        var expectedSuffix = Encoding.ASCII.GetBytes("\nendstream\nendobj\n");
        Assert.Equal(
            [.. expectedPrefix, 1, 2, 3, .. expectedSuffix],
            destination.ToArray());
        Assert.Equal(new PdfInteger(99), dictionary["Length"]);
    }

    [Fact]
    public void RejectsDirectCyclesAndNestedStreams()
    {
        var cyclic = new PdfArray();
        cyclic.Add(cyclic);

        Assert.Throws<PdfWriteException>(() =>
            PdfObjectWriter.Write(new MemoryStream(), cyclic));

        var stream = new PdfStream(new PdfDictionary(), []);
        Assert.Throws<PdfWriteException>(() =>
            PdfObjectWriter.Write(new MemoryStream(), stream));

        var nestedStream = new PdfArray { stream };
        Assert.Throws<PdfWriteException>(() =>
            PdfObjectWriter.Write(new MemoryStream(), nestedStream));
    }

    [Fact]
    public void HonorsTheConfiguredNestingLimit()
    {
        var value = new PdfArray { new PdfArray { new PdfArray() } };
        var options = new PdfWriteOptions { MaximumObjectNestingDepth = 1 };

        Assert.Throws<PdfWriteException>(() =>
            PdfObjectWriter.Write(new MemoryStream(), value, options));
    }
}
