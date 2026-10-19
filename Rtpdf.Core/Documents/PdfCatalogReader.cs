using Rtpdf.Core.CrossReferences;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Pages;
using Rtpdf.Core.Storage;

namespace Rtpdf.Core.Documents;

/// <summary>
/// Reads and validates the document catalog and conventional page-tree hierarchy.
/// </summary>
public sealed class PdfCatalogReader
{
    private readonly PdfCrossReferenceTable _crossReferences;
    private readonly PdfObjectStore _objectStore;
    private readonly PdfCatalogReaderOptions _options;

    public PdfCatalogReader(
        PdfCrossReferenceTable crossReferences,
        PdfObjectStore objectStore,
        PdfCatalogReaderOptions? options = null)
    {
        _crossReferences = crossReferences ?? throw new ArgumentNullException(nameof(crossReferences));
        _objectStore = objectStore ?? throw new ArgumentNullException(nameof(objectStore));
        _options = options ?? new PdfCatalogReaderOptions();
        _options.Validate();
    }

    public PdfCatalog Read()
    {
        var rootReference = GetRequiredReference(_crossReferences.Trailer, "Root", null);
        var catalog = ResolveDictionary(rootReference, "Trailer /Root");
        RequireName(catalog, "Type", "Catalog", rootReference.ObjectId);

        var pagesReference = GetRequiredReference(catalog, "Pages", rootReference.ObjectId);
        var context = new TraversalContext();
        var result = ReadItem(
            pagesReference,
            expectedParent: null,
            new InheritedAttributes(),
            depth: 0,
            context);

        if (result.Item is not PdfPageTreeNode root)
        {
            throw Error("Catalog /Pages must reference a /Pages node", pagesReference.ObjectId);
        }

        return new PdfCatalog(
            rootReference.ObjectId,
            catalog,
            new PdfPageTree(root, context.Pages.ToArray()));
    }

    private ReadResult ReadItem(
        PdfIndirectReference reference,
        PdfObjectId? expectedParent,
        InheritedAttributes inherited,
        int depth,
        TraversalContext context)
    {
        if (depth >= _options.MaximumPageTreeDepth)
        {
            throw Error(
                $"Page tree exceeds the configured depth of {_options.MaximumPageTreeDepth}",
                reference.ObjectId);
        }

        if (context.Visited.Count >= _options.MaximumPageTreeNodes)
        {
            throw Error(
                $"Page tree exceeds the configured node limit of {_options.MaximumPageTreeNodes}",
                reference.ObjectId);
        }

        if (context.Active.Contains(reference.ObjectId))
        {
            throw Error("Cyclic page-tree reference detected", reference.ObjectId);
        }

        if (!context.Visited.Add(reference.ObjectId))
        {
            throw Error("Page-tree object appears more than once", reference.ObjectId);
        }

        context.Active.Add(reference.ObjectId);
        try
        {
            var dictionary = ResolveDictionary(reference, "Page-tree entry");
            ValidateParent(dictionary, reference.ObjectId, expectedParent);

            var type = GetRequiredName(dictionary, "Type", reference.ObjectId);
            return type.Value switch
            {
                "Pages" => ReadPagesNode(reference, dictionary, inherited, depth, context),
                "Page" => ReadPage(reference, dictionary, inherited, context),
                _ => throw Error(
                    $"Page-tree /Type must be /Pages or /Page, not {type}",
                    reference.ObjectId),
            };
        }
        finally
        {
            context.Active.Remove(reference.ObjectId);
        }
    }

