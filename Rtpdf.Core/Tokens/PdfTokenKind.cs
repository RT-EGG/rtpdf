namespace Rtpdf.Core.Tokens;

/// <summary>
/// The lexical categories recognized by <see cref="PdfLexer"/>.
/// </summary>
public enum PdfTokenKind
{
    EndOfInput,
    Null,
    Boolean,
    Integer,
    Real,
    Name,
    LiteralString,
    HexadecimalString,
    ArrayStart,
    ArrayEnd,
    DictionaryStart,
    DictionaryEnd,
    ProcedureStart,
    ProcedureEnd,
    Keyword,
}
