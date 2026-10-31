namespace Rtpdf.Core.Writing.Content;

/// <summary>Resource limits applied while constructing a content stream.</summary>
public sealed class PdfContentStreamBuilderOptions
{
    public int MaximumOperations { get; init; } = 1_000_000;

    public int MaximumOperandsPerOperation { get; init; } = 100_000;

    public int MaximumOperatorLength { get; init; } = 127;

    public int MaximumContentLength { get; init; } = 256 * 1024 * 1024;

    public int MaximumObjectNestingDepth { get; init; } = 100;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumOperations);
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumOperandsPerOperation);
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumOperatorLength);
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumContentLength);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaximumObjectNestingDepth, 0);
    }
}
