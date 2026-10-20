using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Filters;

internal sealed class PdfPredictor
{
    private static readonly HashSet<int> SupportedBitsPerComponent = [1, 2, 4, 8, 16];

    private readonly string _filterName;
    private readonly int _predictor;
    private readonly int _colors;
    private readonly int _bitsPerComponent;
    private readonly int _columns;
    private readonly int _bytesPerPixel;
    private readonly int _rowBytes;

    private PdfPredictor(
        string filterName,
        int predictor,
        int colors,
        int bitsPerComponent,
        int columns,
        int bytesPerPixel,
        int rowBytes)
    {
        _filterName = filterName;
        _predictor = predictor;
        _colors = colors;
        _bitsPerComponent = bitsPerComponent;
        _columns = columns;
        _bytesPerPixel = bytesPerPixel;
        _rowBytes = rowBytes;
    }

    public static PdfPredictor Create(PdfDictionary? parameters, string filterName)
    {
        var predictor = ReadInteger(parameters, "Predictor", defaultValue: 1, filterName);
        if (predictor == 1)
        {
            return new PdfPredictor(filterName, predictor, 1, 8, 1, 1, 1);
        }

        if (predictor != 2 && predictor is not (>= 10 and <= 15))
        {
            throw Error($"Predictor {predictor} is not supported", filterName);
        }

        var colors = ReadPositiveInteger(parameters, "Colors", defaultValue: 1, filterName);
        var bitsPerComponent = ReadPositiveInteger(
            parameters,
            "BitsPerComponent",
            defaultValue: 8,
            filterName);
        if (!SupportedBitsPerComponent.Contains(bitsPerComponent))
        {
            throw Error("/BitsPerComponent must be 1, 2, 4, 8, or 16", filterName);
        }

        var columns = ReadPositiveInteger(parameters, "Columns", defaultValue: 1, filterName);
        long bitsPerPixel;
        long bitsPerRow;
        try
        {
            bitsPerPixel = checked((long)colors * bitsPerComponent);
            bitsPerRow = checked(bitsPerPixel * columns);
        }
        catch (OverflowException exception)
        {
            throw new PdfFilterException("Predictor dimensions are too large", filterName, exception);
        }

        var bytesPerPixel = DivideRoundUp(bitsPerPixel, 8, "pixel size", filterName);
        var rowBytes = DivideRoundUp(bitsPerRow, 8, "row size", filterName);
        if (predictor >= 10 && rowBytes == int.MaxValue)
        {
            throw Error("Predictor row size is too large", filterName);
        }

        if (predictor == 2 && bitsPerComponent != 8 && (long)colors * columns > int.MaxValue)
        {
            throw Error("Predictor sample count is too large", filterName);
        }

        return new PdfPredictor(
            filterName,
            predictor,
            colors,
            bitsPerComponent,
            columns,
            bytesPerPixel,
            rowBytes);
    }

    public int GetMaximumInflatedLength(int maximumDecodedLength)
    {
        if (_predictor < 10 || maximumDecodedLength == 0)
        {
            return maximumDecodedLength;
        }

        var rows = maximumDecodedLength / _rowBytes;
        var maximum = (long)rows * (_rowBytes + 1L);
        return (int)Math.Min(maximum, int.MaxValue);
    }

    public byte[] Decode(byte[] inflatedData, int maximumDecodedLength)
    {
        var result = _predictor switch
        {
            1 => inflatedData,
            2 => DecodeTiff(inflatedData),
            _ => DecodePng(inflatedData),
        };

        if (result.Length > maximumDecodedLength)
        {
            throw Error(
                $"Decoded stream exceeds the configured limit of {maximumDecodedLength} bytes",
                _filterName);
        }

        return result;
    }

    private byte[] DecodeTiff(byte[] input)
    {
        EnsureCompleteRows(input.Length, _rowBytes);
        var output = input;
        if (_bitsPerComponent == 8)
        {
            for (var rowStart = 0; rowStart < output.Length; rowStart += _rowBytes)
            {
                for (var index = _colors; index < _rowBytes; index++)
                {
                    output[rowStart + index] = unchecked(
                        (byte)(output[rowStart + index] + output[rowStart + index - _colors]));
                }
            }

            return output;
        }

        var samplesPerRow = checked(_colors * _columns);
        var sampleMask = (1 << _bitsPerComponent) - 1;
        for (var rowStart = 0; rowStart < output.Length; rowStart += _rowBytes)
        {
            for (var sample = _colors; sample < samplesPerRow; sample++)
            {
                var encoded = ReadBits(output, rowStart, sample, _bitsPerComponent);
                var left = ReadBits(output, rowStart, sample - _colors, _bitsPerComponent);
                WriteBits(
                    output,
                    rowStart,
                    sample,
                    _bitsPerComponent,
                    (encoded + left) & sampleMask);
            }
        }

        return output;
    }

