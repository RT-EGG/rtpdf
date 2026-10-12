using System.Globalization;

namespace Rtpdf.Core.Objects;

/// <summary>
/// A PDF real number object. Decimal storage avoids binary floating-point artifacts.
/// </summary>
public sealed class PdfReal : PdfNumber, IEquatable<PdfReal>
{
    public PdfReal(decimal value)
    {
        Value = value;
    }

    public decimal Value { get; }

    public override decimal DecimalValue => Value;

    public override PdfObjectType Type => PdfObjectType.Real;

    public bool Equals(PdfReal? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is PdfReal other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
