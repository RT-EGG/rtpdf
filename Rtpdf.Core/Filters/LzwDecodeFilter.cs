using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Filters;

/// <summary>
/// Decodes LZW data used by the PDF <c>/LZWDecode</c> filter.
/// </summary>
public sealed class LzwDecodeFilter : IPdfFilter
{
    private const int ClearTableCode = 256;
    private const int EndOfDataCode = 257;
    private const int FirstDictionaryCode = 258;
    private const int MaximumCode = 4095;

    public string Name => "LZWDecode";

    public byte[] Decode(
        ReadOnlyMemory<byte> encodedData,
        PdfDictionary? decodeParameters,
        int maximumDecodedLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDecodedLength);

        var earlyChange = ReadEarlyChange(decodeParameters);
        var predictor = PdfPredictor.Create(decodeParameters, Name);
        var maximumLzwLength = predictor.GetMaximumInflatedLength(maximumDecodedLength);
        var reader = new BitReader(encodedData);
        var prefixes = new ushort[MaximumCode + 1];
        var suffixes = new byte[MaximumCode + 1];
        Span<byte> decodeStack = stackalloc byte[MaximumCode + 1];
        using var output = new MemoryStream();

        var nextCode = FirstDictionaryCode;
        var codeSize = 9;
        var previousCode = -1;
        var hasInitialClearCode = false;
        while (reader.TryRead(codeSize, out var code))
        {
            if (!hasInitialClearCode && code != ClearTableCode)
            {
                throw Error("LZW data must begin with a clear-table code");
            }

            if (code == ClearTableCode)
            {
                hasInitialClearCode = true;
                nextCode = FirstDictionaryCode;
                codeSize = 9;
                previousCode = -1;
                continue;
            }

            if (code == EndOfDataCode)
            {
                return predictor.Decode(output.ToArray(), maximumDecodedLength);
            }

            if (nextCode > MaximumCode && previousCode >= 0)
            {
                throw Error("LZW dictionary is full but was not followed by a clear-table code");
            }

            byte firstByte;
            if (code < nextCode)
            {
                firstByte = WriteEntry(
                    code,
                    prefixes,
                    suffixes,
                    decodeStack,
                    output,
                    maximumLzwLength);
            }
            else if (code == nextCode && previousCode >= 0)
            {
                firstByte = GetFirstByte(previousCode, prefixes, suffixes);
                WriteEntry(
                    previousCode,
                    prefixes,
                    suffixes,
                    decodeStack,
                    output,
                    maximumLzwLength);
                WriteByte(output, firstByte, maximumLzwLength);
            }
            else
            {
                throw Error($"LZW code {code} is not defined");
            }

            if (previousCode >= 0 && nextCode <= MaximumCode)
            {
                prefixes[nextCode] = (ushort)previousCode;
                suffixes[nextCode] = firstByte;
                nextCode++;
                if (codeSize < 12 && nextCode == (1 << codeSize) - earlyChange)
                {
                    codeSize++;
                }
            }

            previousCode = code;
        }

        throw Error("LZW data is truncated or missing its end-of-data code");
    }

    private byte WriteEntry(
        int code,
        ushort[] prefixes,
        byte[] suffixes,
        Span<byte> stack,
        Stream output,
        int maximumDecodedLength)
    {
        var length = 0;
        var current = code;
        while (current >= 256)
        {
            if (current > MaximumCode || length >= stack.Length)
            {
                throw Error("LZW dictionary contains an invalid chain");
            }

            stack[length++] = suffixes[current];
            current = prefixes[current];
        }

        var firstByte = (byte)current;
        WriteByte(output, firstByte, maximumDecodedLength);
        for (var index = length - 1; index >= 0; index--)
        {
            WriteByte(output, stack[index], maximumDecodedLength);
        }

        return firstByte;
    }

    private byte GetFirstByte(int code, ushort[] prefixes, byte[] suffixes)
    {
        var current = code;
        var depth = 0;
        while (current >= 256)
        {
            if (current > MaximumCode || depth++ > MaximumCode)
            {
                throw Error("LZW dictionary contains an invalid chain");
            }

            current = prefixes[current];
        }

        return (byte)current;
    }

    private void WriteByte(Stream output, byte value, int maximumDecodedLength)
    {
        if (output.Length >= maximumDecodedLength)
        {
            throw Error($"Decoded stream exceeds the configured limit of {maximumDecodedLength} bytes");
        }

        output.WriteByte(value);
    }

    private int ReadEarlyChange(PdfDictionary? parameters)
    {
        if (parameters is null || !parameters.TryGetValue("EarlyChange", out var rawValue))
        {
            return 1;
        }

        if (rawValue is not PdfInteger integer || integer.Value is not (0 or 1))
        {
            throw Error("/EarlyChange must be the integer 0 or 1");
        }

        return (int)integer.Value;
    }

    private PdfFilterException Error(string message) => new(message, Name);

    private ref struct BitReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _bitOffset;

        public BitReader(ReadOnlyMemory<byte> data)
        {
            _data = data.Span;
        }

        public bool TryRead(int bitCount, out int value)
        {
            if ((long)_bitOffset + bitCount > (long)_data.Length * 8)
            {
                value = 0;
                return false;
            }

            value = 0;
            for (var bit = 0; bit < bitCount; bit++)
            {
                var absoluteBit = _bitOffset++;
                value = (value << 1) |
                    ((_data[absoluteBit / 8] >> (7 - (absoluteBit % 8))) & 1);
            }

            return true;
        }
    }
}
