using System.Text;
using Rtpdf.Core.Tokens;
using Xunit;

namespace Rtpdf.Core.Tests.Tokens;

public sealed class PdfLexerTests
{
    [Fact]
    public void ReadSkipsEveryPdfWhitespaceAndComments()
    {
        var lexer = CreateLexer("\0\t\n\f\r % a comment\r\n true %last comment");

        var token = lexer.Read();

        Assert.Equal(PdfTokenKind.Boolean, token.Kind);
        Assert.Equal("true", token.GetAsciiValue());
        Assert.Equal(PdfTokenKind.EndOfInput, lexer.Read().Kind);
    }

    [Fact]
    public void ReadRecognizesObjectAndStructuralTokens()
    {
        var lexer = CreateLexer("null true false -12 +3.5 .25 6. /Name [ ] << >> { } obj");

        Assert.Collection(
            ReadAll(lexer),
            token => AssertToken(token, PdfTokenKind.Null, "null"),
            token => AssertToken(token, PdfTokenKind.Boolean, "true"),
            token => AssertToken(token, PdfTokenKind.Boolean, "false"),
            token => AssertToken(token, PdfTokenKind.Integer, "-12"),
            token => AssertToken(token, PdfTokenKind.Real, "+3.5"),
            token => AssertToken(token, PdfTokenKind.Real, ".25"),
            token => AssertToken(token, PdfTokenKind.Real, "6."),
            token => AssertToken(token, PdfTokenKind.Name, "Name"),
            token => Assert.Equal(PdfTokenKind.ArrayStart, token.Kind),
            token => Assert.Equal(PdfTokenKind.ArrayEnd, token.Kind),
            token => Assert.Equal(PdfTokenKind.DictionaryStart, token.Kind),
            token => Assert.Equal(PdfTokenKind.DictionaryEnd, token.Kind),
            token => Assert.Equal(PdfTokenKind.ProcedureStart, token.Kind),
            token => Assert.Equal(PdfTokenKind.ProcedureEnd, token.Kind),
            token => AssertToken(token, PdfTokenKind.Keyword, "obj"));
    }

    [Fact]
    public void ReadNameDecodesHexEscapesAndRetainsRawBytes()
    {
        var lexer = CreateLexer("/A#20B#23C / /Tail[1]");

        var escaped = lexer.Read();
        var empty = lexer.Read();
        var beforeDelimiter = lexer.Read();

        Assert.Equal("/A#20B#23C", Encoding.ASCII.GetString(escaped.RawBytes.Span));
        Assert.Equal("A B#C", Encoding.Latin1.GetString(escaped.ValueBytes.Span));
        Assert.Empty(empty.ValueBytes.ToArray());
        Assert.Equal("Tail", beforeDelimiter.GetAsciiValue());
        Assert.Equal(PdfTokenKind.ArrayStart, lexer.Read().Kind);
    }

    [Theory]
    [InlineData("/Bad#")]
    [InlineData("/Bad#0")]
    [InlineData("/Bad#XZ")]
    public void ReadNameRejectsMalformedHexEscape(string input)
    {
        var exception = Assert.Throws<PdfLexerException>(() => CreateLexer(input).Read());

        Assert.Contains("two hexadecimal digits", exception.Message);
    }

    [Fact]
    public void ReadLiteralStringHandlesNestingEscapesOctalAndLineContinuation()
    {
        var lexer = CreateLexer("(outer (inner) \\(x\\) \\053 \\n \\r \\t \\b \\f \\\\ a\\\r\ncontinued)");

        var token = lexer.Read();

        Assert.Equal(PdfTokenKind.LiteralString, token.Kind);
        Assert.Equal(
            "outer (inner) (x) + \n \r \t \b \f \\ acontinued",
            Encoding.Latin1.GetString(token.ValueBytes.Span));
    }

    [Fact]
    public void ReadLiteralStringNormalizesUnescapedLineEndings()
    {
        var lexer = CreateLexer("(a\rb\r\nc\nd)");

        Assert.Equal("a\nb\nc\nd", Encoding.Latin1.GetString(lexer.Read().ValueBytes.Span));
    }

    [Fact]
    public void ReadLiteralStringRejectsMissingClosingParenthesis()
    {
        var exception = Assert.Throws<PdfLexerException>(() => CreateLexer("(unfinished").Read());

        Assert.Equal(0, exception.Offset);
    }

    [Fact]
    public void ReadHexadecimalStringIgnoresWhitespaceAndPadsOddNibble()
    {
        var lexer = CreateLexer("<48 65\n6C6c 6F2>");

        var token = lexer.Read();

        Assert.Equal(PdfTokenKind.HexadecimalString, token.Kind);
        Assert.Equal(new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20 }, token.ValueBytes.ToArray());
    }

    [Theory]
    [InlineData("<0G>", 2)]
    [InlineData("<123", 0)]
    [InlineData(">", 0)]
    public void ReadRejectsMalformedHexadecimalSyntax(string input, int expectedOffset)
    {
        var exception = Assert.Throws<PdfLexerException>(() => CreateLexer(input).Read());

        Assert.Equal(expectedOffset, exception.Offset);
    }

    [Theory]
    [InlineData("+", PdfTokenKind.Keyword)]
    [InlineData(".", PdfTokenKind.Keyword)]
    [InlineData("1.2.3", PdfTokenKind.Keyword)]
    [InlineData("12abc", PdfTokenKind.Keyword)]
    [InlineData("123456789012345678901234567890", PdfTokenKind.Integer)]
    public void NumberClassificationRequiresTheEntireRegularToken(string input, PdfTokenKind expectedKind)
    {
        Assert.Equal(expectedKind, CreateLexer(input).Read().Kind);
    }

    [Fact]
    public void TokenReportsExactOffsetLengthAndRawBytes()
    {
        var lexer = CreateLexer("  /A#20B ");

        var token = lexer.Read();

        Assert.Equal(2, token.Offset);
        Assert.Equal(6, token.Length);
        Assert.Equal("/A#20B", Encoding.ASCII.GetString(token.RawBytes.Span));
        Assert.Equal(9, lexer.Read().Offset);
    }

    [Fact]
    public void LexerCopiesMemoryInput()
    {
        byte[] input = "true"u8.ToArray();
        var lexer = new PdfLexer(input);
        input[0] = (byte)'f';

        Assert.Equal("true", lexer.Read().GetAsciiValue());
    }

    [Fact]
    public void StreamConstructorReadsFromCurrentPositionWithoutTakingOwnership()
    {
        using var stream = new MemoryStream("xx false"u8.ToArray());
        stream.Position = 3;
        var lexer = new PdfLexer(stream);

        Assert.Equal("false", lexer.Read().GetAsciiValue());
        Assert.True(stream.CanRead);
    }

    private static PdfLexer CreateLexer(string input) => new(Encoding.Latin1.GetBytes(input));

    private static List<PdfToken> ReadAll(PdfLexer lexer)
    {
        var tokens = new List<PdfToken>();
        PdfToken token;
        while ((token = lexer.Read()).Kind != PdfTokenKind.EndOfInput)
        {
            tokens.Add(token);
        }

        return tokens;
    }

    private static void AssertToken(PdfToken token, PdfTokenKind expectedKind, string expectedValue)
    {
        Assert.Equal(expectedKind, token.Kind);
        Assert.Equal(expectedValue, token.GetAsciiValue());
    }
}
