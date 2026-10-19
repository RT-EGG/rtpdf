namespace Rtpdf.Core.Parsing;

/// <summary>
/// Parses PDF date strings of the form <c>D:YYYYMMDDHHmmSSOHH'mm'</c>.
/// </summary>
public static class PdfDateParser
{
    /// <summary>
    /// Tries to parse a PDF date string. The returned value keeps the local calendar fields
    /// recorded in the PDF and therefore always has <see cref="DateTimeKind.Unspecified"/>.
    /// </summary>
    public static bool TryParse(string? value, out DateTime result) =>
        TryParse(value, out result, out _);

    /// <summary>
    /// Tries to parse a PDF date string and separately returns its UTC offset.
    /// A null offset means that the source did not specify its relationship to UTC.
    /// </summary>
    public static bool TryParse(
        string? value,
        out DateTime result,
        out TimeSpan? utcOffset)
    {
        result = default;
        utcOffset = null;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var input = value.AsSpan();
        if (input.StartsWith("D:".AsSpan(), StringComparison.Ordinal))
        {
            input = input[2..];
        }

        var numericLength = 0;
        while (numericLength < input.Length && IsDigit(input[numericLength]))
        {
            numericLength++;
        }

        if (numericLength is not (4 or 6 or 8 or 10 or 12 or 14))
        {
            return false;
        }

        var year = ReadNumber(input, 0, 4);
        var month = numericLength >= 6 ? ReadNumber(input, 4, 2) : 1;
        var day = numericLength >= 8 ? ReadNumber(input, 6, 2) : 1;
        var hour = numericLength >= 10 ? ReadNumber(input, 8, 2) : 0;
        var minute = numericLength >= 12 ? ReadNumber(input, 10, 2) : 0;
        var second = numericLength >= 14 ? ReadNumber(input, 12, 2) : 0;

        if (!TryCreateDateTime(year, month, day, hour, minute, second, out result))
        {
            return false;
        }

        var zone = input[numericLength..];
        if (zone.IsEmpty)
        {
            return true;
        }

        // A time-zone field follows the seconds field; omitted date/time fields cannot be skipped.
        if (numericLength != 14 || !TryParseUtcOffset(zone, out utcOffset))
        {
            result = default;
            utcOffset = null;
            return false;
        }

        return true;
    }

    private static bool TryParseUtcOffset(ReadOnlySpan<char> value, out TimeSpan? result)
    {
        result = null;
        if (value.SequenceEqual("Z".AsSpan()))
        {
            result = TimeSpan.Zero;
            return true;
        }

        if (value.Length != 7 ||
            value[0] is not ('+' or '-') ||
            value[3] != '\'' ||
            value[6] != '\'' ||
            !IsDigit(value[1]) ||
            !IsDigit(value[2]) ||
            !IsDigit(value[4]) ||
            !IsDigit(value[5]))
        {
            return false;
        }

        var hours = ReadNumber(value, 1, 2);
        var minutes = ReadNumber(value, 4, 2);
        if (hours > 23 || minutes > 59)
        {
            return false;
        }

        var offset = new TimeSpan(hours, minutes, 0);
        result = value[0] == '-' ? -offset : offset;
        return true;
    }

    private static bool TryCreateDateTime(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int second,
        out DateTime result)
    {
        try
        {
            result = new DateTime(
                year,
                month,
                day,
                hour,
                minute,
                second,
                DateTimeKind.Unspecified);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            result = default;
            return false;
        }
    }

    private static int ReadNumber(ReadOnlySpan<char> input, int offset, int length)
    {
        var result = 0;
        for (var index = offset; index < offset + length; index++)
        {
            result = (result * 10) + input[index] - '0';
        }

        return result;
    }

    private static bool IsDigit(char value) => value is >= '0' and <= '9';
}
