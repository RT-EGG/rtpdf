using System.Text;

namespace Rtpdf.Core.Objects;

/// <summary>
/// A PDF name object. The stored bytes are the decoded name bytes, after processing
/// any lexical <c>#xx</c> escapes.
/// </summary>
public sealed class PdfName : PdfObject, IEquatable<PdfName>
{
    private readonly byte[] _bytes;

    public PdfName(ReadOnlySpan<byte> bytes)
    {
        _bytes = bytes.ToArray();
    }

    public PdfName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Any(character => character > byte.MaxValue))
        {
            throw new ArgumentException("A PDF name string may contain only single-byte characters.", nameof(value));
        }

        _bytes = Encoding.Latin1.GetBytes(value);
    }

    public ReadOnlyMemory<byte> Bytes => _bytes;

    public string Value => Encoding.Latin1.GetString(_bytes);

    public override PdfObjectType Type => PdfObjectType.Name;

    public bool Equals(PdfName? other) => other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);

    public override bool Equals(object? obj) => obj is PdfName other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var value in _bytes)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    public override string ToString()
    {
        var result = new StringBuilder(_bytes.Length + 1).Append('/');
        foreach (var value in _bytes)
        {
            if (value is >= 33 and <= 126 && !IsDelimiter(value) && value != (byte)'#')
            {
                result.Append((char)value);
            }
            else
            {
                result.Append('#').Append(value.ToString("X2"));
            }
        }

        return result.ToString();
    }

    private static bool IsDelimiter(byte value) => value is
        (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or
        (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';
}
