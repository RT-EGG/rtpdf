using Rtpdf.Core.Objects;

namespace Rtpdf.Core.CrossReferences;

/// <summary>
/// A conventional PDF cross-reference section and its trailer dictionary.
/// </summary>
public sealed class PdfCrossReferenceTable
{
    private readonly SortedDictionary<int, PdfObjectLocation> _entries;

    internal PdfCrossReferenceTable(
        long crossReferenceOffset,
        int startXrefKeywordOffset,
        int trailerOffset,
        int declaredSize,
        SortedDictionary<int, PdfObjectLocation> entries,
        PdfDictionary trailer)
    {
        CrossReferenceOffset = crossReferenceOffset;
        StartXrefKeywordOffset = startXrefKeywordOffset;
        TrailerOffset = trailerOffset;
        DeclaredSize = declaredSize;
        _entries = entries;
        Trailer = trailer;

        PreviousCrossReferenceOffset = trailer.TryGetValue("Prev", out var previous) &&
            previous is PdfInteger previousInteger
                ? previousInteger.Value
                : null;
    }

    public long CrossReferenceOffset { get; }

    /// <summary>
    /// Gets the offset of the <c>startxref</c> keyword, or -1 when parsed directly with
    /// <see cref="Rtpdf.Core.Parsing.PdfCrossReferenceParser.ParseAt(long)"/>.
    /// </summary>
    public int StartXrefKeywordOffset { get; }

    /// <summary>Gets the byte offset of the <c>trailer</c> keyword.</summary>
    public int TrailerOffset { get; }

    public int DeclaredSize { get; }

    public int Count => _entries.Count;

    public IReadOnlyCollection<PdfObjectLocation> Entries => _entries.Values;

    public PdfDictionary Trailer { get; }

    public long? PreviousCrossReferenceOffset { get; }

    public PdfObjectLocation this[int objectNumber] => _entries[objectNumber];

    public bool TryGetLocation(int objectNumber, out PdfObjectLocation location) =>
        _entries.TryGetValue(objectNumber, out location!);
}
