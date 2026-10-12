namespace Rtpdf.Core.Objects;

/// <summary>
/// A PDF boolean object.
/// </summary>
public sealed class PdfBoolean : PdfObject, IEquatable<PdfBoolean>
{
    public PdfBoolean(bool value)
    {
        Value = value;
    }

    public static PdfBoolean True { get; } = new(true);

    public static PdfBoolean False { get; } = new(false);

    public bool Value { get; }

    public override PdfObjectType Type => PdfObjectType.Boolean;

    public static PdfBoolean FromValue(bool value) => value ? True : False;

    public bool Equals(PdfBoolean? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is PdfBoolean other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value ? "true" : "false";
}
