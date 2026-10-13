namespace Rtpdf.Core.Documents;

/// <summary>
/// Validation and resource limits used while reading a catalog and page tree.
/// </summary>
public sealed class PdfCatalogReaderOptions
{
    public int MaximumPageTreeDepth { get; init; } = 100;

    public int MaximumPageTreeNodes { get; init; } = 1_000_000;

    public int MaximumPages { get; init; } = 1_000_000;

    public int MaximumKidsPerNode { get; init; } = 100_000;

    public bool ValidatePageCounts { get; init; } = true;

    public bool ValidateParentReferences { get; init; } = true;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaximumPageTreeDepth, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaximumPageTreeNodes, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaximumPages, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumKidsPerNode);
    }
}
