using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Filters;

/// <summary>
/// Decodes byte runs used by the PDF <c>/RunLengthDecode</c> filter.
/// </summary>
public sealed class RunLengthDecodeFilter : IPdfFilter
{
    public string Name => "RunLengthDecode";

    public byte[] Decode(
        ReadOnlyMemory<byte> encodedData,
        PdfDictionary? decodeParameters,
        int maximumDecodedLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDecodedLength);

        using var output = new MemoryStream();
        var input = encodedData.Span;
        var index = 0;
        while (index < input.Length)
        {
            var length = input[index++];
            if (length == 128)
            {
                return output.ToArray();
            }

            if (length <= 127)
            {
                var literalLength = length + 1;
                if (input.Length - index < literalLength)
                {
                    throw Error("RunLength literal run is truncated");
                }

                EnsureCapacity(output.Length, literalLength, maximumDecodedLength);
                output.Write(input.Slice(index, literalLength));
                index += literalLength;
                continue;
            }

            if (index >= input.Length)
            {
                throw Error("RunLength repeated run is missing its byte");
            }

            var repeatLength = 257 - length;
            EnsureCapacity(output.Length, repeatLength, maximumDecodedLength);
            var repeated = input[index++];
            for (var count = 0; count < repeatLength; count++)
            {
                output.WriteByte(repeated);
            }
        }

        throw Error("RunLength data is missing its end marker");
    }

    private void EnsureCapacity(long currentLength, int addedLength, int maximumDecodedLength)
    {
        if (currentLength + addedLength > maximumDecodedLength)
        {
            throw Error($"Decoded stream exceeds the configured limit of {maximumDecodedLength} bytes");
        }
    }

    private PdfFilterException Error(string message) => new(message, Name);
}
