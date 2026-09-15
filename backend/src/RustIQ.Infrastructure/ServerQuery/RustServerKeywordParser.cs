using System.Globalization;

namespace RustIQ.Infrastructure.ServerQuery;

public static class RustServerKeywordParser
{
    // Rust publishes its server browser tags as caret letters inside the keyword string, documented
    // at https://wiki.facepunch.com/rust/server-browser-tags. They are not reliably comma separated:
    // a modded Oxide server sends the single token "^o^z", so the tags are scanned as flags.
    public const char BrowserTagPrefix = '^';
    public const char ModdedBrowserTag = 'z';
    public const char VanillaBrowserTag = 'v';
    public const char OxideBrowserTag = 'o';
    public const char CarbonBrowserTag = 'y';

    private const string BornPrefix = "born";
    private const string CurrentPlayersPrefix = "cp";
    private const string GameModePrefix = "gm";
    private const string MaxPlayersPrefix = "mp";
    private const string QueuePlayersPrefix = "qp";
    private const char WeeklyWipeTag = 'w';
    private const char BiweeklyWipeTag = 'b';
    private const char MonthlyWipeTag = 'm';
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

    /// <summary>
    /// Tells whether the keyword string carries a Rust server browser tag, for example 'z' for the
    /// modded tag that Rust writes as "^z".
    /// </summary>
    public static bool HasBrowserTag(string? keywords, char browserTag)
    {
        if (string.IsNullOrWhiteSpace(keywords))
        {
            return false;
        }

        // Scanned rather than split on commas: Rust runs neighbouring tags together, so "^o^z" is one
        // token and a token-by-token comparison would miss the modded flag on every Oxide server.
        for (var index = 0; index < keywords.Length - 1; index++)
        {
            if (keywords[index] == BrowserTagPrefix
                && char.ToLowerInvariant(keywords[index + 1]) == char.ToLowerInvariant(browserTag))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tells whether the keyword string carries a spelled out tag, for example "modded". Servers on
    /// older builds, and admins writing sv_tags by hand, still use the long form.
    /// </summary>
    public static bool HasWordTag(string? keywords, string wordTag)
    {
        return EnumerateKeywords(keywords)
            .Any(keyword => keyword.Equals(wordTag, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Tells whether the keyword string came from a Rust server at all. A server that answered with
    /// Rust keywords is at least a community server; one that told us nothing stays unknown.
    /// </summary>
    public static bool LooksLikeRustKeywords(string? keywords)
    {
        foreach (var keyword in EnumerateKeywords(keywords))
        {
            if (keyword[0] == BrowserTagPrefix
                || keyword.StartsWith(GameModePrefix, StringComparison.OrdinalIgnoreCase)
                || keyword.StartsWith(BornPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return GetMaxPlayers(keywords) is not null;
    }

    private static WipeFrequency? GetWipeFrequency(string? keywords)
    {
        if (string.IsNullOrWhiteSpace(keywords))
        {
            return null;
        }

        if (HasBrowserTag(keywords, WeeklyWipeTag) || HasWordTag(keywords, "weekly"))
        {
            return WipeFrequency.Weekly;
        }

        if (HasBrowserTag(keywords, BiweeklyWipeTag) || HasWordTag(keywords, "biweekly"))
        {
            return WipeFrequency.Biweekly;
        }

        if (HasBrowserTag(keywords, MonthlyWipeTag) || HasWordTag(keywords, "monthly"))
        {
            return WipeFrequency.Monthly;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateKeywords(string? keywords)
    {
        return string.IsNullOrWhiteSpace(keywords)
            ? []
            : keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
