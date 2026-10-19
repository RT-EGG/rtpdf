namespace Rtpdf.Core.CrossReferences;

/// <summary>
/// A cross-reference entry for one PDF object number.
/// </summary>
public sealed class PdfObjectLocation
{
    internal PdfObjectLocation(
        int objectNumber,
        long fieldValue,
        ushort generationNumber,
        PdfObjectLocationKind kind,
        int? objectStreamIndex = null)
    {
        ObjectNumber = objectNumber;
        FieldValue = fieldValue;
        GenerationNumber = generationNumber;
        Kind = kind;
        ObjectStreamIndex = objectStreamIndex;
    }

    public int ObjectNumber { get; }

    /// <summary>
    /// Gets the byte offset, next free object number, or object stream number depending on
    /// <see cref="Kind"/>.
    /// </summary>
    public long FieldValue { get; }

    public ushort GenerationNumber { get; }

    public PdfObjectLocationKind Kind { get; }

    public bool IsInUse => Kind is PdfObjectLocationKind.InUse or PdfObjectLocationKind.Compressed;

    public long? ByteOffset => Kind == PdfObjectLocationKind.InUse ? FieldValue : null;

    public long? NextFreeObjectNumber => Kind == PdfObjectLocationKind.Free ? FieldValue : null;

    public int? ObjectStreamNumber => Kind == PdfObjectLocationKind.Compressed ? (int)FieldValue : null;

    public int? ObjectStreamIndex { get; }

    public override string ToString() => Kind switch
    {
        PdfObjectLocationKind.InUse => $"{ObjectNumber} {GenerationNumber} at {FieldValue}",
        PdfObjectLocationKind.Free => $"{ObjectNumber} {GenerationNumber} free -> {FieldValue}",
        PdfObjectLocationKind.Compressed =>
            $"{ObjectNumber} in object stream {ObjectStreamNumber} at index {ObjectStreamIndex}",
        _ => $"{ObjectNumber} unknown",
    };
}
