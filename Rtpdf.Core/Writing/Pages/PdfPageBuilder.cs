using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Pages;

namespace Rtpdf.Core.Writing.Pages;

/// <summary>Builds one page leaf, its resources, and its content stream references.</summary>
public sealed class PdfPageBuilder
{
    private readonly PdfDocumentBuilder _document;
    private readonly List<PdfIndirectReference> _contents = [];
    private PdfRectangle _mediaBox;
    private PdfRectangle? _cropBox;
    private int _rotation;

    internal PdfPageBuilder(
        PdfDocumentBuilder document,
        PdfIndirectReference parent,
        PdfRectangle mediaBox,
        int index)
    {
        _document = document;
        ValidateRectangle(mediaBox, nameof(mediaBox));
        _mediaBox = mediaBox;

        Resources = new PdfDictionary();
        Dictionary = new PdfDictionary
        {
            ["Type"] = new PdfName("Page"),
            ["Parent"] = parent,
            ["MediaBox"] = CreateRectangleArray(mediaBox),
            ["Resources"] = Resources,
        };
        Object = document.AddObject(Dictionary);
        Index = index;
        Contents = _contents.AsReadOnly();
    }

    public int Index { get; }

    public PdfIndirectObject Object { get; }

    public PdfIndirectReference Reference => Object.CreateReference();

    /// <summary>Gets the mutable low-level page dictionary.</summary>
    public PdfDictionary Dictionary { get; }

    /// <summary>Gets the mutable page resource dictionary.</summary>
    public PdfDictionary Resources { get; }

    public IReadOnlyList<PdfIndirectReference> Contents { get; }

    public PdfRectangle MediaBox
    {
        get => _mediaBox;
        set
        {
            ValidateRectangle(value, nameof(value));
            _mediaBox = value;
            Dictionary["MediaBox"] = CreateRectangleArray(value);
        }
    }

    public PdfRectangle? CropBox
    {
        get => _cropBox;
        set
        {
            if (value is null)
            {
                _cropBox = null;
                Dictionary.Remove("CropBox");
                return;
            }

            ValidateRectangle(value.Value, nameof(value));
            _cropBox = value;
            Dictionary["CropBox"] = CreateRectangleArray(value.Value);
        }
    }

    /// <summary>Gets or sets clockwise rotation normalized to 0, 90, 180, or 270 degrees.</summary>
    public int Rotation
    {
        get => _rotation;
        set
        {
            if (value % 90 != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Page rotation must be a multiple of 90 degrees");
            }

            _rotation = ((value % 360) + 360) % 360;
            if (_rotation == 0)
            {
                Dictionary.Remove("Rotate");
            }
            else
            {
                Dictionary["Rotate"] = new PdfInteger(_rotation);
            }
        }
    }

    /// <summary>Registers and appends an encoded page content stream.</summary>
    public PdfIndirectReference AddContent(PdfStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var reference = _document.AddObject(stream).CreateReference();
        AddContent(reference);
        return reference;
    }

    /// <summary>Appends a content stream already registered in this document.</summary>
    public void AddContent(PdfIndirectReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (!_document.TryGetObject(reference, out var indirectObject) ||
            indirectObject.Value is not PdfStream)
        {
            throw new PdfWriteException(
                $"Page content reference {reference.ObjectId} R is not a registered PDF stream");
        }

        _contents.Add(reference);
        Dictionary["Contents"] = _contents.Count == 1
            ? reference
            : new PdfArray(_contents);
    }

    /// <summary>Adds or replaces one named entry in a page resource category.</summary>
    public void SetResource(string category, string name, PdfObject value) =>
        SetResource(new PdfName(category), new PdfName(name), value);

    /// <summary>Adds or replaces one named entry in a page resource category.</summary>
    public void SetResource(PdfName category, PdfName name, PdfObject value)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        if (value is PdfStream)
        {
            throw new PdfWriteException(
                "A resource stream must be registered as an indirect object before it is added to a page");
        }

        if (value is PdfIndirectReference reference &&
            !_document.TryGetObject(reference, out _))
        {
            throw new PdfWriteException(
                $"Resource reference {reference.ObjectId} R is not registered in this document");
        }

        GetOrCreateResourceCategory(category)[name] = value;
    }

    /// <summary>Registers a stream and adds it to this page's XObject resources.</summary>
    public PdfIndirectReference AddXObject(string name, PdfStream xObject) =>
        AddXObject(new PdfName(name), xObject);

    /// <summary>Registers a stream and adds it to this page's XObject resources.</summary>
    public PdfIndirectReference AddXObject(PdfName name, PdfStream xObject)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(xObject);
        var category = GetOrCreateResourceCategory(new PdfName("XObject"));
        var reference = _document.AddObject(xObject).CreateReference();
        category[name] = reference;
        return reference;
    }

    private static PdfArray CreateRectangleArray(PdfRectangle rectangle) =>
        new(
        [
            CreateNumber(rectangle.Left),
            CreateNumber(rectangle.Bottom),
            CreateNumber(rectangle.Right),
            CreateNumber(rectangle.Top),
        ]);

    private static PdfNumber CreateNumber(decimal value) =>
        decimal.Truncate(value) == value && value is >= long.MinValue and <= long.MaxValue
            ? new PdfInteger(decimal.ToInt64(value))
            : new PdfReal(value);

    private PdfDictionary GetOrCreateResourceCategory(PdfName category)
    {
        if (!Resources.TryGetValue(category, out var categoryValue))
        {
            var newCategory = new PdfDictionary();
            Resources[category] = newCategory;
            return newCategory;
        }

        return categoryValue as PdfDictionary ?? throw new PdfWriteException(
            $"Page resource category {category} is not a dictionary");
    }

    private static void ValidateRectangle(PdfRectangle value, string parameterName)
    {
        if (value.Width <= 0 || value.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "A page rectangle must have positive width and height");
        }
    }
}
