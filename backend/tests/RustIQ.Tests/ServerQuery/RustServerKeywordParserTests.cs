using RustIQ.Infrastructure.ServerQuery;

namespace RustIQ.Tests.ServerQuery;

public sealed class RustServerKeywordParserTests
{
    [Fact]
    public void PopulationParsers_ReadFullRustCountsBeyondA2sByteLimit()
    {
        const string keywords = "mp725,cp669,ptrak,qp56,$r?,v2631,gmrust";

        Assert.Equal(669, RustServerKeywordParser.GetCurrentPlayers(keywords));
        Assert.Equal(725, RustServerKeywordParser.GetMaxPlayers(keywords));
        Assert.Equal(56, RustServerKeywordParser.GetQueuePlayers(keywords));
    }

    [Fact]
    public void PopulationParsers_IgnoreInvalidCounts()
    {
        const string keywords = "mp-1,cpunknown,qp";

        Assert.Null(RustServerKeywordParser.GetCurrentPlayers(keywords));
        Assert.Null(RustServerKeywordParser.GetMaxPlayers(keywords));
        Assert.Null(RustServerKeywordParser.GetQueuePlayers(keywords));
    }

    [Fact]
    public void GetLastWipeAtUtc_ReadsBornUnixTimestampFromRustKeywords()
    {
        var bornAtUtc = DateTimeOffset.Parse("2026-07-29T07:30:00Z");
        var checkedAtUtc = bornAtUtc.AddHours(1);
        var keywords = $"mp200,qp12,born{bornAtUtc.ToUnixTimeSeconds()},gmrust";

        var result = RustServerKeywordParser.GetLastWipeAtUtc(keywords, checkedAtUtc);

        Assert.Equal(bornAtUtc, result);
    }

    [Fact]
    public void GetLastWipeAtUtc_RejectsFutureBornTimestamp()
    {
        var checkedAtUtc = DateTimeOffset.Parse("2026-07-29T08:00:00Z");
        var futureBornAtUtc = checkedAtUtc.AddHours(1);

        var result = RustServerKeywordParser.GetLastWipeAtUtc(
            $"born{futureBornAtUtc.ToUnixTimeSeconds()}",
            checkedAtUtc);

        Assert.Null(result);
    }
}
