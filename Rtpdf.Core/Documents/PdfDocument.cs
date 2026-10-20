using Rtpdf.Core.CrossReferences;
using Rtpdf.Core.Filters;
using Rtpdf.Core.Storage;

namespace Rtpdf.Core.Documents;

/// <summary>
/// Represents the parsed top-level structures and object resolver for one PDF source.
/// </summary>
public sealed class PdfDocument : IDisposable
{
    private IDisposable? _ownedSource;

    internal PdfDocument(
        long sourceLength,
        PdfCrossReferenceTable crossReferences,
        PdfObjectStore objectStore,
        PdfCatalog catalog,
        IDisposable? ownedSource = null)
    {
        SourceLength = sourceLength;
        CrossReferences = crossReferences;
        ObjectStore = objectStore;
        Catalog = catalog;
        StreamDecoder = new PdfStreamDecoder(objectStore);
        _ownedSource = ownedSource;
    }

    public long SourceLength { get; }

    public PdfCrossReferenceTable CrossReferences { get; }

    public PdfObjectStore ObjectStore { get; }

    public PdfCatalog Catalog { get; }

    public PdfStreamDecoder StreamDecoder { get; }

    public void Dispose()
    {
        Interlocked.Exchange(ref _ownedSource, null)?.Dispose();
        GC.SuppressFinalize(this);
    }
}
