using RustIQ.Infrastructure.ServerQuery;

namespace RustIQ.Tests.ServerQuery;

public sealed class RustWipeScheduleParserTests
{
    private static readonly DateTimeOffset CheckedAtUtc =
        DateTimeOffset.Parse("2026-07-31T11:45:00Z");

    [Fact]
    public void GetNextWipeAtUtc_ParsesWeeklyLondonScheduleAcrossDescriptionFragments()
    {
        var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["description_00"] = "Weekly map wipe @ Fridays 17:00 London Ti",
            ["description_01"] = "me (unless forced wipe)"
        };

        var result = RustWipeScheduleParser.GetNextWipeAtUtc(
            rules,
            DateTimeOffset.Parse("2026-07-24T15:59:06Z"),
            CheckedAtUtc);

        Assert.Equal(DateTimeOffset.Parse("2026-07-31T16:00:00Z"), result);
    }

    [Fact]
    public void GetNextWipeAtUtc_ParsesWeeklyNewYorkScheduleWithDaylightSavingTime()
    {
        var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["description_00"] = "Weekly map wipe @ Fridays 17:00 New York Time (unless forced wipe)"
        };

        var result = RustWipeScheduleParser.GetNextWipeAtUtc(
            rules,
            DateTimeOffset.Parse("2026-07-24T20:00:00Z"),
            CheckedAtUtc);

        Assert.Equal(DateTimeOffset.Parse("2026-07-31T21:00:00Z"), result);
    }

    [Fact]
    public void GetNextWipeAtUtc_ParsesMapScheduleWithoutMisreadingForcedWipe()
    {
        var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["description_00"] = "Wipe Schedule\\n - Map: Mondays @ 15:00 London Time (unless force wipe)\\n",
            ["description_01"] = "Force wipes are always on the First Thursday @ 2PM EST"
        };

        var result = RustWipeScheduleParser.GetNextWipeAtUtc(
            rules,
            DateTimeOffset.Parse("2026-07-27T14:00:00Z"),
            CheckedAtUtc);

        Assert.Equal(DateTimeOffset.Parse("2026-08-03T14:00:00Z"), result);
    }

    [Fact]
    public void GetNextWipeAtUtc_DoesNotTreatMonthlyForcedWipeAsWeekly()
    {
        var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["description_00"] = "Forced wipe: First Thursday @ 2PM EST / 19:00 London"
        };

        var result = RustWipeScheduleParser.GetNextWipeAtUtc(
            rules,
            DateTimeOffset.Parse("2026-07-02T18:00:00Z"),
            CheckedAtUtc);

        Assert.Null(result);
    }

    [Fact]
    public void GetNextWipeAtUtc_DoesNotTreatUnrelatedEventAsWipeSchedule()
    {
        var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["description_00"] = "Community event Fridays 17:00 London Time. Weekly wipe details are on Discord."
        };

        var result = RustWipeScheduleParser.GetNextWipeAtUtc(
            rules,
            DateTimeOffset.Parse("2026-07-24T16:00:00Z"),
            CheckedAtUtc);

        Assert.Null(result);
    }

    [Fact]
    public void GetNextWipeAtUtc_DoesNotThrowWhenDescriptionStartsWithWeekday()
    {
        var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["description_00"] = "Fridays 17:00 London Time community event"
        };

        var exception = Record.Exception(() => RustWipeScheduleParser.GetNextWipeAtUtc(
            rules,
            DateTimeOffset.Parse("2026-07-24T16:00:00Z"),
            CheckedAtUtc));

        Assert.Null(exception);
    }

    [Fact]
    public void GetNextWipeAtUtc_RollsPastScheduleForwardWithoutReturningExpiredDate()
    {
        var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["description_00"] = "Weekly map wipe @ Fridays 17:00 London Time"
        };

        var result = RustWipeScheduleParser.GetNextWipeAtUtc(
            rules,
            DateTimeOffset.Parse("2026-07-24T16:00:00Z"),
            DateTimeOffset.Parse("2026-07-31T16:01:00Z"));

        Assert.Equal(DateTimeOffset.Parse("2026-08-07T16:00:00Z"), result);
    }

    [Fact]
    public void GetNextWipeAtUtc_DoesNotTreatNonOrdinalForcedWipeAsWeekly()
    {
        var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["description_00"] = "Forced wipe: Thursday @ 2PM EST"
        };

        var result = RustWipeScheduleParser.GetNextWipeAtUtc(
            rules,
            DateTimeOffset.Parse("2026-07-02T18:00:00Z"),
            CheckedAtUtc);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("Map - Weekly on Thursdays @ 3PM UK / 4PM CEST", "2026-08-06T14:00:00Z")]
    [InlineData("Map wipes Every Sunday at 12:45 CEST", "2026-08-02T10:45:00Z")]
    [InlineData("Map: Biweekly (every other Thursday) @ 15:00 London Time", "2026-08-06T14:00:00Z")]
    public void GetNextWipeAtUtc_ParsesObservedServerScheduleFormats(
        string description,
        string expectedUtc)
    {
        var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["description_00"] = description
        };

        var result = RustWipeScheduleParser.GetNextWipeAtUtc(
            rules,
            DateTimeOffset.Parse("2026-07-23T14:00:00Z"),
            CheckedAtUtc);

        Assert.Equal(DateTimeOffset.Parse(expectedUtc), result);
    }

    [Theory]
    [InlineData("Weekly map wipe @ 15:00 London Time", "2026-08-06T14:00:00Z")]
    [InlineData("Biweekly map wipe @ 15:00 London Time", "2026-08-13T14:00:00Z")]
    public void GetNextWipeAtUtc_InfersMissingWeekdayFromLastWipe(
        string description,
        string expectedUtc)
    {
        var rules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["description_00"] = description
        };

        var result = RustWipeScheduleParser.GetNextWipeAtUtc(
            rules,
            DateTimeOffset.Parse("2026-07-30T14:00:00Z"),
            CheckedAtUtc);

        Assert.Equal(DateTimeOffset.Parse(expectedUtc), result);
    }
}