    private ReadResult ReadPagesNode(
        PdfIndirectReference reference,
        PdfDictionary dictionary,
        InheritedAttributes inherited,
        int depth,
        TraversalContext context)
    {
        var declaredCount = GetRequiredNonNegativeInteger(dictionary, "Count", reference.ObjectId);
        var kids = GetRequiredValue<PdfArray>(dictionary, "Kids", reference.ObjectId);
        if (kids.Count > _options.MaximumKidsPerNode)
        {
            throw Error(
                $"Page-tree node exceeds the configured child limit of {_options.MaximumKidsPerNode}",
                reference.ObjectId);
        }

        var effectiveAttributes = inherited.Apply(dictionary);
        var childItems = new List<PdfPageTreeItem>(kids.Count);
        var actualCount = 0;

        foreach (var kid in kids)
        {
            if (kid is not PdfIndirectReference kidReference)
            {
                throw Error("Every /Kids entry must be an indirect reference", reference.ObjectId);
            }

            var child = ReadItem(
                kidReference,
                reference.ObjectId,
                effectiveAttributes,
                depth + 1,
                context);
            childItems.Add(child.Item);
            actualCount = checked(actualCount + child.PageCount);
        }

        if (_options.ValidatePageCounts && declaredCount != actualCount)
        {
            throw Error(
                $"Page-tree /Count is {declaredCount}, but the subtree contains {actualCount} pages",
                reference.ObjectId);
        }

        return new ReadResult(
            new PdfPageTreeNode(reference.ObjectId, dictionary, declaredCount, childItems.ToArray()),
            actualCount);
    }

    private ReadResult ReadPage(
        PdfIndirectReference reference,
        PdfDictionary dictionary,
        InheritedAttributes inherited,
        TraversalContext context)
    {
        if (context.Pages.Count >= _options.MaximumPages)
        {
            throw Error(
                $"Page tree exceeds the configured page limit of {_options.MaximumPages}",
                reference.ObjectId);
        }

        var attributes = inherited.Apply(dictionary);
        var mediaBox = ReadRectangle(attributes.MediaBox, "MediaBox", reference.ObjectId, required: true)
            ?? throw Error("Page /MediaBox is required", reference.ObjectId);
        var cropBox = ReadRectangle(attributes.CropBox, "CropBox", reference.ObjectId, required: false)
            ?? mediaBox;
        var resources = attributes.Resources is null
            ? null
            : ResolveValue(attributes.Resources, reference.ObjectId, "Resources") as PdfDictionary
                ?? throw Error("Page /Resources must resolve to a dictionary", reference.ObjectId);
        var rotation = ReadRotation(attributes.Rotate, reference.ObjectId);

        var page = new PdfPage(
            reference.ObjectId,
            dictionary,
            context.Pages.Count,
            mediaBox,
            cropBox,
            resources,
            rotation);
        context.Pages.Add(page);
        return new ReadResult(page, 1);
    }

    private void ValidateParent(
        PdfDictionary dictionary,
        PdfObjectId objectId,
        PdfObjectId? expectedParent)
    {
        if (!_options.ValidateParentReferences)
        {
            return;
        }

        if (expectedParent is null)
        {
            if (dictionary.ContainsKey("Parent"))
            {
                throw Error("The root /Pages node must not have a /Parent entry", objectId);
            }

            return;
        }

        if (!dictionary.TryGetValue("Parent", out var parent) ||
            parent is not PdfIndirectReference parentReference ||
            parentReference.ObjectId != expectedParent.Value)
        {
            throw Error($"Page-tree /Parent must reference {expectedParent.Value}", objectId);
        }
    }

    private PdfRectangle? ReadRectangle(
        PdfObject? value,
        string entryName,
        PdfObjectId objectId,
        bool required)
    {
        if (value is null)
        {
            if (required)
            {
                throw Error($"Page /{entryName} is required", objectId);
            }

            return null;
        }

        if (ResolveValue(value, objectId, entryName) is not PdfArray array || array.Count != 4)
        {
            throw Error($"Page /{entryName} must be an array of four numbers", objectId);
        }

        var coordinates = new decimal[4];
        for (var index = 0; index < coordinates.Length; index++)
        {
            if (ResolveValue(array[index], objectId, entryName) is not PdfNumber number)
            {
                throw Error($"Page /{entryName} must contain only numbers", objectId);
            }

            coordinates[index] = number.DecimalValue;
        }

        var rectangle = new PdfRectangle(
            coordinates[0],
            coordinates[1],
            coordinates[2],
            coordinates[3]);
        if (rectangle.Right < rectangle.Left || rectangle.Top < rectangle.Bottom)
        {
            throw Error($"Page /{entryName} has inverted bounds", objectId);
        }

        return rectangle;
    }

