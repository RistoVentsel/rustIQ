namespace RustIQ.Application.Servers.Wipes;

public enum WipeTagFrequency
{
    None,
    Weekly,
    Biweekly,
    Monthly
}

public enum WipePredictionSource
{
    History,
    WeeklyPattern,
    Description,
    Tag,
    ForcedWipeFallback
}

public sealed class WipePrediction
{
    public WipePrediction(DateTimeOffset nextWipeAtUtc, WipePredictionSource source)
    {
        NextWipeAtUtc = nextWipeAtUtc;
        Source = source;
    }

    public DateTimeOffset NextWipeAtUtc { get; }

    public WipePredictionSource Source { get; }

    public bool FromServerData
    {
        get { return Source != WipePredictionSource.ForcedWipeFallback; }
    }
}

public sealed class WipePredictorOptions
{
    public int MinimumKnownWipes { get; init; } = 3;

    public TimeSpan MaxGapDifference { get; init; } = TimeSpan.FromHours(2);
}

public static class WipePredictor
{
    private static readonly TimeZoneInfo London = FindLondon();

    public static WipePrediction Predict(
        IReadOnlyList<DateTimeOffset> knownWipesUtc,
        WipeTagFrequency tag,
        DateTimeOffset? describedNextWipeUtc,
        DateTimeOffset nowUtc,
        WipePredictorOptions? options = null)
    {
        if (options == null)
        {
            options = new WipePredictorOptions();
        }

        DateTimeOffset forced = NextForcedWipeUtc(nowUtc);

        WipePrediction? prediction = PredictFromHistory(knownWipesUtc, nowUtc, options);
        if (prediction == null)
        {
            prediction = PredictFromWeeklyPattern(knownWipesUtc, nowUtc);
        }

        if (prediction == null)
        {
            prediction = PredictFromDescription(describedNextWipeUtc, nowUtc);
        }

        if (prediction == null)
        {
            prediction = PredictFromTag(knownWipesUtc, tag, nowUtc, forced);
        }

        if (prediction == null)
        {
            return new WipePrediction(forced, WipePredictionSource.ForcedWipeFallback);
        }

        if (prediction.NextWipeAtUtc > forced)
        {
            return new WipePrediction(forced, prediction.Source);
        }

        return prediction;
    }

    public static DateTimeOffset NextForcedWipeUtc(DateTimeOffset nowUtc)
    {
        DateTime localNow = TimeZoneInfo.ConvertTime(nowUtc, London).DateTime;

        for (int i = 0; i <= 2; i++)
        {
            DateTime firstDay = new DateTime(localNow.Year, localNow.Month, 1).AddMonths(i);

            DateTime thursday = firstDay;
            while (thursday.DayOfWeek != DayOfWeek.Thursday)
            {
                thursday = thursday.AddDays(1);
            }

            DateTimeOffset wipeTime = ToUtc(thursday.AddHours(19));
            if (wipeTime > nowUtc)
            {
                return wipeTime;
            }
        }

        throw new InvalidOperationException("Could not calculate the next forced wipe.");
    }

    private static WipePrediction? PredictFromHistory(
        IReadOnlyList<DateTimeOffset> wipes,
        DateTimeOffset nowUtc,
        WipePredictorOptions options)
    {
        int count = wipes.Count;
        if (count < 3 || count < options.MinimumKnownWipes)
        {
            return null;
        }

        TimeSpan lastGap = wipes[count - 1] - wipes[count - 2];
        TimeSpan gapBefore = wipes[count - 2] - wipes[count - 3];

        if (lastGap <= TimeSpan.Zero)
        {
            return null;
        }

        TimeSpan difference = lastGap - gapBefore;
        if (difference < TimeSpan.Zero)
        {
            difference = -difference;
        }

        if (difference > options.MaxGapDifference)
        {
            return null;
        }

        DateTimeOffset next = wipes[count - 1] + lastGap;
        while (next <= nowUtc)
        {
            next = next + lastGap;
        }

        return new WipePrediction(next, WipePredictionSource.History);
    }

    private static WipePrediction? PredictFromWeeklyPattern(IReadOnlyList<DateTimeOffset> wipes, DateTimeOffset nowUtc)
    {
        if (wipes.Count == 0)
        {
            return null;
        }

        if (wipes[0] > nowUtc.AddDays(-7))
        {
            return null;
        }

        DateTimeOffset? next = null;
        for (int i = 0; i < wipes.Count; i++)
        {
            DateTime local = TimeZoneInfo.ConvertTime(wipes[i], London).DateTime;
            DateTimeOffset weekLater = ToUtc(local.AddDays(7));
            if (weekLater <= nowUtc)
            {
                continue;
            }

            if (next == null || weekLater < next.Value)
            {
                next = weekLater;
            }
        }

        if (next == null)
        {
            return null;
        }

        return new WipePrediction(next.Value, WipePredictionSource.WeeklyPattern);
    }

    private static WipePrediction? PredictFromDescription(DateTimeOffset? described, DateTimeOffset nowUtc)
    {
        if (described == null)
        {
            return null;
        }

        if (described.Value <= nowUtc)
        {
            return null;
        }

        return new WipePrediction(described.Value, WipePredictionSource.Description);
    }

    private static WipePrediction? PredictFromTag(
        IReadOnlyList<DateTimeOffset> wipes,
        WipeTagFrequency tag,
        DateTimeOffset nowUtc,
        DateTimeOffset forced)
    {
        if (tag == WipeTagFrequency.Monthly)
        {
            return new WipePrediction(forced, WipePredictionSource.Tag);
        }

        if (tag != WipeTagFrequency.Weekly && tag != WipeTagFrequency.Biweekly)
        {
            return null;
        }

        if (wipes.Count == 0)
        {
            return null;
        }

        int days = 7;
        if (tag == WipeTagFrequency.Biweekly)
        {
            days = 14;
        }

        DateTime local = TimeZoneInfo.ConvertTime(wipes[wipes.Count - 1], London).DateTime;
        DateTimeOffset next = ToUtc(local.AddDays(days));
        while (next <= nowUtc)
        {
            local = local.AddDays(days);
            next = ToUtc(local.AddDays(days));
        }

        return new WipePrediction(next, WipePredictionSource.Tag);
    }

    private static DateTimeOffset ToUtc(DateTime local)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (London.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, London));
    }

    private static TimeZoneInfo FindLondon()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        }
    }
}
