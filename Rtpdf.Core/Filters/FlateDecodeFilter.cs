using System.IO.Compression;
using Rtpdf.Core.Diagnostics;
using Rtpdf.Core.Objects;

namespace Rtpdf.Core.Filters;

/// <summary>
/// Decodes zlib-wrapped DEFLATE data used by the PDF <c>/FlateDecode</c> filter.
/// </summary>
public sealed class FlateDecodeFilter : IPdfFilter
{
    public string Name => "FlateDecode";

    public byte[] Decode(
        ReadOnlyMemory<byte> encodedData,
        PdfDictionary? decodeParameters,
        int maximumDecodedLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumDecodedLength);
        ValidateDecodeParameters(decodeParameters);

        try
        {
            using var input = new MemoryStream(encodedData.ToArray(), writable: false);
            using var inflater = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[81920];

            while (true)
            {
                var read = inflater.Read(buffer);
                if (read == 0)
                {
                    return output.ToArray();
                }

                if (output.Length + read > maximumDecodedLength)
                {
                    throw new PdfFilterException(
                        $"Decoded stream exceeds the configured limit of {maximumDecodedLength} bytes",
                        Name);
                }

                output.Write(buffer, 0, read);
            }
        }
        catch (PdfFilterException)
        {
            throw;
        }
        catch (InvalidDataException exception)
        {
            throw new PdfFilterException("Flate data is invalid", Name, exception);
        }
    }

    private void ValidateDecodeParameters(PdfDictionary? decodeParameters)
    {
        if (decodeParameters is null || !decodeParameters.TryGetValue("Predictor", out var predictor))
        {
            return;
        }

        if (predictor is not PdfInteger integer)
        {
            throw new PdfFilterException("/Predictor must be an integer", Name);
        }

        if (integer.Value != 1)
        {
            throw new PdfFilterException(
                $"Predictor {integer.Value} is not supported yet",
                Name);
        }
    }
}
