using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Filters;
using Rtpdf.Core.Objects;
using Rtpdf.Core.Parsing;
using Rtpdf.Core.Security;
using Rtpdf.Core.Storage;
using Xunit;

namespace Rtpdf.Core.Tests.Security;

public sealed class PdfStandardSecurityHandlerTests
{
    [Fact]
    public void DetectorReportsUnencryptedAndStandardEncryptionMetadata()
    {
        var plain = BuildDocument(["(plain)"u8.ToArray()], trailerEntries: "");
        var encrypted = CreateEncryptedDocument(revision: 2, useAes: false);

        var plainInfo = PdfEncryptionDetector.Inspect(plain.Bytes, plain.Table);
        var encryptedInfo = PdfEncryptionDetector.Inspect(encrypted.Bytes, encrypted.Table);

        Assert.False(plainInfo.IsEncrypted);
        Assert.True(encryptedInfo.IsEncrypted);
        Assert.Equal("Standard", encryptedInfo.SecurityHandler);
        Assert.Equal(1, encryptedInfo.Version);
        Assert.Equal(2, encryptedInfo.Revision);
        Assert.Equal(40, encryptedInfo.KeyLengthBits);
        Assert.Equal(new PdfObjectId(3), encryptedInfo.EncryptionObjectId);
    }

    [Theory]
    [InlineData("user", PdfPasswordAuthenticationKind.User)]
    [InlineData("owner", PdfPasswordAuthenticationKind.Owner)]
    public void Revision2AuthenticatesPasswordsAndDecryptsNestedStrings(
        string password,
        PdfPasswordAuthenticationKind expectedKind)
    {
        var document = CreateEncryptedDocument(revision: 2, useAes: false);

        var context = PdfStandardSecurityHandler.Authenticate(document.Bytes, document.Table, password);
        var store = CreateStore(document, context);
        var dictionary = store.Resolve<PdfDictionary>(new PdfIndirectReference(1));

        Assert.Equal(expectedKind, context.AuthenticationKind);
        Assert.Equal(-4, context.Permissions);
        Assert.Equal("secret", Assert.IsType<PdfString>(dictionary["Text"]).GetText());
        var array = Assert.IsType<PdfArray>(dictionary["Nested"]);
        Assert.Equal("inside", Assert.IsType<PdfString>(array[0]).GetText());
    }

    [Fact]
    public void AuthenticationRejectsAnIncorrectPassword()
    {
        var document = CreateEncryptedDocument(revision: 2, useAes: false);

        var exception = Assert.Throws<PdfEncryptionException>(() =>
            PdfStandardSecurityHandler.Authenticate(document.Bytes, document.Table, "wrong"));

        Assert.Contains("not valid", exception.Message);
    }

