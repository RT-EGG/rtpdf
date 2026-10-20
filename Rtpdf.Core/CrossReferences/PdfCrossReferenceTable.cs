using Rtpdf.Core.Objects;

namespace Rtpdf.Core.CrossReferences;

/// <summary>
/// The effective cross-reference table obtained from one or more PDF revisions.
/// </summary>
public sealed class PdfCrossReferenceTable
{
    private readonly SortedDictionary<int, PdfObjectLocation> _entries;

    internal PdfCrossReferenceTable(
        IReadOnlyList<PdfCrossReferenceSection> sections,
        SortedDictionary<int, PdfObjectLocation> entries)
    {
        if (sections.Count == 0)
        {
            throw new ArgumentException("At least one cross-reference section is required.", nameof(sections));
        }

        Sections = Array.AsReadOnly(sections.ToArray());
        _entries = entries;
    }

    /// <summary>Gets physical sections in newest-to-oldest revision order.</summary>
    public IReadOnlyList<PdfCrossReferenceSection> Sections { get; }

    /// <summary>Gets the byte offset of the newest cross-reference section.</summary>
    public long CrossReferenceOffset => Sections[0].CrossReferenceOffset;

    /// <summary>
    /// Gets the offset of the newest <c>startxref</c> keyword, or -1 when parsed directly with
    /// <see cref="Rtpdf.Core.Parsing.PdfCrossReferenceParser.ParseAt(long)"/>.
    /// </summary>
    public int StartXrefKeywordOffset => Sections[0].StartXrefKeywordOffset;

    /// <summary>Gets the byte offset of the newest <c>trailer</c> keyword.</summary>
    public int TrailerOffset => Sections[0].TrailerOffset;

    /// <summary>Gets the <c>/Size</c> value from the newest trailer.</summary>
    public int DeclaredSize => Sections[0].DeclaredSize;

    /// <summary>Gets the number of effective entries after newer revisions override older ones.</summary>
    public int Count => _entries.Count;

    /// <summary>Gets effective entries in object-number order.</summary>
    public IReadOnlyCollection<PdfObjectLocation> Entries => _entries.Values;

    /// <summary>Gets the newest trailer dictionary.</summary>
    public PdfDictionary Trailer => Sections[0].Trailer;

    /// <summary>Gets the <c>/Prev</c> value from the newest trailer, if present.</summary>
    public long? PreviousCrossReferenceOffset => Sections[0].PreviousCrossReferenceOffset;

    public PdfObjectLocation this[int objectNumber] => _entries[objectNumber];

    public bool TryGetLocation(int objectNumber, out PdfObjectLocation location) =>
        _entries.TryGetValue(objectNumber, out location!);
}
