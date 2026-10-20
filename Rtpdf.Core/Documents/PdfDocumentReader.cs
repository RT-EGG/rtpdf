using Rtpdf.Core.IO;
using Rtpdf.Core.Parsing;
using Rtpdf.Core.Storage;

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
        using var source = PdfByteSource.OpenFile(path);
        return Open(source, options);
    }

    public static PdfDocument Open(
        Stream source,
        PdfDocumentReaderOptions? options = null,
        bool leaveOpen = false)
    {
        var validatedOptions = ValidateOptions(options);
        var maximumBufferedLength = Math.Min(validatedOptions.MaximumSourceLength, int.MaxValue);
        using var byteSource = StreamPdfByteSource.Create(
            source,
            leaveOpen,
            maximumBufferedLength);
        return Open(byteSource, validatedOptions);
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

        return OpenCore(source.ToArray(), validatedOptions);
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

        if (sourceLength > int.MaxValue)
        {
            throw new NotSupportedException(
                "PDF sources larger than Int32.MaxValue are not supported by the current parsers");
        }

        var bytes = GC.AllocateUninitializedArray<byte>((int)sourceLength);
        source.ReadExactly(0, bytes);
        return OpenCore(bytes, validatedOptions);
    }

    private static PdfDocument OpenCore(byte[] source, PdfDocumentReaderOptions options)
    {
        var crossReferences = new PdfCrossReferenceParser(
            source,
            options.CrossReferenceParserOptions).Parse();
        var objectStore = new PdfObjectStore(
            source,
            crossReferences,
            options.ObjectStoreOptions);
        var catalog = new PdfCatalogReader(
            crossReferences,
            objectStore,
            options.CatalogReaderOptions).Read();
        return new PdfDocument(source.LongLength, crossReferences, objectStore, catalog);
    }

    private static PdfDocumentReaderOptions ValidateOptions(PdfDocumentReaderOptions? options)
    {
        var result = options ?? new PdfDocumentReaderOptions();
        result.Validate();
        return result;
    }
}
