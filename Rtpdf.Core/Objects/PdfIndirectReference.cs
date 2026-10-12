namespace Rtpdf.Core.Objects;

/// <summary>
/// A reference to an indirect PDF object.
/// </summary>
public sealed class PdfIndirectReference : PdfObject, IEquatable<PdfIndirectReference>
{
    public PdfIndirectReference(PdfObjectId objectId)
    {
        if (objectId.ObjectNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(objectId));
        }

        ObjectId = objectId;
    }

    public PdfIndirectReference(int objectNumber, int generationNumber = 0)
        : this(new PdfObjectId(objectNumber, generationNumber))
    {
    }

    public PdfObjectId ObjectId { get; }

    public override PdfObjectType Type => PdfObjectType.IndirectReference;

    public bool Equals(PdfIndirectReference? other) => other is not null && ObjectId == other.ObjectId;

    public override bool Equals(object? obj) => obj is PdfIndirectReference other && Equals(other);

    public override int GetHashCode() => ObjectId.GetHashCode();

    public override string ToString() => $"{ObjectId} R";
}
