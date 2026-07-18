using RustIQ.Api.Workers;

namespace RustIQ.Tests.Workers;

public sealed class MapMetadataRefreshScheduleTests
{
    private static readonly TimeZoneInfo Tallinn = TimeZoneInfo.FindSystemTimeZoneById("Europe/Tallinn");

    [Theory]
    [InlineData("2026-07-18T14:30:00Z", "2026-07-18T15:00:00Z")]
    [InlineData("2026-07-18T15:00:00Z", "2026-07-18T19:00:00Z")]
    [InlineData("2026-07-18T20:00:00Z", "2026-07-19T00:00:00Z")]
    [InlineData("2026-01-18T15:30:00Z", "2026-01-18T16:00:00Z")]
    public void GetNextRunUtc_UsesTallinnLocalHoursAcrossDaysAndSeasons(
        string nowUtcText,
        string expectedUtcText)
    {
        var nowUtc = DateTimeOffset.Parse(nowUtcText);

        var result = MapMetadataRefreshSchedule.GetNextRunUtc(
            nowUtc,
            Tallinn,
            [3, 18, 22]);

        Assert.Equal(DateTimeOffset.Parse(expectedUtcText), result);
    }

    [Fact]
    public void GetNextRunUtc_MovesAnInvalidDstHourToTheFirstValidMinute()
    {
        var nowUtc = DateTimeOffset.Parse("2026-03-29T00:00:00Z");

        var result = MapMetadataRefreshSchedule.GetNextRunUtc(nowUtc, Tallinn, [3]);

        Assert.Equal(DateTimeOffset.Parse("2026-03-29T01:00:00Z"), result);
        Assert.Equal(4, TimeZoneInfo.ConvertTime(result, Tallinn).Hour);
    }
}
