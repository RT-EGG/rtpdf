using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Writing;

/// <summary>
/// Collects the indirect objects and trailer references needed for a newly generated PDF.
/// </summary>
public sealed class PdfDocumentBuilder
{
    private readonly List<PdfIndirectObject> _objects = [];

    public PdfDocumentBuilder()
    {
        Objects = _objects.AsReadOnly();
    }

    public IReadOnlyList<PdfIndirectObject> Objects { get; }

    /// <summary>Gets or sets the required document catalog reference.</summary>
    public PdfIndirectReference? Root { get; set; }

    /// <summary>Gets or sets the optional document information dictionary reference.</summary>
    public PdfIndirectReference? Info { get; set; }

    /// <summary>Adds an indirect object using the next contiguous object number.</summary>
    public PdfIndirectObject AddObject(PdfObject value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new PdfIndirectObject(new PdfObjectId(_objects.Count + 1), value);
        _objects.Add(result);
        return result;
    }
}
