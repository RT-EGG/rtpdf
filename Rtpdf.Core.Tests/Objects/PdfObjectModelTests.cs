using Rtpdf.Core.Objects;
using Xunit;

namespace Rtpdf.Core.Tests.Objects;

public sealed class PdfObjectModelTests
{
    [Fact]
    public void PrimitiveObjectsKeepTheirDistinctPdfTypes()
    {
        Assert.Equal(PdfObjectType.Null, PdfNull.Instance.Type);
        Assert.Equal(PdfObjectType.Boolean, PdfBoolean.True.Type);
        Assert.Equal(PdfObjectType.Integer, new PdfInteger(1).Type);
        Assert.Equal(PdfObjectType.Real, new PdfReal(1).Type);
        Assert.NotEqual<PdfObject>(new PdfInteger(1), new PdfReal(1));
    }

    [Fact]
    public void PdfNameCopiesBytesAndUsesByteValueEquality()
    {
        byte[] source = [0x41, 0x20, 0x2F, 0xFF];
        var name = new PdfName(source);
        source[0] = 0;

        Assert.Equal(new byte[] { 0x41, 0x20, 0x2F, 0xFF }, name.Bytes.ToArray());
        Assert.Equal(new PdfName(new byte[] { 0x41, 0x20, 0x2F, 0xFF }), name);
        Assert.Equal("/A#20#2F#FF", name.ToString());
    }

    [Fact]
    public void PdfNameRejectsCharactersThatCannotBeRepresentedAsSingleBytes()
    {
        Assert.Throws<ArgumentException>(() => new PdfName("日本語"));
    }

    [Fact]
    public void PdfStringPreservesBytesAndRepresentationKind()
    {
        byte[] source = [0x41, 0x42];
        var literal = new PdfString(source, PdfStringKind.Literal);
        var hexadecimal = new PdfString(source, PdfStringKind.Hexadecimal);
        source[0] = 0;

        Assert.Equal("AB", literal.GetText());
        Assert.Equal("(AB)", literal.ToString());
        Assert.Equal("<4142>", hexadecimal.ToString());
        Assert.NotEqual(literal, hexadecimal);
    }

    [Fact]
    public void PdfStringDecodesBomMarkedBigEndianUtf16()
    {
        var value = new PdfString(new byte[] { 0xFE, 0xFF, 0x30, 0x42 });

        Assert.Equal("あ", value.GetText());
    }

    [Fact]
    public void PdfArrayIsMutableButRejectsClrNull()
    {
        var array = new PdfArray { new PdfInteger(1), PdfNull.Instance };
        array[0] = new PdfInteger(2);

        Assert.Equal(2, array.Count);
        Assert.Equal(new PdfInteger(2), array[0]);
        Assert.Throws<ArgumentNullException>(() => array.Add(null!));
    }

    [Fact]
    public void PdfDictionaryUsesPdfNameValueEqualityAndStringConvenienceAccess()
    {
        var dictionary = new PdfDictionary
        {
            [new PdfName("Type")] = new PdfName("Catalog"),
        };

        Assert.True(dictionary.TryGetValue("Type", out var value));
        Assert.Equal(new PdfName("Catalog"), value);
        Assert.Throws<ArgumentNullException>(() => dictionary["Missing"] = null!);
    }

    [Fact]
    public void PdfStreamCopiesEncodedDataWithoutRewritingLengthEntry()
    {
        var dictionary = new PdfDictionary { ["Length"] = new PdfInteger(99) };
        byte[] source = [1, 2, 3];
        var stream = new PdfStream(dictionary, source);
        source[0] = 9;

        Assert.Equal(new byte[] { 1, 2, 3 }, stream.EncodedData.ToArray());
        Assert.Equal(new PdfInteger(99), stream.Dictionary["Length"]);

        stream.ReplaceEncodedData([4, 5]);
        Assert.Equal(new byte[] { 4, 5 }, stream.GetEncodedDataCopy());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 65536)]
    public void PdfObjectIdRejectsInvalidNumbers(int objectNumber, int generationNumber)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfObjectId(objectNumber, generationNumber));
    }

    [Fact]
    public void IndirectObjectCreatesMatchingReferenceAndCanTrackReplacement()
    {
        var id = new PdfObjectId(12, 3);
        var indirectObject = new PdfIndirectObject(id, new PdfInteger(1));

        Assert.Equal(id, indirectObject.CreateReference().ObjectId);
        Assert.Equal("12 3 R", indirectObject.CreateReference().ToString());

        indirectObject.Value = PdfNull.Instance;
        Assert.Same(PdfNull.Instance, indirectObject.Value);
    }
}
