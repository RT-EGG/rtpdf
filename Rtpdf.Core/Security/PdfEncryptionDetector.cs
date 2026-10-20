using Rtpdf.Core.CrossReferences;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Storage;

namespace Rtpdf.Core.Security;

/// <summary>
/// Inspects a trailer encryption entry without attempting password authentication.
/// </summary>
public static class PdfEncryptionDetector
{
    public static PdfEncryptionInfo Inspect(
        ReadOnlyMemory<byte> documentData,
        PdfCrossReferenceTable crossReferences)
    {
        ArgumentNullException.ThrowIfNull(crossReferences);
        if (!crossReferences.Trailer.TryGetValue("Encrypt", out var rawEncrypt))
        {
            return PdfEncryptionInfo.Unencrypted;
        }

        var dictionary = LoadEncryptionDictionary(documentData, crossReferences, rawEncrypt, out var objectId);
        var filter = ReadName(dictionary, "Filter");
        var version = ReadInteger(dictionary, "V", defaultValue: 0);
        var revision = ReadInteger(dictionary, "R");
        var keyLength = version == 1 ? 40 : ReadInteger(dictionary, "Length", defaultValue: 40);
        return new PdfEncryptionInfo(true, filter, version, revision, keyLength, objectId);
    }

    internal static PdfDictionary LoadEncryptionDictionary(
        ReadOnlyMemory<byte> documentData,
        PdfCrossReferenceTable crossReferences,
        PdfObject rawEncrypt,
        out PdfObjectId? objectId)
    {
        if (rawEncrypt is PdfDictionary directDictionary)
        {
            objectId = null;
            return directDictionary;
        }

        if (rawEncrypt is not PdfIndirectReference reference)
        {
            throw new PdfEncryptionException("Trailer /Encrypt must be a dictionary or an indirect reference to one");
        }

        try
        {
            objectId = reference.ObjectId;
            return new PdfObjectStore(documentData, crossReferences).Resolve<PdfDictionary>(reference);
        }
        catch (PdfObjectResolutionException exception)
        {
            throw new PdfEncryptionException("Encryption dictionary could not be loaded", exception);
        }
    }

    public static PdfEncryptionInfo Inspect(
        Stream documentData,
        PdfCrossReferenceTable crossReferences)
    {
        ArgumentNullException.ThrowIfNull(documentData);
        using var buffer = new MemoryStream();
        documentData.CopyTo(buffer);
        return Inspect(buffer.ToArray(), crossReferences);
    }

    internal static string ReadName(PdfDictionary dictionary, string key)
    {
        if (!dictionary.TryGetValue(key, out var value) || value is not PdfName name)
        {
            throw new PdfEncryptionException($"Encryption dictionary /{key} must be a name");
        }

        return name.Value;
    }

    internal static int ReadInteger(PdfDictionary dictionary, string key, int? defaultValue = null)
    {
        if (!dictionary.TryGetValue(key, out var value))
        {
            return defaultValue ?? throw new PdfEncryptionException($"Encryption dictionary is missing /{key}");
        }

        if (value is not PdfInteger integer || integer.Value is < int.MinValue or > int.MaxValue)
        {
            throw new PdfEncryptionException($"Encryption dictionary /{key} must be a 32-bit integer");
        }

        return (int)integer.Value;
    }
}
