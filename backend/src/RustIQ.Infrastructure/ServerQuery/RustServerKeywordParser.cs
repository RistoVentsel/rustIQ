using System.Globalization;

namespace RustIQ.Infrastructure.ServerQuery;

public static class RustServerKeywordParser
{
    private const string BornPrefix = "born";
    private const string CurrentPlayersPrefix = "cp";
    private const string MaxPlayersPrefix = "mp";
    private const string QueuePlayersPrefix = "qp";
    private const string WeeklyWipeTag = "^w";
    private const string BiweeklyWipeTag = "^b";
    private const string MonthlyWipeTag = "^m";
    private const int DefaultWipeHour = 19;

    public static int? GetCurrentPlayers(string? keywords)
    {
        return GetNonNegativeInt(keywords, CurrentPlayersPrefix);
    }

    public static int? GetMaxPlayers(string? keywords)
    {
        return GetNonNegativeInt(keywords, MaxPlayersPrefix);
    }

    public static int? GetQueuePlayers(string? keywords)
    {
        return GetNonNegativeInt(keywords, QueuePlayersPrefix);
    }

    public static DateTimeOffset? GetLastWipeAtUtc(
        string? keywords,
        DateTimeOffset checkedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(keywords))
        {
            return null;
        }

        foreach (var keyword in keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!keyword.StartsWith(BornPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var timestampText = keyword[BornPrefix.Length..];
            if (!long.TryParse(timestampText, NumberStyles.None, CultureInfo.InvariantCulture, out var unixSeconds))
            {
                continue;
            }

            try
            {
                var bornAtUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
                if (bornAtUtc <= checkedAtUtc.AddMinutes(5))
                {
                    return bornAtUtc;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        return null;
    }

    public static DateTimeOffset? GetNextWipeAtUtc(
        string? keywords,
        DateTimeOffset checkedAtUtc)
    {
        var frequency = GetWipeFrequency(keywords);
        if (frequency is null || !TryGetLondonTimeZone(out var timeZone))
        {
            return null;
        }

        var localNow = TimeZoneInfo.ConvertTime(checkedAtUtc, timeZone);
        for (var dayOffset = 0; dayOffset <= 42; dayOffset++)
        {
            var date = localNow.Date.AddDays(dayOffset);
            if (date.DayOfWeek != DayOfWeek.Thursday
                || !IsScheduledWipeDate(date, frequency.Value))
            {
                continue;
            }

            var localCandidate = DateTime.SpecifyKind(
                date.AddHours(DefaultWipeHour),
                DateTimeKind.Unspecified);
            if (localCandidate < localNow.DateTime)
            {
                continue;
            }

            return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localCandidate, timeZone));
        }

        return null;
    }

    private static WipeFrequency? GetWipeFrequency(string? keywords)
    {
        if (string.IsNullOrWhiteSpace(keywords))
        {
            return null;
        }

        foreach (var keyword in keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (keyword.Equals(WeeklyWipeTag, StringComparison.OrdinalIgnoreCase)
                || keyword.Equals("weekly", StringComparison.OrdinalIgnoreCase))
            {
                return WipeFrequency.Weekly;
            }

            if (keyword.Equals(BiweeklyWipeTag, StringComparison.OrdinalIgnoreCase)
                || keyword.Equals("biweekly", StringComparison.OrdinalIgnoreCase))
            {
                return WipeFrequency.Biweekly;
            }

            if (keyword.Equals(MonthlyWipeTag, StringComparison.OrdinalIgnoreCase)
                || keyword.Equals("monthly", StringComparison.OrdinalIgnoreCase))
            {
                return WipeFrequency.Monthly;
            }
        }

        return null;
    }

    private static bool IsScheduledWipeDate(DateTime date, WipeFrequency frequency)
    {
        var thursdayOfMonth = ((date.Day - 1) / 7) + 1;
        return frequency switch
        {
            WipeFrequency.Weekly => true,
            WipeFrequency.Biweekly => thursdayOfMonth % 2 == 1,
            WipeFrequency.Monthly => thursdayOfMonth == 1,
            _ => false
        };
    }

    private static bool TryGetLondonTimeZone(out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = null!;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = null!;
            return false;
        }
    }

    private static int? GetNonNegativeInt(string? keywords, string prefix)
    {
        if (string.IsNullOrWhiteSpace(keywords))
        {
            return null;
        }

        foreach (var keyword in keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!keyword.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var valueText = keyword[prefix.Length..];
            if (int.TryParse(valueText, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                && value >= 0)
            {
                return value;
            }
        }

        return null;
    }

    private enum WipeFrequency
    {
        Weekly,
        Biweekly,
        Monthly
    }
}
