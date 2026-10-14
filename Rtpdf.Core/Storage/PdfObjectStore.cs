using Rtpdf.Core.CrossReferences;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Parsing;
using Rtpdf.Core.Tokens;

namespace Rtpdf.Core.Storage;

/// <summary>
/// Loads indirect objects through a cross-reference table and caches parsed values.
/// </summary>
public sealed class PdfObjectStore
{
    private readonly byte[] _input;
    private readonly PdfCrossReferenceTable _crossReferences;
    private readonly PdfObjectStoreOptions _options;
    private readonly PdfIndirectObjectParser _parser;
    private readonly Dictionary<PdfObjectId, PdfIndirectObject> _cache = [];
    private readonly HashSet<PdfObjectId> _loading = [];
    private readonly object _syncRoot = new();

    public PdfObjectStore(
        ReadOnlyMemory<byte> input,
        PdfCrossReferenceTable crossReferences,
        PdfObjectStoreOptions? options = null)
    {
        _input = input.ToArray();
        _crossReferences = crossReferences ?? throw new ArgumentNullException(nameof(crossReferences));
        _options = options ?? new PdfObjectStoreOptions();
        _options.Validate();
        _parser = new PdfIndirectObjectParser(_options.ObjectParserOptions);
    }

    public PdfObjectStore(
        Stream input,
        PdfCrossReferenceTable crossReferences,
        PdfObjectStoreOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        _input = buffer.ToArray();
        _crossReferences = crossReferences ?? throw new ArgumentNullException(nameof(crossReferences));
        _options = options ?? new PdfObjectStoreOptions();
        _options.Validate();
        _parser = new PdfIndirectObjectParser(_options.ObjectParserOptions);
    }

    public int CachedObjectCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _cache.Count;
            }
        }
    }

    /// <summary>
    /// Loads exactly one indirect object without following a reference stored as its value.
    /// </summary>
    public PdfIndirectObject GetIndirectObject(PdfObjectId objectId)
    {
        lock (_syncRoot)
        {
            return GetIndirectObjectCore(objectId);
        }
    }

    /// <summary>
    /// Resolves an indirect reference. If an indirect object itself contains another reference,
    /// the chain is followed until a non-reference value is reached.
    /// </summary>
    public PdfObject Resolve(PdfIndirectReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        lock (_syncRoot)
        {
            var current = reference;
            var visited = new HashSet<PdfObjectId>();

            for (var depth = 0; depth < _options.MaximumReferenceDepth; depth++)
            {
                if (!visited.Add(current.ObjectId))
                {
                    throw new PdfObjectResolutionException(
                        "Cyclic indirect reference detected",
                        current.ObjectId);
                }

                var value = GetIndirectObjectCore(current.ObjectId).Value;
                if (value is not PdfIndirectReference next)
                {
                    return value;
                }

                current = next;
            }

            throw new PdfObjectResolutionException(
                $"Indirect reference chain exceeds the configured depth of {_options.MaximumReferenceDepth}",
                current.ObjectId);
        }
    }

    public TObject Resolve<TObject>(PdfIndirectReference reference)
        where TObject : PdfObject
    {
        var value = Resolve(reference);
        return value as TObject
            ?? throw new PdfObjectResolutionException(
                $"Resolved object is {value.GetType().Name}, not {typeof(TObject).Name}",
                reference.ObjectId);
    }

    public bool TryGetCachedObject(PdfObjectId objectId, out PdfIndirectObject value)
    {
        lock (_syncRoot)
        {
            return _cache.TryGetValue(objectId, out value!);
        }
    }

    private PdfIndirectObject GetIndirectObjectCore(PdfObjectId objectId)
    {
        if (_cache.TryGetValue(objectId, out var cached))
        {
            return cached;
        }

        if (!_crossReferences.TryGetLocation(objectId.ObjectNumber, out var location))
        {
            throw new PdfObjectResolutionException(
                "Object number is not present in the cross-reference table",
                objectId);
        }

        if (!location.IsInUse || location.ByteOffset is not long byteOffset)
        {
            throw new PdfObjectResolutionException(
                "Cross-reference entry is free",
                objectId);
        }

        if (location.GenerationNumber != objectId.GenerationNumber)
        {
            throw new PdfObjectResolutionException(
                $"Reference generation {objectId.GenerationNumber} does not match xref generation {location.GenerationNumber}",
                objectId,
                byteOffset);
        }

        if (byteOffset < 0 || byteOffset >= _input.Length)
        {
            throw new PdfObjectResolutionException(
                "Cross-reference entry points outside the object source",
                objectId,
                byteOffset);
        }

        if (!_loading.Add(objectId))
        {
            throw new PdfObjectResolutionException(
                "Cyclic indirect-object dependency detected",
                objectId,
                byteOffset);
        }

        try
        {
            PdfIndirectObject parsed;
            try
            {
                parsed = _parser.Parse(_input, (int)byteOffset, ResolveStreamLength);
            }
            catch (Exception exception) when (exception is PdfParseException or PdfLexerException)
            {
                throw new PdfObjectResolutionException(
                    "Indirect object syntax is invalid",
                    objectId,
                    byteOffset,
                    exception);
            }
            catch (PdfObjectResolutionException exception)
            {
                throw new PdfObjectResolutionException(
                    "Indirect object dependency could not be resolved",
                    objectId,
                    byteOffset,
                    exception);
            }

            if (parsed.ObjectId != objectId)
            {
                throw new PdfObjectResolutionException(
                    $"Object declaration identifies {parsed.ObjectId} instead of the requested object",
                    objectId,
                    byteOffset);
            }

            if (_cache.Count >= _options.MaximumCachedObjects)
            {
                throw new PdfObjectResolutionException(
                    $"Object cache exceeds the configured limit of {_options.MaximumCachedObjects}",
                    objectId,
                    byteOffset);
            }

            _cache.Add(objectId, parsed);
            return parsed;
        }
        finally
        {
            _loading.Remove(objectId);
        }
    }

    private PdfObject ResolveStreamLength(PdfIndirectReference reference) => Resolve(reference);
}
