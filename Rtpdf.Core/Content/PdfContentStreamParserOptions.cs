using Rtpdf.Core.Parsing;

namespace Rtpdf.Core.Content;

/// <summary>
/// Resource limits applied while parsing a decoded PDF content stream.
/// </summary>
public sealed class PdfContentStreamParserOptions
{
    public int MaximumOperations { get; init; } = 1_000_000;

    public int MaximumOperandsPerOperation { get; init; } = 100_000;

    public int MaximumOperatorLength { get; init; } = 127;

    public PdfObjectParserOptions ObjectParserOptions { get; init; } = new();

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumOperations);
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumOperandsPerOperation);
        ArgumentOutOfRangeException.ThrowIfNegative(MaximumOperatorLength);
        ArgumentNullException.ThrowIfNull(ObjectParserOptions);
        ObjectParserOptions.Validate();
    }
}
