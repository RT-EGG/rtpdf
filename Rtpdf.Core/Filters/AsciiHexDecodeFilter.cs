using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Filters;

/// <summary>
/// Decodes hexadecimal text used by the PDF <c>/ASCIIHexDecode</c> filter.
/// </summary>
public sealed class AsciiHexDecodeFilter : IPdfFilter
{
    public string Name => "ASCIIHexDecode";

    public byte[] Decode(
        ReadOnlyMemory<byte> encodedData,
        PdfDictionary? decodeParameters,
        int maximumDecodedLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDecodedLength);

        using var output = new MemoryStream();
        int? highNibble = null;
        var ended = false;
        foreach (var value in encodedData.Span)
        {
            if (IsWhiteSpace(value))
            {
                continue;
            }

            if (ended)
            {
                throw Error("ASCII hexadecimal data contains non-whitespace bytes after its end marker");
            }

            if (value == (byte)'>')
            {
                ended = true;
                continue;
            }

            var nibble = DecodeNibble(value);
            if (nibble < 0)
            {
                throw Error($"ASCII hexadecimal data contains invalid byte 0x{value:X2}");
            }

            if (highNibble is null)
            {
                highNibble = nibble;
            }
            else
            {
                WriteByte(output, (byte)((highNibble.Value << 4) | nibble), maximumDecodedLength);
                highNibble = null;
            }
        }

        if (highNibble is not null)
        {
            WriteByte(output, (byte)(highNibble.Value << 4), maximumDecodedLength);
        }

        return output.ToArray();
    }

    private void WriteByte(Stream output, byte value, int maximumDecodedLength)
    {
        if (output.Length >= maximumDecodedLength)
        {
            throw Error($"Decoded stream exceeds the configured limit of {maximumDecodedLength} bytes");
        }

        output.WriteByte(value);
    }

    private static int DecodeNibble(byte value) => value switch
    {
        >= (byte)'0' and <= (byte)'9' => value - '0',
        >= (byte)'A' and <= (byte)'F' => value - 'A' + 10,
        >= (byte)'a' and <= (byte)'f' => value - 'a' + 10,
        _ => -1,
    };

    private static bool IsWhiteSpace(byte value) => value is 0 or 9 or 10 or 12 or 13 or 32;

    private PdfFilterException Error(string message) => new(message, Name);
}
