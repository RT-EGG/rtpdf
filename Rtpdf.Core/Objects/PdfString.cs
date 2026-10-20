using System.Text;
using Rtpdf.Core.Text;

namespace Rtpdf.Core.Objects;

/// <summary>
/// A PDF string object that retains its original bytes and representation kind.
/// </summary>
public sealed class PdfString : PdfObject, IEquatable<PdfString>
{
    private readonly byte[] _bytes;

    public PdfString(ReadOnlySpan<byte> bytes, PdfStringKind kind = PdfStringKind.Literal)
    {
        _bytes = bytes.ToArray();
        Kind = kind;
    }

    public ReadOnlyMemory<byte> Bytes => _bytes;

    public PdfStringKind Kind { get; }

    public override PdfObjectType Type => PdfObjectType.String;

    /// <summary>
    /// Decodes a byte-order-marked UTF-16 string. Other strings use PDFDocEncoding.
    /// Undefined PDFDocEncoding bytes are represented by U+FFFD while the original
    /// bytes remain available through <see cref="Bytes"/>.
    /// </summary>
    public string GetText()
    {
        if (_bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
        {
            return Encoding.BigEndianUnicode.GetString(_bytes.AsSpan(2));
        }

        if (_bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]))
        {
            return Encoding.Unicode.GetString(_bytes.AsSpan(2));
        }

        return PdfDocEncoding.Decode(_bytes);
    }

    public bool Equals(PdfString? other) =>
        other is not null && Kind == other.Kind && _bytes.AsSpan().SequenceEqual(other._bytes);

    public override bool Equals(object? obj) => obj is PdfString other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        foreach (var value in _bytes)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => Kind == PdfStringKind.Hexadecimal
        ? $"<{Convert.ToHexString(_bytes)}>"
        : $"({EscapeLiteralString(_bytes)})";

    private static string EscapeLiteralString(ReadOnlySpan<byte> bytes)
    {
        var result = new StringBuilder(bytes.Length);
        foreach (var value in bytes)
        {
            switch (value)
            {
                case (byte)'(':
                case (byte)')':
                case (byte)'\\':
                    result.Append('\\').Append((char)value);
                    break;
                case (byte)'\n':
                    result.Append("\\n");
                    break;
                case (byte)'\r':
                    result.Append("\\r");
                    break;
                case (byte)'\t':
                    result.Append("\\t");
                    break;
                case (byte)'\b':
                    result.Append("\\b");
                    break;
                case (byte)'\f':
                    result.Append("\\f");
                    break;
                default:
                    result.Append((char)value);
                    break;
            }
        }

        return result.ToString();
    }
}
