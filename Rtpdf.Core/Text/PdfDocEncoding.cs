using System.Diagnostics.CodeAnalysis;

namespace Rtpdf.Core.Text;

/// <summary>
/// Decodes the single-byte PDFDocEncoding used by PDF text strings without a Unicode BOM.
/// </summary>
public static class PdfDocEncoding
{
    private const char ReplacementCharacter = '\uFFFD';
    private static readonly char[] Characters = CreateCharacterTable();

    /// <summary>
    /// Decodes PDFDocEncoding, replacing the undefined byte values 0x7F and 0x9F
    /// with the Unicode replacement character.
    /// </summary>
    public static string Decode(ReadOnlySpan<byte> source)
    {
        if (source.IsEmpty)
        {
            return string.Empty;
        }

        return string.Create(source.Length, source.ToArray(), static (destination, bytes) =>
        {
            for (var index = 0; index < bytes.Length; index++)
            {
                destination[index] = Characters[bytes[index]];
            }
        });
    }

    /// <summary>
    /// Attempts strict decoding. Undefined byte values cause this method to return false.
    /// </summary>
    public static bool TryDecode(
        ReadOnlySpan<byte> source,
        [NotNullWhen(true)] out string? value)
    {
        foreach (var item in source)
        {
            if (item is 0x7F or 0x9F)
            {
                value = null;
                return false;
            }
        }

        value = Decode(source);
        return true;
    }

    private static char[] CreateCharacterTable()
    {
        var characters = new char[256];
        for (var value = 0; value < characters.Length; value++)
        {
            characters[value] = (char)value;
        }

        characters[0x18] = '\u02D8';
        characters[0x19] = '\u02C7';
        characters[0x1A] = '\u02C6';
        characters[0x1B] = '\u02D9';
        characters[0x1C] = '\u02DD';
        characters[0x1D] = '\u02DB';
        characters[0x1E] = '\u02DA';
        characters[0x1F] = '\u02DC';
        characters[0x7F] = ReplacementCharacter;
        characters[0x80] = '\u2022';
        characters[0x81] = '\u2020';
        characters[0x82] = '\u2021';
        characters[0x83] = '\u2026';
        characters[0x84] = '\u2014';
        characters[0x85] = '\u2013';
        characters[0x86] = '\u0192';
        characters[0x87] = '\u2044';
        characters[0x88] = '\u2039';
        characters[0x89] = '\u203A';
        characters[0x8A] = '\u2212';
        characters[0x8B] = '\u2030';
        characters[0x8C] = '\u201E';
        characters[0x8D] = '\u201C';
        characters[0x8E] = '\u201D';
        characters[0x8F] = '\u2018';
        characters[0x90] = '\u2019';
        characters[0x91] = '\u201A';
        characters[0x92] = '\u2122';
        characters[0x93] = '\uFB01';
        characters[0x94] = '\uFB02';
        characters[0x95] = '\u0141';
        characters[0x96] = '\u0152';
        characters[0x97] = '\u0160';
        characters[0x98] = '\u0178';
        characters[0x99] = '\u017D';
        characters[0x9A] = '\u0131';
        characters[0x9B] = '\u0142';
        characters[0x9C] = '\u0153';
        characters[0x9D] = '\u0161';
        characters[0x9E] = '\u017E';
        characters[0x9F] = ReplacementCharacter;
        characters[0xA0] = '\u20AC';
        return characters;
    }
}
