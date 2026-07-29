using System.Globalization;

namespace RustIQ.Infrastructure.ServerQuery;

public static class RustServerKeywordParser
{
    private const string BornPrefix = "born";
    private const string CurrentPlayersPrefix = "cp";
    private const string MaxPlayersPrefix = "mp";
    private const string QueuePlayersPrefix = "qp";

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
}