    [Fact]
    public void StreamAuthenticationUsesItsCurrentPositionAndLeavesItOpen()
    {
        var document = CreateEncryptedDocument(revision: 2, useAes: false);
        using var stream = new MemoryStream([1, 2, 3, .. document.Bytes]);
        stream.Position = 3;

        var context = PdfStandardSecurityHandler.Authenticate(stream, document.Table, "user");

        Assert.Equal(PdfPasswordAuthenticationKind.User, context.AuthenticationKind);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Revision4AesDecryptsBeforeApplyingStreamFilters()
    {
        var document = CreateEncryptedDocument(revision: 4, useAes: true);
        var context = PdfStandardSecurityHandler.Authenticate(document.Bytes, document.Table, "user");
        var store = CreateStore(document, context);

        var text = store.Resolve<PdfString>(new PdfIndirectReference(1));
        var stream = store.Resolve<PdfStream>(new PdfIndirectReference(2));
        var decoded = new PdfStreamDecoder(store).Decode(stream);

        Assert.Equal("secret", text.GetText());
        Assert.Equal("filtered stream data"u8.ToArray(), decoded);
        Assert.Equal(4, context.Version);
        Assert.Equal(4, context.Revision);
    }

    [Fact]
    public void Revision3Rc4UsesA128BitDocumentKey()
    {
        var document = CreateEncryptedDocument(revision: 3, useAes: false);
        var context = PdfStandardSecurityHandler.Authenticate(document.Bytes, document.Table, "owner");

        var value = CreateStore(document, context).Resolve<PdfDictionary>(new PdfIndirectReference(1));

        Assert.Equal("secret", Assert.IsType<PdfString>(value["Text"]).GetText());
        Assert.Equal(3, context.Revision);
    }

    [Fact]
    public void Revision4CanLeaveMetadataStreamsUnencrypted()
    {
        var document = CreateEncryptedDocument(
            revision: 4,
            useAes: true,
            encryptMetadata: false,
            plainMetadataStream: true);
        var context = PdfStandardSecurityHandler.Authenticate(document.Bytes, document.Table, "user");

        var stream = CreateStore(document, context).Resolve<PdfStream>(new PdfIndirectReference(2));

        Assert.False(context.EncryptMetadata);
        Assert.Equal("metadata"u8.ToArray(), stream.GetEncodedDataCopy());
    }

    [Fact]
    public void EncryptionDictionaryIsNeverDecrypted()
    {
        var document = CreateEncryptedDocument(revision: 2, useAes: false);
        var context = PdfStandardSecurityHandler.Authenticate(document.Bytes, document.Table, "user");
        var store = CreateStore(document, context);

        var dictionary = store.Resolve<PdfDictionary>(new PdfIndirectReference(3));

        Assert.Equal("Standard", Assert.IsType<PdfName>(dictionary["Filter"]).Value);
        Assert.Equal(32, Assert.IsType<PdfString>(dictionary["O"]).Bytes.Length);
    }

    [Fact]
    public void AuthenticateRejectsUnsupportedSecurityHandlerRevision()
    {
        var document = CreateEncryptedDocument(revision: 2, useAes: false);
        var revisionOffset = document.Bytes.AsSpan().IndexOf("/R 2"u8);
        document.Bytes[revisionOffset + 3] = (byte)'6';
        document = document with { Table = new PdfCrossReferenceParser(document.Bytes).Parse() };

        Assert.Throws<PdfEncryptionException>(() =>
            PdfStandardSecurityHandler.Authenticate(document.Bytes, document.Table, "user"));
    }

    private static PdfObjectStore CreateStore(TestDocument document, PdfDecryptionContext context) =>
        new(document.Bytes, document.Table, new PdfObjectStoreOptions { DecryptionContext = context });

    private static TestDocument CreateEncryptedDocument(
        int revision,
        bool useAes,
        bool encryptMetadata = true,
        bool plainMetadataStream = false)
    {
        var version = revision switch { 2 => 1, 3 => 2, _ => 4 };
        var keyLength = revision == 2 ? 5 : 16;
        var fileId = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");
        var security = CreateSecurityValues(
            "user", "owner", revision, keyLength, fileId, permissions: -4, encryptMetadata);
        byte[] object1;
        byte[] object2;

        if (useAes)
        {
            object1 = Encoding.ASCII.GetBytes($"<{Convert.ToHexString(EncryptObject("secret"u8, security.FileKey, 1, 0, aes: true))}>");
            if (plainMetadataStream)
            {
                object2 = "<< /Type /Metadata /Length 8 >>\nstream\nmetadata\nendstream"u8.ToArray();
            }
            else
            {
                var compressed = Compress("filtered stream data"u8);
                var encryptedStream = EncryptObject(compressed, security.FileKey, 2, 0, aes: true);
                object2 = Combine(
                    Encoding.ASCII.GetBytes($"<< /Length {encryptedStream.Length} /Filter /FlateDecode >>\nstream\n"),
                    encryptedStream,
                    "\nendstream"u8.ToArray());
            }
        }
        else
        {
            var first = EncryptObject("secret"u8, security.FileKey, 1, 0, aes: false);
            var nested = EncryptObject("inside"u8, security.FileKey, 1, 0, aes: false);
            object1 = Encoding.ASCII.GetBytes(
                $"<< /Text <{Convert.ToHexString(first)}> /Nested [<{Convert.ToHexString(nested)}>] >>");
            object2 = "null"u8.ToArray();
        }

        var cryptFilters = useAes
            ? " /CF << /StdCF << /CFM /AESV2 /Length 16 >> >> /StmF /StdCF /StrF /StdCF"
            : string.Empty;
        var encryptionDictionary = Encoding.ASCII.GetBytes(
            $"<< /Filter /Standard /V {version} /R {revision} /Length {keyLength * 8}" +
            $" /O <{Convert.ToHexString(security.OwnerEntry)}>" +
            $" /U <{Convert.ToHexString(security.UserEntry)}> /P -4{cryptFilters}" +
            $" /EncryptMetadata {encryptMetadata.ToString().ToLowerInvariant()} >>");
        var trailer = $"/Encrypt 3 0 R /ID [<{Convert.ToHexString(fileId)}> <{Convert.ToHexString(fileId)}>]";
        return BuildDocument([object1, object2, encryptionDictionary], trailer);
    }

    private static SecurityValues CreateSecurityValues(
        string userPassword,
        string ownerPassword,
        int revision,
        int keyLength,
        byte[] fileId,
        int permissions,
        bool encryptMetadata)
    {
        var paddedUser = Pad(Encoding.ASCII.GetBytes(userPassword));
        var ownerDigest = MD5.HashData(Pad(Encoding.ASCII.GetBytes(ownerPassword)));
        if (revision >= 3)
        {
            for (var index = 0; index < 50; index++) ownerDigest = MD5.HashData(ownerDigest.AsSpan(0, keyLength));
        }

        var ownerKey = ownerDigest[..keyLength];
        var ownerEntry = paddedUser.ToArray();
        if (revision == 2)
        {
            ownerEntry = Rc4(ownerKey, ownerEntry);
        }
        else
        {
            for (var index = 0; index <= 19; index++) ownerEntry = Rc4(Xor(ownerKey, index), ownerEntry);
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        hash.AppendData(paddedUser);
        hash.AppendData(ownerEntry);
        Span<byte> permissionBytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(permissionBytes, permissions);
        hash.AppendData(permissionBytes);
        hash.AppendData(fileId);
        if (revision >= 4 && !encryptMetadata) hash.AppendData([0xFF, 0xFF, 0xFF, 0xFF]);
        var digest = hash.GetHashAndReset();
        if (revision >= 3)
        {
            for (var index = 0; index < 50; index++) digest = MD5.HashData(digest.AsSpan(0, keyLength));
        }

        var fileKey = digest[..keyLength];
        byte[] userEntry;
        if (revision == 2)
        {
            userEntry = Rc4(fileKey, Padding);
        }
        else
        {
            var value = MD5.HashData(Combine(Padding, fileId));
            value = Rc4(fileKey, value);
            for (var index = 1; index <= 19; index++) value = Rc4(Xor(fileKey, index), value);
            userEntry = [.. value, .. new byte[16]];
        }

        return new SecurityValues(fileKey, ownerEntry, userEntry);
    }

    private static byte[] EncryptObject(
        ReadOnlySpan<byte> value,
        byte[] fileKey,
        int objectNumber,
        ushort generation,
        bool aes)
    {
        var material = new List<byte>(fileKey);
        material.Add((byte)objectNumber);
        material.Add((byte)(objectNumber >> 8));
        material.Add((byte)(objectNumber >> 16));
        material.Add((byte)generation);
        material.Add((byte)(generation >> 8));
        if (aes) material.AddRange("sAlT"u8.ToArray());
        var key = MD5.HashData(material.ToArray())[..Math.Min(fileKey.Length + 5, 16)];
        if (!aes) return Rc4(key, value);

        var iv = Enumerable.Range(0, 16).Select(index => (byte)index).ToArray();
        using var algorithm = Aes.Create();
        algorithm.Key = key;
        algorithm.IV = iv;
        algorithm.Mode = CipherMode.CBC;
        algorithm.Padding = PaddingMode.PKCS7;
        using var encryptor = algorithm.CreateEncryptor();
        return Combine(iv, encryptor.TransformFinalBlock(value.ToArray(), 0, value.Length));
    }

    private static TestDocument BuildDocument(IReadOnlyList<byte[]> objects, string trailerEntries)
    {
        using var output = new MemoryStream();
        output.Write("%PDF-1.7\n"u8);
        var offsets = new int[objects.Count + 1];
        for (var index = 0; index < objects.Count; index++)
        {
            offsets[index + 1] = checked((int)output.Position);
            output.Write(Encoding.ASCII.GetBytes($"{index + 1} 0 obj\n"));
            output.Write(objects[index]);
            output.Write("\nendobj\n"u8);
        }

        var xrefOffset = output.Position;
        output.Write(Encoding.ASCII.GetBytes($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n"));
        for (var index = 1; index <= objects.Count; index++)
        {
            output.Write(Encoding.ASCII.GetBytes($"{offsets[index]:D10} 00000 n \n"));
        }

        output.Write(Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {objects.Count + 1} {trailerEntries} >>\nstartxref\n{xrefOffset}\n%%EOF\n"));
        var bytes = output.ToArray();
        return new TestDocument(bytes, new PdfCrossReferenceParser(bytes).Parse());
    }

    private static byte[] Compress(ReadOnlySpan<byte> input)
    {
        using var output = new MemoryStream();
        using (var compressor = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            compressor.Write(input);
        }

        return output.ToArray();
    }

    private static byte[] Pad(ReadOnlySpan<byte> password)
    {
        var result = new byte[32];
        var count = Math.Min(password.Length, 32);
        password[..count].CopyTo(result);
        Padding.AsSpan(0, 32 - count).CopyTo(result.AsSpan(count));
        return result;
    }

    private static byte[] Rc4(ReadOnlySpan<byte> key, ReadOnlySpan<byte> input)
    {
        var state = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        var j = 0;
        for (var i = 0; i < 256; i++)
        {
            j = (j + state[i] + key[i % key.Length]) & 255;
            (state[i], state[j]) = (state[j], state[i]);
        }

        var result = new byte[input.Length];
        var x = 0;
        j = 0;
        for (var index = 0; index < result.Length; index++)
        {
            x = (x + 1) & 255;
            j = (j + state[x]) & 255;
            (state[x], state[j]) = (state[j], state[x]);
            result[index] = (byte)(input[index] ^ state[(state[x] + state[j]) & 255]);
        }

        return result;
    }

    private static byte[] Xor(byte[] key, int value) => key.Select(item => (byte)(item ^ value)).ToArray();

    private static byte[] Combine(params byte[][] values) => values.SelectMany(value => value).ToArray();

    private static readonly byte[] Padding =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41,
        0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80,
        0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    private sealed record SecurityValues(byte[] FileKey, byte[] OwnerEntry, byte[] UserEntry);
    private sealed record TestDocument(
        byte[] Bytes,
        Rtpdf.Core.CrossReferences.PdfCrossReferenceTable Table);
}
