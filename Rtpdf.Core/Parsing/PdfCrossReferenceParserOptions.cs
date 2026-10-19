using Rtpdf.Core.Filters;

namespace Rtpdf.Core.Parsing;

/// <summary>
/// Resource limits and validation settings for cross-reference parsing.
/// </summary>
public sealed class PdfCrossReferenceParserOptions
{
    /// <summary>Gets the maximum number of effective entries and entries in each section.</summary>
    public int MaximumEntries { get; init; } = 10_000_000;

    /// <summary>Gets the maximum number of xref tables and streams followed through the file.</summary>
    public int MaximumCrossReferenceSections { get; init; } = 1_024;

    public int MaximumStartXrefSearchBytes { get; init; } = 1_048_576;

    public bool RequireEndOfFileMarker { get; init; } = true;

    public PdfObjectParserOptions ObjectParserOptions { get; init; } = new();

    public PdfStreamDecoderOptions StreamDecoderOptions { get; init; } = new();

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumEntries);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaximumCrossReferenceSections, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaximumStartXrefSearchBytes, 0);
        ArgumentNullException.ThrowIfNull(ObjectParserOptions);
        ArgumentNullException.ThrowIfNull(StreamDecoderOptions);
        ObjectParserOptions.Validate();
        StreamDecoderOptions.Validate();
    }
}
