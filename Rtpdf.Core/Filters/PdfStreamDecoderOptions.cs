namespace Rtpdf.Core.Filters;

/// <summary>
/// Resource limits applied while decoding PDF streams.
/// </summary>
public sealed class PdfStreamDecoderOptions
{
    public int MaximumDecodedStreamLength { get; init; } = 256 * 1024 * 1024;

    public int MaximumFilterCount { get; init; } = 16;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumDecodedStreamLength);
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumFilterCount);
    }
}
