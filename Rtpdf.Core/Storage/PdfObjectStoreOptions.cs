using Rtpdf.Core.Parsing;

namespace Rtpdf.Core.Storage;

/// <summary>
/// Resource limits applied while loading and resolving indirect objects.
/// </summary>
public sealed class PdfObjectStoreOptions
{
    public int MaximumReferenceDepth { get; init; } = 100;

    public int MaximumCachedObjects { get; init; } = 100_000;

    public PdfObjectParserOptions ObjectParserOptions { get; init; } = new();

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaximumReferenceDepth, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaximumCachedObjects, 0);
        ArgumentNullException.ThrowIfNull(ObjectParserOptions);
        ObjectParserOptions.Validate();
    }
}
