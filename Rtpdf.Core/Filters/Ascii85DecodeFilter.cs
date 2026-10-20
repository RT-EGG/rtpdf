using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Filters;

/// <summary>
/// Decodes base-85 text used by the PDF <c>/ASCII85Decode</c> filter.
/// </summary>
public sealed class Ascii85DecodeFilter : IPdfFilter
{
    public string Name => "ASCII85Decode";

    public byte[] Decode(
        ReadOnlyMemory<byte> encodedData,
        PdfDictionary? decodeParameters,
        int maximumDecodedLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDecodedLength);

        using var output = new MemoryStream();
        Span<byte> group = stackalloc byte[5];
        var groupLength = 0;
        var ended = false;
        var input = encodedData.Span;

        for (var index = 0; index < input.Length; index++)
        {
            var value = input[index];
            if (IsWhiteSpace(value))
            {
                continue;
            }

            if (ended)
            {
                throw Error("ASCII85 data contains non-whitespace bytes after its end marker");
            }

            if (value == (byte)'~')
            {
                if (index + 1 >= input.Length || input[index + 1] != (byte)'>')
                {
                    throw Error("ASCII85 end marker must be ~>");
                }

                index++;
                ended = true;
                continue;
            }

            if (value == (byte)'z')
            {
                if (groupLength != 0)
                {
                    throw Error("ASCII85 zero shorthand may appear only between groups");
                }

                Write(output, [0, 0, 0, 0], maximumDecodedLength);
                continue;
            }

            if (value is < (byte)'!' or > (byte)'u')
            {
                throw Error($"ASCII85 data contains invalid byte 0x{value:X2}");
            }

            group[groupLength++] = value;
            if (groupLength == 5)
            {
                DecodeGroup(group, 4, output, maximumDecodedLength);
                groupLength = 0;
            }
        }

        if (!ended)
        {
            throw Error("ASCII85 data is missing its ~> end marker");
        }

        if (groupLength == 1)
        {
            throw Error("ASCII85 final group must contain at least two characters");
        }

        if (groupLength > 1)
        {
            var outputLength = groupLength - 1;
            group[groupLength..].Fill((byte)'u');
            DecodeGroup(group, outputLength, output, maximumDecodedLength);
        }

        return output.ToArray();
    }

    private void DecodeGroup(
        ReadOnlySpan<byte> group,
        int outputLength,
        Stream output,
        int maximumDecodedLength)
    {
        ulong value = 0;
        foreach (var digit in group)
        {
            value = (value * 85) + (uint)(digit - '!');
        }

        if (value > uint.MaxValue)
        {
            throw Error("ASCII85 group exceeds the 32-bit range");
        }

        Span<byte> decoded = stackalloc byte[4]
        {
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value,
        };
        Write(output, decoded[..outputLength], maximumDecodedLength);
    }

    private void Write(Stream output, ReadOnlySpan<byte> bytes, int maximumDecodedLength)
    {
        if (output.Length + bytes.Length > maximumDecodedLength)
        {
            throw Error($"Decoded stream exceeds the configured limit of {maximumDecodedLength} bytes");
        }

        output.Write(bytes);
    }

    private static bool IsWhiteSpace(byte value) => value is 0 or 9 or 10 or 12 or 13 or 32;

    private PdfFilterException Error(string message) => new(message, Name);
}
