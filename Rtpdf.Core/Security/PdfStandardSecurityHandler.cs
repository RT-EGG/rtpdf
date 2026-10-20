using System.Buffers.Binary;
using System.Security.Cryptography;
using Rtpdf.Core.CrossReferences;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Storage;
using Rtpdf.Core.Text;

namespace Rtpdf.Core.Security;

/// <summary>
/// Authenticates PDF Standard Security Handler revisions 2 through 4.
/// </summary>
public static class PdfStandardSecurityHandler
{
    private static readonly byte[] PasswordPadding =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41,
        0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80,
        0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    public static PdfDecryptionContext Authenticate(
        ReadOnlyMemory<byte> documentData,
        PdfCrossReferenceTable crossReferences,
        string? password = null)
    {
        ArgumentNullException.ThrowIfNull(crossReferences);
        if (!crossReferences.Trailer.TryGetValue("Encrypt", out var rawEncrypt))
        {
            throw new PdfEncryptionException("The document is not encrypted");
        }

        var dictionary = PdfEncryptionDetector.LoadEncryptionDictionary(
            documentData, crossReferences, rawEncrypt, out var encryptionObjectId);

        if (PdfEncryptionDetector.ReadName(dictionary, "Filter") != "Standard")
        {
            throw new PdfEncryptionException("Only the Standard Security Handler is supported");
        }

        var version = PdfEncryptionDetector.ReadInteger(dictionary, "V", 0);
        var revision = PdfEncryptionDetector.ReadInteger(dictionary, "R");
        if (version is not (1 or 2 or 4) || revision is < 2 or > 4)
        {
            throw new PdfEncryptionException($"Standard Security Handler V={version}, R={revision} is not supported");
        }

        if ((revision == 3 && version != 2) || (revision == 4 && version != 4))
        {
            throw new PdfEncryptionException($"Standard Security Handler V={version}, R={revision} is inconsistent");
        }

        var keyLengthBits = version == 1 ? 40 : PdfEncryptionDetector.ReadInteger(dictionary, "Length", 40);
        if (keyLengthBits is < 40 or > 128 || keyLengthBits % 8 != 0 || (revision == 2 && keyLengthBits != 40))
        {
            throw new PdfEncryptionException("Encryption key length must be a supported multiple of 8 from 40 to 128 bits");
        }

        var ownerEntry = ReadBytes(dictionary, "O", 32);
        var userEntry = ReadBytes(dictionary, "U", 32);
        var permissions = PdfEncryptionDetector.ReadInteger(dictionary, "P");
        var fileId = ReadFileId(crossReferences.Trailer);
        var encryptMetadata = !dictionary.TryGetValue("EncryptMetadata", out var metadataValue) ||
            metadataValue is PdfBoolean { Value: true };
        if (metadataValue is not null && metadataValue is not PdfBoolean)
        {
            throw new PdfEncryptionException("Encryption dictionary /EncryptMetadata must be a boolean");
        }

        var passwordBytes = EncodePassword(password ?? string.Empty);
        var paddedPassword = PadPassword(passwordBytes);
        var keyLength = keyLengthBits / 8;
        var userKey = ComputeFileKey(paddedPassword, ownerEntry, permissions, fileId, revision, keyLength, encryptMetadata);
        PdfPasswordAuthenticationKind authenticationKind;
        byte[] fileKey;
        if (ValidateUserPassword(userKey, userEntry, fileId, revision))
        {
            authenticationKind = PdfPasswordAuthenticationKind.User;
            fileKey = userKey;
        }
        else
        {
            var recoveredUserPassword = RecoverUserPassword(passwordBytes, ownerEntry, revision, keyLength);
            fileKey = ComputeFileKey(recoveredUserPassword, ownerEntry, permissions, fileId, revision, keyLength, encryptMetadata);
            if (!ValidateUserPassword(fileKey, userEntry, fileId, revision))
            {
                throw new PdfEncryptionException("The password is not valid for this document");
            }

            authenticationKind = PdfPasswordAuthenticationKind.Owner;
        }

        return PdfDecryptionContext.Create(
            fileKey,
            encryptionObjectId,
            version,
            revision,
            permissions,
            encryptMetadata,
            authenticationKind,
            dictionary);
    }

    public static PdfDecryptionContext Authenticate(
        Stream documentData,
        PdfCrossReferenceTable crossReferences,
        string? password = null)
    {
        ArgumentNullException.ThrowIfNull(documentData);
        using var buffer = new MemoryStream();
        documentData.CopyTo(buffer);
        return Authenticate(buffer.ToArray(), crossReferences, password);
    }

