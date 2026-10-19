namespace Rtpdf.Core.Parsing;

/// <summary>
/// Resource limits applied while parsing direct PDF objects.
/// </summary>
public sealed class PdfObjectParserOptions
{
    public int MaximumNestingDepth { get; init; } = 100;

    public int MaximumArrayItems { get; init; } = 1_000_000;

    public int MaximumDictionaryEntries { get; init; } = 100_000;

    public int MaximumEncodedStreamLength { get; init; } = 256 * 1024 * 1024;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumNestingDepth);
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumArrayItems);
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumDictionaryEntries);
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumEncodedStreamLength);
    }
}
