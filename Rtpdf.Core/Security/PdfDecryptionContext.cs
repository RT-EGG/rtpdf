using System.Buffers.Binary;
using System.Security.Cryptography;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Security;

/// <summary>
/// An authenticated document key and crypt-filter configuration used for lazy object decryption.
/// </summary>
public sealed class PdfDecryptionContext
{
    private readonly byte[] _fileKey;
    private readonly CryptMethod _stringMethod;
    private readonly CryptMethod _streamMethod;

    private PdfDecryptionContext(
        byte[] fileKey,
        PdfObjectId? encryptionObjectId,
        int version,
        int revision,
        int permissions,
        bool encryptMetadata,
        PdfPasswordAuthenticationKind authenticationKind,
        CryptMethod stringMethod,
        CryptMethod streamMethod)
    {
        _fileKey = fileKey;
        EncryptionObjectId = encryptionObjectId;
        Version = version;
        Revision = revision;
        Permissions = permissions;
        EncryptMetadata = encryptMetadata;
        AuthenticationKind = authenticationKind;
        _stringMethod = stringMethod;
        _streamMethod = streamMethod;
    }

    public PdfObjectId? EncryptionObjectId { get; }
    public int Version { get; }
    public int Revision { get; }
    public int Permissions { get; }
    public bool EncryptMetadata { get; }
    public PdfPasswordAuthenticationKind AuthenticationKind { get; }

    internal static PdfDecryptionContext Create(
        byte[] fileKey,
        PdfObjectId? encryptionObjectId,
        int version,
        int revision,
        int permissions,
        bool encryptMetadata,
        PdfPasswordAuthenticationKind authenticationKind,
        PdfDictionary encryptionDictionary)
    {
        var stringMethod = CryptMethod.Rc4;
        var streamMethod = CryptMethod.Rc4;
        if (version == 4)
        {
            stringMethod = ReadDefaultMethod(encryptionDictionary, "StrF");
            streamMethod = ReadDefaultMethod(encryptionDictionary, "StmF");
        }

        if ((stringMethod == CryptMethod.Aes128 || streamMethod == CryptMethod.Aes128) &&
            fileKey.Length != 16)
        {
            throw new PdfEncryptionException("AESV2 requires a 128-bit document encryption key");
        }

        return new PdfDecryptionContext(
            fileKey.ToArray(), encryptionObjectId, version, revision, permissions,
            encryptMetadata, authenticationKind, stringMethod, streamMethod);
    }

    internal PdfObject DecryptObject(PdfObject value, PdfObjectId objectId)
    {
        return value switch
        {
            PdfString text => new PdfString(Decrypt(text.Bytes.Span, objectId, _stringMethod), text.Kind),
            PdfArray array => DecryptArray(array, objectId),
            PdfDictionary dictionary => DecryptDictionary(dictionary, objectId),
            PdfStream stream => DecryptStream(stream, objectId),
            _ => value,
        };
    }

    private PdfArray DecryptArray(PdfArray array, PdfObjectId objectId)
    {
        for (var index = 0; index < array.Count; index++)
        {
            array[index] = DecryptObject(array[index], objectId);
        }

        return array;
    }

    private PdfDictionary DecryptDictionary(PdfDictionary dictionary, PdfObjectId objectId)
    {
        foreach (var key in dictionary.Keys.ToArray())
        {
            dictionary[key] = DecryptObject(dictionary[key], objectId);
        }

        return dictionary;
    }

    private PdfStream DecryptStream(PdfStream stream, PdfObjectId objectId)
    {
        if (stream.Dictionary.TryGetValue("Type", out var originalType) &&
            originalType is PdfName { Value: "XRef" })
        {
            return stream;
        }

        var dictionary = DecryptDictionary(stream.Dictionary, objectId);
        if (dictionary.TryGetValue("Filter", out var filter) && ContainsCryptFilter(filter))
        {
            throw new PdfEncryptionException("Explicit /Crypt stream filters are not supported yet");
        }

        dictionary.TryGetValue("Type", out var type);
        var isPlainMetadata = !EncryptMetadata && type is PdfName { Value: "Metadata" };
        if (!isPlainMetadata && _streamMethod != CryptMethod.Identity)
        {
            stream.ReplaceEncodedData(Decrypt(stream.EncodedData.Span, objectId, _streamMethod));
        }

        return stream;
    }