    private static byte[] ComputeFileKey(
        byte[] paddedPassword,
        byte[] ownerEntry,
        int permissions,
        byte[] fileId,
        int revision,
        int keyLength,
        bool encryptMetadata)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        hash.AppendData(paddedPassword);
        hash.AppendData(ownerEntry);
        Span<byte> permissionBytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(permissionBytes, permissions);
        hash.AppendData(permissionBytes);
        hash.AppendData(fileId);
        if (revision >= 4 && !encryptMetadata)
        {
            hash.AppendData([0xFF, 0xFF, 0xFF, 0xFF]);
        }

        var digest = hash.GetHashAndReset();
        if (revision >= 3)
        {
            for (var iteration = 0; iteration < 50; iteration++)
            {
                digest = MD5.HashData(digest.AsSpan(0, keyLength));
            }
        }

        return digest[..keyLength];
    }

    private static bool ValidateUserPassword(byte[] fileKey, byte[] userEntry, byte[] fileId, int revision)
    {
        if (revision == 2)
        {
            return CryptographicOperations.FixedTimeEquals(Rc4(fileKey, PasswordPadding), userEntry);
        }

        var value = MD5.HashData([.. PasswordPadding, .. fileId]);
        value = Rc4(fileKey, value);
        for (var iteration = 1; iteration <= 19; iteration++)
        {
            value = Rc4(XorKey(fileKey, iteration), value);
        }

        return CryptographicOperations.FixedTimeEquals(value.AsSpan(0, 16), userEntry.AsSpan(0, 16));
    }

    private static byte[] RecoverUserPassword(byte[] ownerPassword, byte[] ownerEntry, int revision, int keyLength)
    {
        var digest = MD5.HashData(PadPassword(ownerPassword));
        if (revision >= 3)
        {
            for (var iteration = 0; iteration < 50; iteration++)
            {
                digest = MD5.HashData(digest.AsSpan(0, keyLength));
            }
        }

        var key = digest[..keyLength];
        var value = ownerEntry.ToArray();
        if (revision == 2)
        {
            return Rc4(key, value);
        }

        for (var iteration = 19; iteration >= 0; iteration--)
        {
            value = Rc4(XorKey(key, iteration), value);
        }

        return value;
    }

    private static byte[] ReadBytes(PdfDictionary dictionary, string key, int expectedLength)
    {
        if (!dictionary.TryGetValue(key, out var value) || value is not PdfString text || text.Bytes.Length != expectedLength)
        {
            throw new PdfEncryptionException($"Encryption dictionary /{key} must be a string of exactly {expectedLength} bytes");
        }

        return text.Bytes.ToArray();
    }

    private static byte[] ReadFileId(PdfDictionary trailer)
    {
        if (!trailer.TryGetValue("ID", out var value) || value is not PdfArray { Count: > 0 } ids ||
            ids[0] is not PdfString firstId)
        {
            throw new PdfEncryptionException("Encrypted document trailer must contain a string as the first /ID entry");
        }

        return firstId.Bytes.ToArray();
    }

    private static byte[] EncodePassword(string password)
    {
        var result = new List<byte>(Math.Min(password.Length, 32));
        foreach (var character in password)
        {
            var found = false;
            for (var value = 0; value <= byte.MaxValue; value++)
            {
                if (value is 0x7F or 0x9F || PdfDocEncoding.Decode([(byte)value])[0] != character)
                {
                    continue;
                }

                result.Add((byte)value);
                found = true;
                break;
            }

            if (!found)
            {
                throw new PdfEncryptionException("Password contains a character that cannot be represented in PDFDocEncoding");
            }

            if (result.Count == 32)
            {
                break;
            }
        }

        return result.ToArray();
    }

    private static byte[] PadPassword(ReadOnlySpan<byte> password)
    {
        var result = new byte[32];
        var copied = Math.Min(password.Length, result.Length);
        password[..copied].CopyTo(result);
        PasswordPadding.AsSpan(0, result.Length - copied).CopyTo(result.AsSpan(copied));
        return result;
    }

    internal static byte[] Rc4(ReadOnlySpan<byte> key, ReadOnlySpan<byte> input)
    {
        Span<byte> state = stackalloc byte[256];
        for (var index = 0; index < state.Length; index++) state[index] = (byte)index;
        var j = 0;
        for (var index = 0; index < state.Length; index++)
        {
            j = (j + state[index] + key[index % key.Length]) & 0xFF;
            (state[index], state[j]) = (state[j], state[index]);
        }

        var result = new byte[input.Length];
        var i = 0;
        j = 0;
        for (var index = 0; index < input.Length; index++)
        {
            i = (i + 1) & 0xFF;
            j = (j + state[i]) & 0xFF;
            (state[i], state[j]) = (state[j], state[i]);
            result[index] = (byte)(input[index] ^ state[(state[i] + state[j]) & 0xFF]);
        }

        return result;
    }

    private static byte[] XorKey(ReadOnlySpan<byte> key, int value)
    {
        var result = key.ToArray();
        for (var index = 0; index < result.Length; index++) result[index] ^= (byte)value;
        return result;
    }
}
