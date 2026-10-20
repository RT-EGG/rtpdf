using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Storage;

namespace Rtpdf.Core.Filters;

/// <summary>
/// Applies a stream's declared filters in order while retaining encoded bytes on the source object.
/// </summary>
public sealed class PdfStreamDecoder
{
    private readonly Dictionary<string, IPdfFilter> _filters;
    private readonly PdfObjectStore? _objectStore;
    private readonly PdfStreamDecoderOptions _options;

    public PdfStreamDecoder(PdfStreamDecoderOptions? options = null)
    {
        _options = GetValidatedOptions(options);
        _filters = CreateFilterRegistry();
    }

    public PdfStreamDecoder(
        PdfObjectStore objectStore,
        PdfStreamDecoderOptions? options = null)
    {
        _objectStore = objectStore ?? throw new ArgumentNullException(nameof(objectStore));
        _options = GetValidatedOptions(options);
        _filters = CreateFilterRegistry();
    }

    public byte[] Decode(PdfStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var stages = GetFilterStages(stream.Dictionary);
        var current = stream.GetEncodedDataCopy();
        if (stages.Count == 0)
        {
            EnsureDecodedLength(current.Length, filterName: null);
            return current;
        }

        foreach (var stage in stages)
        {
            if (!_filters.TryGetValue(stage.Name, out var filter))
            {
                throw new PdfFilterException("Stream filter is not supported", stage.Name);
            }

            current = filter.Decode(current, stage.DecodeParameters, _options.MaximumDecodedStreamLength);
            EnsureDecodedLength(current.Length, stage.Name);
        }

        return current;
    }

    private IReadOnlyList<FilterStage> GetFilterStages(PdfDictionary dictionary)
    {
        if (!dictionary.TryGetValue("Filter", out var rawFilter))
        {
            if (dictionary.ContainsKey("DecodeParms"))
            {
                throw new PdfFilterException("/DecodeParms requires /Filter");
            }

            return [];
        }

        var filterObject = Resolve(rawFilter, "Filter");
        var filterWasArray = filterObject is PdfArray;
        var names = filterObject switch
        {
            PdfName name => [name.Value],
            PdfArray array => ReadFilterNames(array),
            _ => throw new PdfFilterException("/Filter must resolve to a name or an array of names"),
        };

        if (names.Count > _options.MaximumFilterCount)
        {
            throw new PdfFilterException(
                $"Stream filter count exceeds the configured limit of {_options.MaximumFilterCount}");
        }

        var parameters = ReadDecodeParameters(dictionary, names.Count, filterWasArray);
        var stages = new FilterStage[names.Count];
        for (var index = 0; index < names.Count; index++)
        {
            stages[index] = new FilterStage(names[index], parameters[index]);
        }

        return stages;
    }

    private IReadOnlyList<string> ReadFilterNames(PdfArray array)
    {
        var names = new string[array.Count];
        for (var index = 0; index < array.Count; index++)
        {
            if (Resolve(array[index], "Filter") is not PdfName name)
            {
                throw new PdfFilterException("Every /Filter array entry must resolve to a name");
            }

            names[index] = name.Value;
        }

        return names;
    }

    private IReadOnlyList<PdfDictionary?> ReadDecodeParameters(
        PdfDictionary dictionary,
        int filterCount,
        bool filterWasArray)
    {
        if (!dictionary.TryGetValue("DecodeParms", out var rawParameters))
        {
            return new PdfDictionary?[filterCount];
        }

        var parameterObject = Resolve(rawParameters, "DecodeParms");
        if (!filterWasArray)
        {
            return [ReadParameterDictionary(parameterObject)];
        }

        if (parameterObject is not PdfArray array || array.Count != filterCount)
        {
            throw new PdfFilterException(
                "/DecodeParms must be an array matching the /Filter array");
        }

        var parameters = new PdfDictionary?[filterCount];
        for (var index = 0; index < filterCount; index++)
        {
            parameters[index] = ReadParameterDictionary(Resolve(array[index], "DecodeParms"));
        }

        return parameters;
    }

    private static PdfDictionary? ReadParameterDictionary(PdfObject value) => value switch
    {
        PdfDictionary dictionary => dictionary,
        PdfNull => null,
        _ => throw new PdfFilterException("/DecodeParms entries must be dictionaries or null"),
    };

    private PdfObject Resolve(PdfObject value, string entryName)
    {
        if (value is not PdfIndirectReference reference)
        {
            return value;
        }

        if (_objectStore is null)
        {
            throw new PdfFilterException($"Indirect /{entryName} requires an object store");
        }

        try
        {
            return _objectStore.Resolve(reference);
        }
        catch (PdfObjectResolutionException exception)
        {
            throw new PdfFilterException($"Indirect /{entryName} could not be resolved", innerException: exception);
        }
    }

    private void EnsureDecodedLength(int length, string? filterName)
    {
        if (length > _options.MaximumDecodedStreamLength)
        {
            throw new PdfFilterException(
                $"Decoded stream exceeds the configured limit of {_options.MaximumDecodedStreamLength} bytes",
                filterName);
        }
    }

    private static PdfStreamDecoderOptions GetValidatedOptions(PdfStreamDecoderOptions? options)
    {
        var result = options ?? new PdfStreamDecoderOptions();
        result.Validate();
        return result;
    }

    private static Dictionary<string, IPdfFilter> CreateFilterRegistry()
    {
        IPdfFilter[] filters =
        [
            new FlateDecodeFilter(),
            new LzwDecodeFilter(),
            new AsciiHexDecodeFilter(),
            new Ascii85DecodeFilter(),
            new RunLengthDecodeFilter(),
        ];
        return filters.ToDictionary(filter => filter.Name, StringComparer.Ordinal);
    }

    private sealed record FilterStage(string Name, PdfDictionary? DecodeParameters);
}