    private int ReadRotation(PdfObject? value, PdfObjectId objectId)
    {
        if (value is null)
        {
            return 0;
        }

        if (ResolveValue(value, objectId, "Rotate") is not PdfInteger integer || integer.Value % 90 != 0)
        {
            throw Error("Page /Rotate must be an integer multiple of 90", objectId);
        }

        return (int)((integer.Value % 360 + 360) % 360);
    }

    private PdfDictionary ResolveDictionary(PdfIndirectReference reference, string entryName)
    {
        PdfObject value;
        try
        {
            value = _objectStore.Resolve(reference);
        }
        catch (PdfObjectResolutionException exception)
        {
            throw Error($"{entryName} could not be resolved", reference.ObjectId, exception);
        }

        return value as PdfDictionary
            ?? throw Error($"{entryName} must resolve to a dictionary", reference.ObjectId);
    }

    private PdfObject ResolveValue(PdfObject value, PdfObjectId objectId, string entryName)
    {
        if (value is not PdfIndirectReference reference)
        {
            return value;
        }

        try
        {
            return _objectStore.Resolve(reference);
        }
        catch (PdfObjectResolutionException exception)
        {
            throw Error($"/{entryName} could not be resolved", objectId, exception);
        }
    }

    private PdfName GetRequiredName(PdfDictionary dictionary, string key, PdfObjectId objectId)
    {
        if (!dictionary.TryGetValue(key, out var raw) ||
            ResolveValue(raw, objectId, key) is not PdfName name)
        {
            throw Error($"/{key} must resolve to a name", objectId);
        }

        return name;
    }

    private void RequireName(
        PdfDictionary dictionary,
        string key,
        string expected,
        PdfObjectId objectId)
    {
        var name = GetRequiredName(dictionary, key, objectId);
        if (name.Value != expected)
        {
            throw Error($"/{key} must be /{expected}, not {name}", objectId);
        }
    }

    private int GetRequiredNonNegativeInteger(
        PdfDictionary dictionary,
        string key,
        PdfObjectId objectId)
    {
        if (!dictionary.TryGetValue(key, out var raw) ||
            ResolveValue(raw, objectId, key) is not PdfInteger integer ||
            integer.Value < 0 ||
            integer.Value > int.MaxValue)
        {
            throw Error($"/{key} must be a non-negative 32-bit integer", objectId);
        }

        return (int)integer.Value;
    }

    private TObject GetRequiredValue<TObject>(
        PdfDictionary dictionary,
        string key,
        PdfObjectId objectId)
        where TObject : PdfObject
    {
        if (!dictionary.TryGetValue(key, out var raw) ||
            ResolveValue(raw, objectId, key) is not TObject value)
        {
            throw Error($"/{key} must resolve to {typeof(TObject).Name}", objectId);
        }

        return value;
    }

    private static PdfIndirectReference GetRequiredReference(
        PdfDictionary dictionary,
        string key,
        PdfObjectId? objectId)
    {
        if (!dictionary.TryGetValue(key, out var value) || value is not PdfIndirectReference reference)
        {
            throw Error($"/{key} must be an indirect reference", objectId);
        }

        return reference;
    }

    private static PdfDocumentStructureException Error(
        string message,
        PdfObjectId? objectId,
        Exception? innerException = null) =>
        new(message, objectId, innerException);

    private readonly record struct ReadResult(PdfPageTreeItem Item, int PageCount);

    private readonly record struct InheritedAttributes(
        PdfObject? MediaBox = null,
        PdfObject? CropBox = null,
        PdfObject? Resources = null,
        PdfObject? Rotate = null)
    {
        public InheritedAttributes Apply(PdfDictionary dictionary) => new(
            Get(dictionary, "MediaBox", MediaBox),
            Get(dictionary, "CropBox", CropBox),
            Get(dictionary, "Resources", Resources),
            Get(dictionary, "Rotate", Rotate));

        private static PdfObject? Get(
            PdfDictionary dictionary,
            string key,
            PdfObject? inherited) =>
            dictionary.TryGetValue(key, out var value) ? value : inherited;
    }

    private sealed class TraversalContext
    {
        public HashSet<PdfObjectId> Active { get; } = [];

        public HashSet<PdfObjectId> Visited { get; } = [];

        public List<PdfPage> Pages { get; } = [];
    }
}
