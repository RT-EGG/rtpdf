using Rtpdf.Core.IO;
using Rtpdf.Core.Parsing;
using Rtpdf.Core.Storage;
using Rtpdf.Core.Security;

namespace Rtpdf.Core.Documents;

/// <summary>
/// Opens a PDF source and constructs its cross references, object store, and catalog.
/// </summary>
public static class PdfDocumentReader
{
    public static PdfDocument Open(
        string path,
        PdfDocumentReaderOptions? options = null)
    {
        var source = PdfByteSource.OpenFile(path);
        try
        {
            return OpenCore(source, ValidateOptions(options), ownedSource: source);
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    public static PdfDocument Open(
        Stream source,
        PdfDocumentReaderOptions? options = null,
        bool leaveOpen = false)
    {
        var validatedOptions = ValidateOptions(options);
        var maximumBufferedLength = Math.Min(validatedOptions.MaximumSourceLength, int.MaxValue);
        var byteSource = StreamPdfByteSource.Create(
            source,
            leaveOpen,
            maximumBufferedLength);
        try
        {
            return OpenCore(byteSource, validatedOptions, ownedSource: byteSource);
        }
        catch
        {
            byteSource.Dispose();
            throw;
        }
    }

    public static PdfDocument Open(
        ReadOnlyMemory<byte> source,
        PdfDocumentReaderOptions? options = null)
    {
        var validatedOptions = ValidateOptions(options);
        var sourceLength = source.Length;
        if (sourceLength > validatedOptions.MaximumSourceLength)
        {
            throw new InvalidDataException(
                $"PDF source exceeds the configured limit of {validatedOptions.MaximumSourceLength} bytes");
        }

        var byteSource = PdfByteSource.FromMemory(source);
        try
        {
            return OpenCore(byteSource, validatedOptions, ownedSource: byteSource);
        }
        catch
        {
            byteSource.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens a caller-owned byte source. The source is not disposed by this method.
    /// </summary>
    public static PdfDocument Open(
        PdfByteSource source,
        PdfDocumentReaderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var validatedOptions = ValidateOptions(options);
        var sourceLength = source.Length;
        if (sourceLength < 0)
        {
            throw new InvalidDataException("PDF byte source length must not be negative");
        }

        if (sourceLength > validatedOptions.MaximumSourceLength)
        {
            throw new InvalidDataException(
                $"PDF source exceeds the configured limit of {validatedOptions.MaximumSourceLength} bytes");
        }

        return OpenCore(source, validatedOptions, ownedSource: null);
    }

    private static PdfDocument OpenCore(
        PdfByteSource source,
        PdfDocumentReaderOptions options,
        IDisposable? ownedSource)
    {
        if (source.Length > options.MaximumSourceLength)
        {
            throw new InvalidDataException(
                $"PDF source exceeds the configured limit of {options.MaximumSourceLength} bytes");
        }

        if (source.Length > int.MaxValue)
        {
            throw new NotSupportedException(
                "PDF sources larger than Int32.MaxValue are not supported by the current xref parser");
        }

        // The xref parser is currently array-based. Keep this buffer only while opening;
        // indirect objects are subsequently loaded on demand from the byte source.
        var xrefInput = GC.AllocateUninitializedArray<byte>((int)source.Length);
        source.ReadExactly(0, xrefInput);
        var crossReferences = new PdfCrossReferenceParser(
            xrefInput,
            options.CrossReferenceParserOptions).Parse();
        var objectStoreOptions = options.ObjectStoreOptions;
        if (crossReferences.Trailer.ContainsKey("Encrypt") && objectStoreOptions.DecryptionContext is null)
        {
            var context = PdfStandardSecurityHandler.Authenticate(
                xrefInput,
                crossReferences,
                options.Password);
            objectStoreOptions = CopyWithDecryptionContext(objectStoreOptions, context);
        }

        var objectStore = new PdfObjectStore(
            source,
            crossReferences,
            objectStoreOptions);
        var catalog = new PdfCatalogReader(
            crossReferences,
            objectStore,
            options.CatalogReaderOptions).Read();
        return new PdfDocument(source.Length, crossReferences, objectStore, catalog, ownedSource);
    }

    private static PdfObjectStoreOptions CopyWithDecryptionContext(
        PdfObjectStoreOptions options,
        PdfDecryptionContext context) =>
        new()
        {
            MaximumIndirectObjectLength = options.MaximumIndirectObjectLength,
            MaximumReferenceDepth = options.MaximumReferenceDepth,
            MaximumCachedObjects = options.MaximumCachedObjects,
            MaximumObjectsPerObjectStream = options.MaximumObjectsPerObjectStream,
            MaximumCachedObjectStreams = options.MaximumCachedObjectStreams,
            MaximumCachedObjectStreamBytes = options.MaximumCachedObjectStreamBytes,
            ObjectParserOptions = options.ObjectParserOptions,
            StreamDecoderOptions = options.StreamDecoderOptions,
            DecryptionContext = context,
        };

    private static PdfDocumentReaderOptions ValidateOptions(PdfDocumentReaderOptions? options)
    {
        var result = options ?? new PdfDocumentReaderOptions();
        result.Validate();
        return result;
    }
}
