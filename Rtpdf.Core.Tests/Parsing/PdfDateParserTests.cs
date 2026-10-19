using Rtpdf.Core.Parsing;
using Xunit;

namespace Rtpdf.Core.Tests.Parsing;

public sealed class PdfDateParserTests
{
    [Theory]
    [InlineData("D:2026", 2026, 1, 1, 0, 0, 0)]
    [InlineData("202602", 2026, 2, 1, 0, 0, 0)]
    [InlineData("D:20260203", 2026, 2, 3, 0, 0, 0)]
    [InlineData("D:2026020304", 2026, 2, 3, 4, 0, 0)]
    [InlineData("D:202602030405", 2026, 2, 3, 4, 5, 0)]
    [InlineData("D:20260203040506", 2026, 2, 3, 4, 5, 6)]
    public void TryParseSupportsOptionalPrefixAndTrailingFields(
        string input,
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int second)
    {
        var success = PdfDateParser.TryParse(input, out var result);

        Assert.True(success);
        Assert.Equal(new DateTime(year, month, day, hour, minute, second), result);
        Assert.Equal(DateTimeKind.Unspecified, result.Kind);
    }

    [Theory]
    [InlineData("D:20261019153045Z", 0, 0)]
    [InlineData("D:20261019153045+09'30'", 9, 30)]
    [InlineData("D:20261019153045-08'00'", -8, 0)]
    public void TryParseReturnsUtcOffsetWithoutChangingLocalCalendarFields(
        string input,
        int offsetHours,
        int offsetMinutes)
    {
        var success = PdfDateParser.TryParse(input, out var result, out var utcOffset);

        Assert.True(success);
        Assert.Equal(new DateTime(2026, 10, 19, 15, 30, 45), result);
        Assert.Equal(new TimeSpan(offsetHours, offsetMinutes, 0), utcOffset);
    }

    [Fact]
    public void TryParseReturnsNullOffsetWhenUtcRelationshipIsUnknown()
    {
        var success = PdfDateParser.TryParse(
            "D:20261019153045",
            out var result,
            out var utcOffset);

        Assert.True(success);
        Assert.Equal(new DateTime(2026, 10, 19, 15, 30, 45), result);
        Assert.Null(utcOffset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("D:")]
    [InlineData("D:202")]
    [InlineData("D:20261")]
    [InlineData("D:202613")]
    [InlineData("D:20260230")]
    [InlineData("D:2026010124")]
    [InlineData("D:202601010060")]
    [InlineData("D:20260101000060")]
    [InlineData("D:2026Z")]
    [InlineData("D:20260101000000z")]
    [InlineData("D:20260101000000+0900")]
    [InlineData("D:20260101000000+09'00")]
    [InlineData("D:20260101000000+24'00'")]
    [InlineData("D:20260101000000+09'60'")]
    [InlineData("D:20260101000000Zextra")]
    [InlineData(" D:20260101")]
    public void TryParseRejectsMalformedOrOutOfRangeValues(string? input)
    {
        var success = PdfDateParser.TryParse(input, out var result, out var utcOffset);

        Assert.False(success);
        Assert.Equal(default, result);
        Assert.Null(utcOffset);
    }

    [Fact]
    public void TryParseAcceptsLeapDayOnlyForLeapYears()
    {
        Assert.True(PdfDateParser.TryParse("D:20240229", out var leapDay));
        Assert.Equal(new DateTime(2024, 2, 29), leapDay);
        Assert.False(PdfDateParser.TryParse("D:20230229", out _));
    }
}
