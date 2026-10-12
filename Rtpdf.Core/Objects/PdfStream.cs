namespace Rtpdf.Core.Objects;

/// <summary>
/// A PDF stream and its stream dictionary. Data is kept in its encoded form;
/// decoding belongs to the filter layer.
/// </summary>
public sealed class PdfStream : PdfObject
{
    private byte[] _encodedData;

    public PdfStream(PdfDictionary dictionary, ReadOnlySpan<byte> encodedData)
    {
        Dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
        _encodedData = encodedData.ToArray();
    }

    public PdfDictionary Dictionary { get; }

    public ReadOnlyMemory<byte> EncodedData => _encodedData;

    public int EncodedLength => _encodedData.Length;

    public override PdfObjectType Type => PdfObjectType.Stream;

    public void ReplaceEncodedData(ReadOnlySpan<byte> encodedData) => _encodedData = encodedData.ToArray();

    public byte[] GetEncodedDataCopy() => (byte[])_encodedData.Clone();
}
