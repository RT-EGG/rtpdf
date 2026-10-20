using Rtpdf.Core.Parsing;
using Rtpdf.Core.Storage;

namespace Rtpdf.Core.Documents;

/// <summary>
/// Configures source and parser limits used by <see cref="PdfDocumentReader"/>.
/// </summary>
public sealed class PdfDocumentReaderOptions
{
    /// <summary>Gets the password used for an encrypted document. Null attempts the empty password.</summary>
    public string? Password { get; init; }

    /// <summary>
    /// Gets the maximum accepted source length. Non-seekable streams are spooled to a temporary file up to this limit.
    /// </summary>
    public long MaximumSourceLength { get; init; } = 256L * 1024 * 1024;

    public PdfCrossReferenceParserOptions CrossReferenceParserOptions { get; init; } = new();

    public PdfObjectStoreOptions ObjectStoreOptions { get; init; } = new();

    public PdfCatalogReaderOptions CatalogReaderOptions { get; init; } = new();

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumSourceLength);
        ArgumentNullException.ThrowIfNull(CrossReferenceParserOptions);
        ArgumentNullException.ThrowIfNull(ObjectStoreOptions);
        ArgumentNullException.ThrowIfNull(CatalogReaderOptions);
        CrossReferenceParserOptions.Validate();
        ObjectStoreOptions.Validate();
        CatalogReaderOptions.Validate();
    }
}