    private byte[] Decrypt(ReadOnlySpan<byte> input, PdfObjectId objectId, CryptMethod method)
    {
        if (method == CryptMethod.Identity)
        {
            return input.ToArray();
        }

        Span<byte> keyMaterial = stackalloc byte[_fileKey.Length + 9];
        _fileKey.CopyTo(keyMaterial);
        keyMaterial[_fileKey.Length] = (byte)objectId.ObjectNumber;
        keyMaterial[_fileKey.Length + 1] = (byte)(objectId.ObjectNumber >> 8);
        keyMaterial[_fileKey.Length + 2] = (byte)(objectId.ObjectNumber >> 16);
        BinaryPrimitives.WriteUInt16LittleEndian(
            keyMaterial.Slice(_fileKey.Length + 3, 2),
            objectId.GenerationNumber);
        var materialLength = _fileKey.Length + 5;
        if (method == CryptMethod.Aes128)
        {
            "sAlT"u8.CopyTo(keyMaterial[materialLength..]);
            materialLength += 4;
        }

        var digest = MD5.HashData(keyMaterial[..materialLength]);
        var objectKey = digest[..Math.Min(_fileKey.Length + 5, 16)];
        return method switch
        {
            CryptMethod.Rc4 => PdfStandardSecurityHandler.Rc4(objectKey, input),
            CryptMethod.Aes128 => DecryptAes(input, objectKey),
            _ => input.ToArray(),
        };
    }

    private static byte[] DecryptAes(ReadOnlySpan<byte> input, byte[] key)
    {
        if (input.Length < 32 || (input.Length - 16) % 16 != 0)
        {
            throw new PdfEncryptionException("AES-encrypted object data has an invalid length");
        }

        try
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = input[..16].ToArray();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(input[16..].ToArray(), 0, input.Length - 16);
        }
        catch (CryptographicException exception)
        {
            throw new PdfEncryptionException("AES-encrypted object data is invalid", exception);
        }
    }

    private static CryptMethod ReadDefaultMethod(PdfDictionary dictionary, string key)
    {
        var filterName = dictionary.TryGetValue(key, out var rawFilter)
            ? (rawFilter as PdfName)?.Value
            : "Identity";
        if (filterName is null)
        {
            throw new PdfEncryptionException($"Encryption dictionary /{key} must be a name");
        }

        if (filterName == "Identity") return CryptMethod.Identity;
        if (!dictionary.TryGetValue("CF", out var rawFilters) || rawFilters is not PdfDictionary filters ||
            !filters.TryGetValue(filterName, out var rawDefinition) || rawDefinition is not PdfDictionary definition)
        {
            throw new PdfEncryptionException($"Crypt filter /{filterName} is not defined");
        }

        var method = definition.TryGetValue("CFM", out var rawMethod)
            ? (rawMethod as PdfName)?.Value
            : "None";
        var result = method switch
        {
            "None" => CryptMethod.Identity,
            "V2" => CryptMethod.Rc4,
            "AESV2" => CryptMethod.Aes128,
            _ => throw new PdfEncryptionException($"Crypt filter method /{method} is not supported"),
        };
        if (result == CryptMethod.Aes128 &&
            definition.TryGetValue("Length", out var rawLength) &&
            rawLength is not PdfInteger { Value: 16 })
        {
            throw new PdfEncryptionException("AESV2 crypt filter /Length must be 16 bytes");
        }

        return result;
    }

    private static bool ContainsCryptFilter(PdfObject value) => value switch
    {
        PdfName { Value: "Crypt" } => true,
        PdfArray array => array.Any(item => item is PdfName { Value: "Crypt" }),
        _ => false,
    };

    private enum CryptMethod
    {
        Identity,
        Rc4,
        Aes128,
    }
}
