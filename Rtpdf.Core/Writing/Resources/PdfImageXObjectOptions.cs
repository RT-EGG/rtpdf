using System.IO.Compression;

namespace Rtpdf.Core.Writing.Resources;

/// <summary>Resource limits and compression settings for generated image XObjects.</summary>
public sealed class PdfImageXObjectOptions
{
    public long MaximumPixels { get; init; } = 100_000_000;

    public int MaximumSourceLength { get; init; } = 256 * 1024 * 1024;

    public int MaximumEncodedLength { get; init; } = 256 * 1024 * 1024;

    public CompressionLevel CompressionLevel { get; init; } = CompressionLevel.Optimal;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumPixels);
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumSourceLength);
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumEncodedLength);
        if (!Enum.IsDefined(CompressionLevel))
        {
            throw new ArgumentOutOfRangeException(nameof(CompressionLevel));
        }
    }
}
