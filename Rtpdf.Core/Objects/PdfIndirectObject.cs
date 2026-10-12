namespace Rtpdf.Core.Objects;

/// <summary>
/// Associates an object value with its indirect object identity. This is a file-level
/// container rather than a value that can itself be nested in another PDF object.
/// </summary>
public sealed class PdfIndirectObject
{
    private PdfObject _value;

    public PdfIndirectObject(PdfObjectId objectId, PdfObject value)
    {
        if (objectId.ObjectNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(objectId));
        }

        ObjectId = objectId;
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public PdfObjectId ObjectId { get; }

    public PdfObject Value
    {
        get => _value;
        set => _value = value ?? throw new ArgumentNullException(nameof(value), "Use PdfNull.Instance for a PDF null value.");
    }

    public PdfIndirectReference CreateReference() => new(ObjectId);
}
