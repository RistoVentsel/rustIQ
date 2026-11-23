using RustIQ.Application.Servers.Wipes;

namespace RustIQ.Tests.Servers;

public sealed class WipePredictorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NextForced = new(2026, 12, 3, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NextForcedWipe_IsTheFirstThursdayAt19LondonTime()
    {
        Assert.Equal(NextForced, WipePredictor.NextForcedWipeUtc(Now));
        Assert.Equal(new DateTimeOffset(2026, 11, 5, 19, 0, 0, TimeSpan.Zero),
            WipePredictor.NextForcedWipeUtc(new DateTimeOffset(2026, 11, 5, 18, 0, 0, TimeSpan.Zero)));
        Assert.Equal(NextForced,
            WipePredictor.NextForcedWipeUtc(new DateTimeOffset(2026, 11, 5, 19, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void History_RegularGapsGiveTheLastWipePlusTheLastGap()
    {
        var wipes = Wipes("2026-11-17T10:00", "2026-11-19T10:00", "2026-11-21T11:30");

        var prediction = WipePredictor.Predict(wipes, WipeTagFrequency.Weekly, null, Now);

        Assert.Equal(WipePredictionSource.History, prediction.Source);
        Assert.Equal(Utc("2026-11-23T13:00"), prediction.NextWipeAtUtc);
        Assert.True(prediction.FromServerData);
    }

    [Fact]
    public void History_GapsDifferingByMoreThanTwoHoursFallThroughToTheNextRule()
    {
        var wipes = Wipes("2026-11-16T10:00", "2026-11-19T10:00", "2026-11-21T10:00");

        var prediction = WipePredictor.Predict(wipes, WipeTagFrequency.None, null, Now);

        Assert.Equal(WipePredictionSource.ForcedWipeFallback, prediction.Source);
        Assert.Equal(NextForced, prediction.NextWipeAtUtc);
        Assert.False(prediction.FromServerData);
    }

    [Fact]
    public void History_NeedsThreeKnownWipes()
    {
        var wipes = Wipes("2026-11-20T10:00", "2026-11-21T10:00");

        Assert.NotEqual(WipePredictionSource.History, WipePredictor.Predict(wipes, WipeTagFrequency.None, null, Now).Source);
    }

    [Fact]
    public void History_APassedTimeMovesForwardByTheGap()
    {
        var wipes = Wipes("2026-11-21T00:00", "2026-11-21T06:00", "2026-11-21T12:00");

        var prediction = WipePredictor.Predict(wipes, WipeTagFrequency.None, null, Now.AddHours(13));

        Assert.Equal(Utc("2026-11-23T06:00"), prediction.NextWipeAtUtc);
    }

    [Fact]
    public void WeeklyPattern_IsTheEarliestWipeOfTheLastWeekPlusSevenDays()
    {
        var wipes = Wipes("2026-11-12T19:00", "2026-11-16T18:00", "2026-11-19T19:00");

        var prediction = WipePredictor.Predict(wipes, WipeTagFrequency.Weekly, Utc("2026-11-26T19:00"), Now);

        Assert.Equal(WipePredictionSource.WeeklyPattern, prediction.Source);
        Assert.Equal(Utc("2026-11-23T18:00"), prediction.NextWipeAtUtc);
        Assert.True(prediction.FromServerData);
    }

    [Fact]
    public void WeeklyPattern_NeedsAKnownWipeAtLeastSevenDaysOld()
    {
        var wipes = Wipes("2026-11-16T18:00", "2026-11-19T19:00");

        Assert.NotEqual(WipePredictionSource.WeeklyPattern, WipePredictor.Predict(wipes, WipeTagFrequency.Weekly, null, Now).Source);
    }

    [Fact]
    public void WeeklyPattern_GivesNothingWhenTheLastWeekHadNoWipe()
    {
        var wipes = Wipes("2026-11-05T19:00", "2026-11-12T19:00");

        Assert.Equal(WipePredictionSource.Tag, WipePredictor.Predict(wipes, WipeTagFrequency.Weekly, null, Now).Source);
    }

    [Fact]
    public void Description_ComesBeforeTheTag()
    {
        var described = Utc("2026-11-24T18:00");

        var prediction = WipePredictor.Predict(Wipes("2026-11-20T15:00"), WipeTagFrequency.Weekly, described, Now);

        Assert.Equal(WipePredictionSource.Description, prediction.Source);
        Assert.Equal(described, prediction.NextWipeAtUtc);
    }

    [Fact]
    public void WeeklyTag_KeepsTheLastWipesWeekdayAndLondonTime()
    {
        var prediction = WipePredictor.Predict(Wipes("2026-11-20T15:00"), WipeTagFrequency.Weekly, null, Now);

        Assert.Equal(WipePredictionSource.Tag, prediction.Source);
        Assert.Equal(Utc("2026-11-27T15:00"), prediction.NextWipeAtUtc);
    }

    [Fact]
    public void BiweeklyTag_AddsFourteenDays()
    {
        var prediction = WipePredictor.Predict(Wipes("2026-11-12T20:00"), WipeTagFrequency.Biweekly, null, Now);

        Assert.Equal(Utc("2026-11-26T20:00"), prediction.NextWipeAtUtc);
    }

    [Fact]
    public void MonthlyTag_IsTheNextForcedWipe()
    {
        var prediction = WipePredictor.Predict(Wipes("2026-11-05T19:05"), WipeTagFrequency.Monthly, null, Now);

        Assert.Equal(WipePredictionSource.Tag, prediction.Source);
        Assert.Equal(NextForced, prediction.NextWipeAtUtc);
    }

    [Fact]
    public void APredictionLaterThanTheNextForcedWipeIsCappedAtIt()
    {
        var wipes = Wipes("2026-10-22T19:00", "2026-11-05T19:00", "2026-11-19T19:00");

        var prediction = WipePredictor.Predict(wipes, WipeTagFrequency.None, null, Now);

        Assert.Equal(WipePredictionSource.History, prediction.Source);
        Assert.Equal(NextForced, prediction.NextWipeAtUtc);
    }

    private static DateTimeOffset[] Wipes(params string[] times) => times.Select(Utc).ToArray();

    private static DateTimeOffset Utc(string text) =>
        DateTimeOffset.Parse(text + "Z", System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime();
}
