namespace Rtpdf.Core.Parsing;

/// <summary>
/// Resource limits and validation settings for conventional cross-reference parsing.
/// </summary>
public sealed class PdfCrossReferenceParserOptions
{
    public int MaximumEntries { get; init; } = 10_000_000;

    public int MaximumStartXrefSearchBytes { get; init; } = 1_048_576;

    public bool RequireEndOfFileMarker { get; init; } = true;

    public PdfObjectParserOptions ObjectParserOptions { get; init; } = new();

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumEntries);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaximumStartXrefSearchBytes, 0);
        ArgumentNullException.ThrowIfNull(ObjectParserOptions);
        ObjectParserOptions.Validate();
    }
}
