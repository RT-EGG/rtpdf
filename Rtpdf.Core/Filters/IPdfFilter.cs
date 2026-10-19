using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Filters;

/// <summary>
/// Decodes one filter stage from a PDF stream.
/// </summary>
public interface IPdfFilter
{
    string Name { get; }

    byte[] Decode(
        ReadOnlyMemory<byte> encodedData,
        PdfDictionary? decodeParameters,
        int maximumDecodedLength);
}
