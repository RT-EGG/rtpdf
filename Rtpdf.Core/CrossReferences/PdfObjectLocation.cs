namespace Rtpdf.Core.CrossReferences;

/// <summary>
/// A conventional cross-reference entry for one PDF object number.
/// </summary>
public sealed class PdfObjectLocation
{
    internal PdfObjectLocation(
        int objectNumber,
        long fieldValue,
        ushort generationNumber,
        PdfObjectLocationKind kind)
    {
        ObjectNumber = objectNumber;
        FieldValue = fieldValue;
        GenerationNumber = generationNumber;
        Kind = kind;
    }

    public int ObjectNumber { get; }

    /// <summary>
    /// Gets the byte offset for an in-use entry or the next free object number for a free entry.
    /// </summary>
    public long FieldValue { get; }

    public ushort GenerationNumber { get; }

    public PdfObjectLocationKind Kind { get; }

    public bool IsInUse => Kind == PdfObjectLocationKind.InUse;

    public long? ByteOffset => IsInUse ? FieldValue : null;

    public long? NextFreeObjectNumber => IsInUse ? null : FieldValue;

    public override string ToString() => IsInUse
        ? $"{ObjectNumber} {GenerationNumber} at {FieldValue}"
        : $"{ObjectNumber} {GenerationNumber} free -> {FieldValue}";
}
