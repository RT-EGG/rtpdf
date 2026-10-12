namespace Rtpdf.Core.Objects;

/// <summary>
/// The object and generation numbers that identify an indirect PDF object.
/// </summary>
public readonly record struct PdfObjectId
{
    public PdfObjectId(int objectNumber, int generationNumber = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(objectNumber, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(generationNumber);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(generationNumber, ushort.MaxValue);

        ObjectNumber = objectNumber;
        GenerationNumber = (ushort)generationNumber;
    }

    public int ObjectNumber { get; }

    public ushort GenerationNumber { get; }

    public override string ToString() => $"{ObjectNumber} {GenerationNumber}";
}
