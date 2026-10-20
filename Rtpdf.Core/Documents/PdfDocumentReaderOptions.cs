using Rtpdf.Core.Parsing;
using Rtpdf.Core.Storage;

namespace Rtpdf.Core.Documents;

/// <summary>
/// Configures source and parser limits used by <see cref="PdfDocumentReader"/>.
/// </summary>
public sealed class PdfDocumentReaderOptions
{
    /// <summary>
    /// Gets the maximum number of source bytes materialized while opening a document.
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
