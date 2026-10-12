namespace Rtpdf.Core.Tokens;

/// <summary>
/// Reads PDF lexical tokens from a byte sequence. The lexer owns a copy of its input,
/// so returned token memory remains valid for the lifetime of the token.
/// </summary>
public sealed class PdfLexer
{
    private readonly byte[] _input;
    private int _position;

    public PdfLexer(ReadOnlyMemory<byte> input)
    {
        _input = input.ToArray();
    }

    public PdfLexer(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);

        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        _input = buffer.ToArray();
    }

    public int Position => _position;

    public PdfToken Read()
    {
        SkipWhitespaceAndComments();

        if (_position >= _input.Length)
        {
            return new PdfToken(PdfTokenKind.EndOfInput, _position, ReadOnlyMemory<byte>.Empty);
        }

        return _input[_position] switch
        {
            (byte)'/' => ReadName(),
            (byte)'(' => ReadLiteralString(),
            (byte)'<' => Peek(1) == (byte)'<' ? ReadFixed(PdfTokenKind.DictionaryStart, 2) : ReadHexadecimalString(),
            (byte)'>' when Peek(1) == (byte)'>' => ReadFixed(PdfTokenKind.DictionaryEnd, 2),
            (byte)'>' => throw Error("Unexpected '>' delimiter", _position),
            (byte)'[' => ReadFixed(PdfTokenKind.ArrayStart, 1),
            (byte)']' => ReadFixed(PdfTokenKind.ArrayEnd, 1),
            (byte)'{' => ReadFixed(PdfTokenKind.ProcedureStart, 1),
            (byte)'}' => ReadFixed(PdfTokenKind.ProcedureEnd, 1),
            _ => ReadRegularToken(),
        };
    }

    private PdfToken ReadName()
    {
        var start = _position++;
        var value = new List<byte>();

        while (_position < _input.Length && !IsWhitespace(_input[_position]) && !IsDelimiter(_input[_position]))
        {
            var current = _input[_position++];
            if (current != (byte)'#')
            {
                value.Add(current);
                continue;
            }

            if (_position + 1 >= _input.Length ||
                !TryGetHexValue(_input[_position], out var high) ||
                !TryGetHexValue(_input[_position + 1], out var low))
            {
                throw Error("A PDF name escape must contain exactly two hexadecimal digits", _position - 1);
            }

            value.Add((byte)((high << 4) | low));
            _position += 2;
        }

        return CreateToken(PdfTokenKind.Name, start, value.ToArray());
    }

    private PdfToken ReadLiteralString()
    {
        var start = _position++;
        var nestingDepth = 1;
        var value = new List<byte>();

        while (_position < _input.Length)
        {
            var current = _input[_position++];
            switch (current)
            {
                case (byte)'(':
                    nestingDepth++;
                    value.Add(current);
                    break;

                case (byte)')':
                    nestingDepth--;
                    if (nestingDepth == 0)
                    {
                        return CreateToken(PdfTokenKind.LiteralString, start, value.ToArray());
                    }

                    value.Add(current);
                    break;

                case (byte)'\\':
                    ReadLiteralEscape(value);
                    break;

                case (byte)'\r':
                    if (Peek(0) == (byte)'\n')
                    {
                        _position++;
                    }

                    value.Add((byte)'\n');
                    break;

                case (byte)'\n':
                    value.Add((byte)'\n');
                    break;

                default:
                    value.Add(current);
                    break;
            }
        }

        throw Error("Unterminated literal string", start);
    }

    private void ReadLiteralEscape(List<byte> value)
    {
        if (_position >= _input.Length)
        {
            throw Error("Unterminated escape sequence in literal string", _position - 1);
        }

        var escaped = _input[_position++];
        switch (escaped)
        {
            case (byte)'n':
                value.Add((byte)'\n');
                return;
            case (byte)'r':
                value.Add((byte)'\r');
                return;
            case (byte)'t':
                value.Add((byte)'\t');
                return;
            case (byte)'b':
                value.Add((byte)'\b');
                return;
            case (byte)'f':
                value.Add((byte)'\f');
                return;
            case (byte)'(':
            case (byte)')':
            case (byte)'\\':
                value.Add(escaped);
                return;
            case (byte)'\n':
                return;
            case (byte)'\r':
                if (Peek(0) == (byte)'\n')
                {
                    _position++;
                }

                return;
        }

        if (!IsOctalDigit(escaped))
        {
            value.Add(escaped);
            return;
        }

        var octalValue = escaped - (byte)'0';
        var digitCount = 1;
        while (digitCount < 3 && IsOctalDigit(Peek(0)))
        {
            octalValue = (octalValue * 8) + (_input[_position++] - (byte)'0');
            digitCount++;
        }

        value.Add((byte)(octalValue & byte.MaxValue));
    }

    private PdfToken ReadHexadecimalString()
    {
        var start = _position++;
        var value = new List<byte>();
        int? highNibble = null;

        while (_position < _input.Length)
        {
            var currentOffset = _position;
            var current = _input[_position++];

            if (current == (byte)'>')
            {
                if (highNibble is not null)
                {
                    value.Add((byte)(highNibble.Value << 4));
                }

                return CreateToken(PdfTokenKind.HexadecimalString, start, value.ToArray());
            }

            if (IsWhitespace(current))
            {
                continue;
            }

            if (!TryGetHexValue(current, out var nibble))
            {
                throw Error("Invalid character in hexadecimal string", currentOffset);
            }

            if (highNibble is null)
            {
                highNibble = nibble;
            }
            else
            {
                value.Add((byte)((highNibble.Value << 4) | nibble));
                highNibble = null;
            }
        }

        throw Error("Unterminated hexadecimal string", start);
    }

    private PdfToken ReadRegularToken()
    {
        var start = _position;
        while (_position < _input.Length && !IsWhitespace(_input[_position]) && !IsDelimiter(_input[_position]))
        {
            _position++;
        }

        if (_position == start)
        {
            throw Error($"Unexpected delimiter '{(char)_input[_position]}'", _position);
        }

        var bytes = _input.AsSpan(start, _position - start);
        var kind = ClassifyRegularToken(bytes);
        return new PdfToken(kind, start, _input.AsMemory(start, _position - start));
    }

    private static PdfTokenKind ClassifyRegularToken(ReadOnlySpan<byte> bytes)
    {
        if (bytes.SequenceEqual("null"u8))
        {
            return PdfTokenKind.Null;
        }

        if (bytes.SequenceEqual("true"u8) || bytes.SequenceEqual("false"u8))
        {
            return PdfTokenKind.Boolean;
        }

        if (TryClassifyNumber(bytes, out var numberKind))
        {
            return numberKind;
        }

        return PdfTokenKind.Keyword;
    }

    private static bool TryClassifyNumber(ReadOnlySpan<byte> bytes, out PdfTokenKind kind)
    {
        kind = PdfTokenKind.Integer;
        var index = 0;
        if (bytes.Length > 0 && bytes[0] is (byte)'+' or (byte)'-')
        {
            index++;
        }

        var digitCount = 0;
        var hasDecimalPoint = false;
        for (; index < bytes.Length; index++)
        {
            if (bytes[index] is >= (byte)'0' and <= (byte)'9')
            {
                digitCount++;
                continue;
            }

            if (bytes[index] == (byte)'.' && !hasDecimalPoint)
            {
                hasDecimalPoint = true;
                kind = PdfTokenKind.Real;
                continue;
            }

            return false;
        }

        return digitCount > 0;
    }

    private PdfToken ReadFixed(PdfTokenKind kind, int length)
    {
        var start = _position;
        _position += length;
        return new PdfToken(kind, start, _input.AsMemory(start, length));
    }

    private PdfToken CreateToken(PdfTokenKind kind, int start, ReadOnlyMemory<byte> valueBytes) =>
        new(kind, start, _input.AsMemory(start, _position - start), valueBytes);

    private void SkipWhitespaceAndComments()
    {
        while (_position < _input.Length)
        {
            if (IsWhitespace(_input[_position]))
            {
                _position++;
                continue;
            }

            if (_input[_position] != (byte)'%')
            {
                return;
            }

            _position++;
            while (_position < _input.Length && _input[_position] is not (byte)'\r' and not (byte)'\n')
            {
                _position++;
            }
        }
    }

    private byte Peek(int relativeOffset)
    {
        var index = _position + relativeOffset;
        return index >= 0 && index < _input.Length ? _input[index] : byte.MaxValue;
    }

    private static bool IsWhitespace(byte value) => value is 0 or 9 or 10 or 12 or 13 or 32;

    private static bool IsDelimiter(byte value) => value is
        (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or
        (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

    private static bool IsOctalDigit(byte value) => value is >= (byte)'0' and <= (byte)'7';

    private static bool TryGetHexValue(byte value, out int result)
    {
        switch (value)
        {
            case >= (byte)'0' and <= (byte)'9':
                result = value - (byte)'0';
                return true;
            case >= (byte)'A' and <= (byte)'F':
                result = value - (byte)'A' + 10;
                return true;
            case >= (byte)'a' and <= (byte)'f':
                result = value - (byte)'a' + 10;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static PdfLexerException Error(string message, int offset) => new(message, offset);
}
