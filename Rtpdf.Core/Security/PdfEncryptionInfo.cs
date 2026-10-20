using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Security;

/// <summary>
/// Describes the encryption declared by a PDF without authenticating a password.
/// </summary>
public sealed record PdfEncryptionInfo(
    bool IsEncrypted,
    string? SecurityHandler,
    int? Version,
    int? Revision,
    int? KeyLengthBits,
    PdfObjectId? EncryptionObjectId)
{
    public static PdfEncryptionInfo Unencrypted { get; } = new(false, null, null, null, null, null);
}
