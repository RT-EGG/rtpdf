using Rtpdf.Core.CrossReferences;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Filters;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Parsing;
using Rtpdf.Core.Security;
using Rtpdf.Core.IO;
using Rtpdf.Core.Tokens;
using System.Diagnostics.CodeAnalysis;

namespace Rtpdf.Core.Storage;

/// <summary>
/// Loads indirect objects through a cross-reference table and caches parsed values.
/// </summary>
public sealed class PdfObjectStore
{
    private readonly byte[]? _input;
    private readonly PdfByteSource? _source;
    private readonly long[] _sourceBoundaries;
    private readonly PdfCrossReferenceTable _crossReferences;
    private readonly PdfObjectStoreOptions _options;
    private readonly PdfIndirectObjectParser _parser;
    private readonly Dictionary<PdfObjectId, PdfIndirectObject> _cache = [];
    private readonly Dictionary<int, PdfObjectStreamData> _objectStreamCache = [];
    private readonly HashSet<PdfObjectId> _loading = [];
    private long _cachedObjectStreamBytes;
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
        ValidateDecryptionContext();
        _sourceBoundaries = CreateSourceBoundaries(_input.LongLength);
        _parser = new PdfIndirectObjectParser(_options.ObjectParserOptions);
    }

    internal PdfObjectStore(
        byte[] input,
        PdfCrossReferenceTable crossReferences,
        PdfObjectStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(input);
        _input = input;
        _crossReferences = crossReferences ?? throw new ArgumentNullException(nameof(crossReferences));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        ValidateDecryptionContext();
        _sourceBoundaries = CreateSourceBoundaries(_input.LongLength);
        _parser = new PdfIndirectObjectParser(_options.ObjectParserOptions);
    }

    internal PdfObjectStore(
        PdfByteSource source,
        PdfCrossReferenceTable crossReferences,
        PdfObjectStoreOptions options)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _crossReferences = crossReferences ?? throw new ArgumentNullException(nameof(crossReferences));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        ValidateDecryptionContext();
        _sourceBoundaries = CreateSourceBoundaries(source.Length);
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
        ValidateDecryptionContext();
        _sourceBoundaries = CreateSourceBoundaries(_input.LongLength);
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

    public int CachedObjectStreamCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _objectStreamCache.Count;
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

    public bool TryGetIndirectObject(PdfObjectId objectId, [NotNullWhen(true)] out PdfIndirectObject? outObject)
    {
        try
        {
            outObject = GetIndirectObject(objectId);
            return true;
        }
        catch (PdfObjectResolutionException)
        {
            outObject = null;
            return false;
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

        if (location.Kind == PdfObjectLocationKind.Compressed)
        {
            return GetCompressedObjectCore(objectId, location);
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

        var sourceLength = _source?.Length ?? _input!.LongLength;
        if (byteOffset < 0 || byteOffset >= sourceLength)
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
                parsed = ParseIndirectObject(objectId, byteOffset);
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

            if (_options.DecryptionContext is { } context &&
                (context.EncryptionObjectId is null || objectId != context.EncryptionObjectId.Value))
            {
                try
                {
                    parsed.Value = context.DecryptObject(parsed.Value, objectId);
                }
                catch (PdfEncryptionException exception)
                {
                    throw new PdfObjectResolutionException(
                        "Indirect object could not be decrypted",
                        objectId,
                        byteOffset,
                        exception);
                }
            }

            AddToObjectCache(parsed, byteOffset);
            return parsed;
        }
        finally
        {
            _loading.Remove(objectId);
        }
    }

    private PdfIndirectObject GetCompressedObjectCore(
        PdfObjectId objectId,
        PdfObjectLocation location)
    {
        if (_crossReferences.Trailer.TryGetValue("Encrypt", out var encryption) &&
            encryption is PdfIndirectReference encryptionReference &&
            encryptionReference.ObjectId == objectId)
        {
            throw new PdfObjectResolutionException(
                "The encryption dictionary must not be stored in an object stream",
                objectId);
        }

        if (location.GenerationNumber != objectId.GenerationNumber)
        {
            throw new PdfObjectResolutionException(
                $"Reference generation {objectId.GenerationNumber} does not match compressed-object generation {location.GenerationNumber}",
                objectId);
        }

        if (location.ObjectStreamNumber is not int objectStreamNumber ||
            location.ObjectStreamIndex is not int objectStreamIndex)
        {
            throw new PdfObjectResolutionException(
                "Compressed cross-reference entry is incomplete",
                objectId);
        }

        if (!_loading.Add(objectId))
        {
            throw new PdfObjectResolutionException(
                "Cyclic object-stream dependency detected",
                objectId);
        }

        try
        {
            try
            {
                var objectStream = GetObjectStreamData(objectStreamNumber);
                var parsed = objectStream.ReadObject(
                    objectId,
                    objectStreamIndex,
                    _options.ObjectParserOptions);
                AddToObjectCache(parsed, offset: null);
                return parsed;
            }
            catch (PdfObjectResolutionException exception)
            {
                throw new PdfObjectResolutionException(
                    "Object stream dependency could not be resolved",
                    objectId,
                    innerException: exception);
            }
            catch (Exception exception) when (
                exception is InvalidDataException or PdfFilterException or PdfParseException or PdfLexerException)
            {
                throw new PdfObjectResolutionException(
                    "Object stream data is invalid",
                    objectId,
                    innerException: exception);
            }
        }
        finally
        {
            _loading.Remove(objectId);
        }
    }

    private PdfObjectStreamData GetObjectStreamData(int objectStreamNumber)
    {
        if (_objectStreamCache.TryGetValue(objectStreamNumber, out var cached))
        {
            return cached;
        }

        var objectStreamId = new PdfObjectId(objectStreamNumber);
        if (!_crossReferences.TryGetLocation(objectStreamNumber, out var location) ||
            location.Kind != PdfObjectLocationKind.InUse ||
            location.GenerationNumber != 0)
        {
            throw new PdfObjectResolutionException(
                "Object stream must have an uncompressed generation-0 xref entry",
                objectStreamId);
        }

        var indirectObject = GetIndirectObjectCore(objectStreamId);
        if (indirectObject.Value is not PdfStream stream)
        {
            throw new PdfObjectResolutionException(
                "Object stream xref entry does not identify a stream",
                objectStreamId,
                location.ByteOffset);
        }

        var type = ResolveDictionaryEntry(stream.Dictionary, "Type", objectStreamId);
        if (type is not PdfName { Value: "ObjStm" })
        {
            throw new PdfObjectResolutionException(
                "Object stream dictionary /Type must be /ObjStm",
                objectStreamId,
                location.ByteOffset);
        }

        var objectCount = ReadObjectStreamInteger(stream.Dictionary, "N", objectStreamId);
        var firstObjectOffset = ReadObjectStreamInteger(stream.Dictionary, "First", objectStreamId);
        var decodedData = new PdfStreamDecoder(this, _options.StreamDecoderOptions).Decode(stream);
        var result = PdfObjectStreamData.Parse(
            decodedData,
            objectCount,
            firstObjectOffset,
            _options.MaximumObjectsPerObjectStream);

        if (_objectStreamCache.Count >= _options.MaximumCachedObjectStreams)
        {
            throw new PdfObjectResolutionException(
                $"Object stream cache exceeds the configured limit of {_options.MaximumCachedObjectStreams} streams",
                objectStreamId,
                location.ByteOffset);
        }

        if (result.DecodedLength > _options.MaximumCachedObjectStreamBytes - _cachedObjectStreamBytes)
        {
            throw new PdfObjectResolutionException(
                $"Object stream cache exceeds the configured limit of {_options.MaximumCachedObjectStreamBytes} bytes",
                objectStreamId,
                location.ByteOffset);
        }

        _objectStreamCache.Add(objectStreamNumber, result);
        _cachedObjectStreamBytes += result.DecodedLength;
        return result;
    }

    private int ReadObjectStreamInteger(
        PdfDictionary dictionary,
        string key,
        PdfObjectId objectStreamId)
    {
        var value = ResolveDictionaryEntry(dictionary, key, objectStreamId);
        if (value is not PdfInteger integer || integer.Value is < 0 or > int.MaxValue)
        {
            throw new PdfObjectResolutionException(
                $"Object stream /{key} must resolve to a non-negative 32-bit integer",
                objectStreamId);
        }

        return (int)integer.Value;
    }

    private PdfObject ResolveDictionaryEntry(
        PdfDictionary dictionary,
        string key,
        PdfObjectId objectStreamId)
    {
        if (!dictionary.TryGetValue(key, out var value))
        {
            throw new PdfObjectResolutionException(
                $"Object stream dictionary is missing /{key}",
                objectStreamId);
        }

        return value is PdfIndirectReference reference ? Resolve(reference) : value;
    }

    private void AddToObjectCache(PdfIndirectObject value, long? offset)
    {
        if (_cache.Count >= _options.MaximumCachedObjects)
        {
            throw new PdfObjectResolutionException(
                $"Object cache exceeds the configured limit of {_options.MaximumCachedObjects}",
                value.ObjectId,
                offset);
        }

        _cache.Add(value.ObjectId, value);
    }

    private PdfObject ResolveStreamLength(PdfIndirectReference reference) => Resolve(reference);

    private PdfIndirectObject ParseIndirectObject(PdfObjectId objectId, long byteOffset)
    {
        if (_input is not null)
        {
            return _parser.Parse(_input, checked((int)byteOffset), ResolveStreamLength);
        }

        var boundaryIndex = Array.BinarySearch(_sourceBoundaries, byteOffset);
        boundaryIndex = boundaryIndex >= 0 ? boundaryIndex + 1 : ~boundaryIndex;
        var endOffset = boundaryIndex < _sourceBoundaries.Length
            ? _sourceBoundaries[boundaryIndex]
            : _source!.Length;
        var length = endOffset - byteOffset;
        if (length <= 0 || length > _options.MaximumIndirectObjectLength || length > int.MaxValue)
        {
            throw new PdfObjectResolutionException(
                $"Indirect object source range exceeds the configured limit of {_options.MaximumIndirectObjectLength} bytes",
                objectId,
                byteOffset);
        }

        var bytes = GC.AllocateUninitializedArray<byte>((int)length);
        _source!.ReadExactly(byteOffset, bytes);
        return _parser.Parse(bytes, 0, ResolveStreamLength);
    }

    private long[] CreateSourceBoundaries(long sourceLength) =>
        _crossReferences.Entries
            .Where(entry => entry.Kind == PdfObjectLocationKind.InUse && entry.ByteOffset is not null)
            .Select(entry => entry.ByteOffset!.Value)
            .Concat(_crossReferences.Sections.Select(section => section.CrossReferenceOffset))
            .Append(sourceLength)
            .Where(offset => offset >= 0 && offset <= sourceLength)
            .Distinct()
            .Order()
            .ToArray();

    private void ValidateDecryptionContext()
    {
        if (_options.DecryptionContext is not { } context)
        {
            return;
        }

        if (!_crossReferences.Trailer.TryGetValue("Encrypt", out var encryption) ||
            (encryption, context.EncryptionObjectId) switch
            {
                (PdfDictionary, null) => false,
                (PdfIndirectReference reference, PdfObjectId id) when reference.ObjectId == id => false,
                _ => true,
            })
        {
            throw new PdfEncryptionException(
                "The decryption context does not match the document encryption dictionary");
        }
    }
}
