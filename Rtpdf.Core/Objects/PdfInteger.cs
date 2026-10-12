using System.Globalization;

namespace Rtpdf.Core.Objects;

/// <summary>
/// A PDF integer object.
/// </summary>
public sealed class PdfInteger : PdfNumber, IEquatable<PdfInteger>
{
    public PdfInteger(long value)
    {
        Value = value;
    }

    public long Value { get; }

    public override decimal DecimalValue => Value;

    public override PdfObjectType Type => PdfObjectType.Integer;

    public bool Equals(PdfInteger? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is PdfInteger other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