    private byte[] DecodePng(byte[] input)
    {
        var encodedRowBytes = checked(_rowBytes + 1);
        EnsureCompleteRows(input.Length, encodedRowBytes);
        var rowCount = input.Length / encodedRowBytes;
        var output = new byte[checked(rowCount * _rowBytes)];

        for (var row = 0; row < rowCount; row++)
        {
            var inputStart = row * encodedRowBytes;
            var outputStart = row * _rowBytes;
            var declaredFilter = input[inputStart];
            if (declaredFilter > 4)
            {
                throw Error($"PNG predictor row uses invalid filter {declaredFilter}", _filterName);
            }

            var filter = _predictor == 15 ? declaredFilter : _predictor - 10;
            if (_predictor != 15 && declaredFilter != filter)
            {
                throw Error(
                    $"PNG predictor row filter {declaredFilter} does not match /Predictor {_predictor}",
                    _filterName);
            }

            for (var index = 0; index < _rowBytes; index++)
            {
                var encoded = input[inputStart + 1 + index];
                var left = index >= _bytesPerPixel ? output[outputStart + index - _bytesPerPixel] : 0;
                var upper = row > 0 ? output[outputStart - _rowBytes + index] : 0;
                var upperLeft = row > 0 && index >= _bytesPerPixel
                    ? output[outputStart - _rowBytes + index - _bytesPerPixel]
                    : 0;
                output[outputStart + index] = filter switch
                {
                    0 => encoded,
                    1 => unchecked((byte)(encoded + left)),
                    2 => unchecked((byte)(encoded + upper)),
                    3 => unchecked((byte)(encoded + ((left + upper) / 2))),
                    4 => unchecked((byte)(encoded + Paeth(left, upper, upperLeft))),
                    _ => throw Error($"PNG predictor {filter + 10} is invalid", _filterName),
                };
            }
        }

        return output;
    }

    private void EnsureCompleteRows(int length, int encodedRowBytes)
    {
        if (length % encodedRowBytes != 0)
        {
            throw Error("Predictor data ends in the middle of a row", _filterName);
        }
    }

    private static int ReadBits(byte[] data, int rowStart, int sampleIndex, int bitsPerComponent)
    {
        var bitOffset = (long)sampleIndex * bitsPerComponent;
        var value = 0;
        for (var bit = 0; bit < bitsPerComponent; bit++)
        {
            var absoluteBit = bitOffset + bit;
            var source = data[rowStart + (int)(absoluteBit / 8)];
            value = (value << 1) | ((source >> (7 - (int)(absoluteBit % 8))) & 1);
        }

        return value;
    }

    private static void WriteBits(
        byte[] data,
        int rowStart,
        int sampleIndex,
        int bitsPerComponent,
        int value)
    {
        var bitOffset = (long)sampleIndex * bitsPerComponent;
        for (var bit = 0; bit < bitsPerComponent; bit++)
        {
            var absoluteBit = bitOffset + bit;
            var byteIndex = rowStart + (int)(absoluteBit / 8);
            var mask = (byte)(1 << (7 - (int)(absoluteBit % 8)));
            if ((value & (1 << (bitsPerComponent - bit - 1))) != 0)
            {
                data[byteIndex] |= mask;
            }
            else
            {
                data[byteIndex] &= unchecked((byte)~mask);
            }
        }
    }

    private static int Paeth(int left, int upper, int upperLeft)
    {
        var estimate = left + upper - upperLeft;
        var leftDistance = Math.Abs(estimate - left);
        var upperDistance = Math.Abs(estimate - upper);
        var upperLeftDistance = Math.Abs(estimate - upperLeft);
        if (leftDistance <= upperDistance && leftDistance <= upperLeftDistance)
        {
            return left;
        }

        return upperDistance <= upperLeftDistance ? upper : upperLeft;
    }

    private static int ReadPositiveInteger(
        PdfDictionary? parameters,
        string key,
        int defaultValue,
        string filterName)
    {
        var value = ReadInteger(parameters, key, defaultValue, filterName);
        if (value <= 0)
        {
            throw Error($"/{key} must be a positive integer", filterName);
        }

        return value;
    }

    private static int ReadInteger(
        PdfDictionary? parameters,
        string key,
        int defaultValue,
        string filterName)
    {
        if (parameters is null || !parameters.TryGetValue(key, out var rawValue))
        {
            return defaultValue;
        }

        if (rawValue is not PdfInteger integer || integer.Value is < int.MinValue or > int.MaxValue)
        {
            throw Error($"/{key} must be a 32-bit integer", filterName);
        }

        return (int)integer.Value;
    }

    private static int DivideRoundUp(
        long value,
        int divisor,
        string description,
        string filterName)
    {
        var result = (value + divisor - 1) / divisor;
        if (result > int.MaxValue)
        {
            throw Error($"Predictor {description} is too large", filterName);
        }

        return (int)result;
    }

    private static PdfFilterException Error(string message, string filterName) =>
        new(message, filterName);
}
