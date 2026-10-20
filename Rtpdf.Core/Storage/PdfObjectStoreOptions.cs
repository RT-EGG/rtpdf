using Rtpdf.Core.Filters;
using Rtpdf.Core.Parsing;

namespace Rtpdf.Core.Storage;

/// <summary>
/// Resource limits applied while loading and resolving indirect objects.
/// </summary>
public sealed class PdfObjectStoreOptions
{
    public int MaximumReferenceDepth { get; init; } = 100;

    public int MaximumCachedObjects { get; init; } = 100_000;

    /// <summary>Gets the maximum number of entries declared by one object stream.</summary>
    public int MaximumObjectsPerObjectStream { get; init; } = 1_000_000;

    /// <summary>Gets the maximum number of decoded object streams retained by this store.</summary>
    public int MaximumCachedObjectStreams { get; init; } = 1_024;

    /// <summary>Gets the maximum total decoded bytes retained for object streams.</summary>
    public long MaximumCachedObjectStreamBytes { get; init; } = 256L * 1024 * 1024;

    public PdfObjectParserOptions ObjectParserOptions { get; init; } = new();

    /// <summary>Gets the limits used while decoding object streams.</summary>
    public PdfStreamDecoderOptions StreamDecoderOptions { get; init; } = new();

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaximumReferenceDepth, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaximumCachedObjects, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumObjectsPerObjectStream);
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumCachedObjectStreams);
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumCachedObjectStreamBytes);
        ArgumentNullException.ThrowIfNull(ObjectParserOptions);
        ArgumentNullException.ThrowIfNull(StreamDecoderOptions);
        ObjectParserOptions.Validate();
        StreamDecoderOptions.Validate();
    }
}
