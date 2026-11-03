using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RustIQ.Infrastructure.ServerQuery;

public static partial class RustWipeScheduleParser
{
    private const int DescriptionFragmentCount = 16;
    private const int ForcedWipeHour = 19;

    public static DateTimeOffset GetNextForcedWipeAtUtc(DateTimeOffset checkedAtUtc)
    {
        var timeZone = GetTimeZoneOrUtc("Europe/London");
        var localNow = TimeZoneInfo.ConvertTime(checkedAtUtc, timeZone);
        var month = new DateTime(localNow.Year, localNow.Month, 1);

        for (var monthOffset = 0; monthOffset < 2; monthOffset++)
        {
            var firstOfMonth = month.AddMonths(monthOffset);
            var daysUntilThursday = ((int)DayOfWeek.Thursday - (int)firstOfMonth.DayOfWeek + 7) % 7;
            var localCandidate = DateTime.SpecifyKind(
                firstOfMonth.AddDays(daysUntilThursday).AddHours(ForcedWipeHour),
                DateTimeKind.Unspecified);
            if (localCandidate >= localNow.DateTime)
            {
                return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localCandidate, timeZone));
            }
        }

        throw new InvalidOperationException("Could not calculate the next Rust forced wipe.");
    }

    public static DateTimeOffset GetEarliestUpcomingWipeAtUtc(
        DateTimeOffset checkedAtUtc,
        params DateTimeOffset?[] candidates)
    {
        return candidates
            .Where(candidate => candidate >= checkedAtUtc)
            .Min()
            ?? GetNextForcedWipeAtUtc(checkedAtUtc);
    }

    public static DateTimeOffset? GetNextWipeAtUtc(
        IReadOnlyDictionary<string, string> rules,
        DateTimeOffset? lastWipeAtUtc,
        DateTimeOffset checkedAtUtc)
    {
        var description = GetDescription(rules);
        if (description is null)
        {
            return null;
        }

        foreach (Match match in WeeklyScheduleRegex().Matches(description))
        {
            if (IsOrdinalOccurrence(description, match.Index))
            {
                continue;
            }

            var context = GetScheduleContext(description, match.Index);
            if (!HasWipeScheduleContext(context)
                || context.Contains("monthly", StringComparison.OrdinalIgnoreCase)
                || context.Contains("forced wipe", StringComparison.OrdinalIgnoreCase)
                || context.Contains("force wipe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Enum.TryParse<DayOfWeek>(match.Groups["day"].Value, ignoreCase: true, out var dayOfWeek)
                || !TryGetTime(match, out var timeOfDay)
                || !TryGetTimeZone(match.Groups["zone"].Value, out var timeZone))
            {
                continue;
            }

            var recurrenceWeeks = context.Contains("biweekly", StringComparison.OrdinalIgnoreCase)
                                  || context.Contains("fortnight", StringComparison.OrdinalIgnoreCase)
                                  || context.Contains("every 2 weeks", StringComparison.OrdinalIgnoreCase)
                ? 2
                : 1;

            return GetNextOccurrenceUtc(
                dayOfWeek,
                timeOfDay,
                timeZone,
                recurrenceWeeks,
                lastWipeAtUtc,
                checkedAtUtc);
        }

        if (lastWipeAtUtc is not null)
        {
            foreach (Match match in WeekdaylessScheduleRegex().Matches(description))
            {
                if (!TryGetTime(match, out var timeOfDay)
                    || !TryGetTimeZone(match.Groups["zone"].Value, out var timeZone))
                {
                    continue;
                }

                var recurrenceWeeks = match.Groups["recurrence"].Value.Equals(
                    "biweekly",
                    StringComparison.OrdinalIgnoreCase)
                    ? 2
                    : 1;
                var localLastWipe = TimeZoneInfo.ConvertTime(lastWipeAtUtc.Value, timeZone);
                return GetNextOccurrenceUtc(
                    localLastWipe.DayOfWeek,
                    timeOfDay,
                    timeZone,
                    recurrenceWeeks,
                    lastWipeAtUtc,
                    checkedAtUtc);
            }
        }

        return null;
    }

    public static string? GetDescription(IReadOnlyDictionary<string, string> rules)
    {
        var description = new StringBuilder();
        for (var index = 0; index < DescriptionFragmentCount; index++)
        {
            if (rules.TryGetValue($"description_{index:00}", out var fragment))
            {
                description.Append(fragment);
            }
        }

        if (description.Length == 0 && rules.TryGetValue("description", out var singleDescription))
        {
            description.Append(singleDescription);
        }

        return description.Length == 0
            ? null
            : description.ToString().Replace("\\n", "\n", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOrdinalOccurrence(string description, int matchIndex)
    {
        var prefixStart = Math.Max(0, matchIndex - 24);
        var prefix = description[prefixStart..matchIndex];
        return OrdinalSuffixRegex().IsMatch(prefix);
    }

    private static string GetScheduleContext(string description, int matchIndex)
    {
        var contextStart = Math.Max(0, matchIndex - 80);
        if (matchIndex > 0)
        {
            var lineStart = description.LastIndexOf('\n', matchIndex - 1, matchIndex - contextStart);
            if (lineStart >= contextStart)
            {
                contextStart = lineStart + 1;
            }
        }

        return description[contextStart..matchIndex];
    }

    private static bool HasWipeScheduleContext(string context)
    {
        return context.Contains("wipe", StringComparison.OrdinalIgnoreCase)
               || MapScheduleLabelRegex().IsMatch(context);
    }

    private static bool TryGetTime(Match match, out TimeSpan timeOfDay)
    {
        timeOfDay = default;
        if (!int.TryParse(match.Groups["hour"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var hour))
        {
            return false;
        }

        var minute = 0;
        if (match.Groups["minute"].Success
            && !int.TryParse(match.Groups["minute"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out minute))
        {
            return false;
        }

        var meridiem = match.Groups["meridiem"].Value
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
        if (meridiem.Length > 0)
        {
            if (hour is < 1 or > 12)
            {
                return false;
            }

            if (meridiem == "PM" && hour < 12)
            {
                hour += 12;
            }
            else if (meridiem == "AM" && hour == 12)
            {
                hour = 0;
            }
        }

        if (hour is < 0 or > 23 || minute is < 0 or > 59)
        {
            return false;
        }

        timeOfDay = new TimeSpan(hour, minute, 0);
        return true;
    }

    private static bool TryGetTimeZone(string value, out TimeZoneInfo timeZone)
    {
        var normalized = Regex.Replace(value, @"\s+", " ").Trim().ToLowerInvariant();
        var timeZoneId = normalized switch
        {
            "london" or "london time" => "Europe/London",
            "new york" or "new york time" or "eastern" or "eastern time" or "est" or "edt" => "America/New_York",
            "chicago" or "chicago time" or "central" or "central time" or "cst" or "cdt" => "America/Chicago",
            "denver" or "denver time" or "mountain" or "mountain time" or "mst" or "mdt" => "America/Denver",
            "los angeles" or "los angeles time" or "pacific" or "pacific time" or "pst" or "pdt" => "America/Los_Angeles",
            "sydney" or "sydney time" or "aest" or "aedt" => "Australia/Sydney",
            "uk" => "Europe/London",
            "cet" or "cest" => "Europe/Berlin",
            "utc" or "utc time" or "gmt" or "gmt time" => "UTC",
            _ => null
        };

        if (timeZoneId is null)
        {
            timeZone = null!;
            return false;
        }

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
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

    private static TimeZoneInfo GetTimeZoneOrUtc(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static DateTimeOffset GetNextOccurrenceUtc(
        DayOfWeek dayOfWeek,
        TimeSpan timeOfDay,
        TimeZoneInfo timeZone,
        int recurrenceWeeks,
        DateTimeOffset? lastWipeAtUtc,
        DateTimeOffset checkedAtUtc)
    {
        var localNow = TimeZoneInfo.ConvertTime(checkedAtUtc, timeZone);
        var daysUntil = ((int)dayOfWeek - (int)localNow.DayOfWeek + 7) % 7;
        var localCandidate = DateTime.SpecifyKind(
            localNow.Date.AddDays(daysUntil).Add(timeOfDay),
            DateTimeKind.Unspecified);

        if (localCandidate < localNow.DateTime)
        {
            localCandidate = localCandidate.AddDays(7);
        }

        var candidateUtc = ConvertToUtc(localCandidate, timeZone);
        if (recurrenceWeeks == 1 || lastWipeAtUtc is null)
        {
            return candidateUtc;
        }

        while (candidateUtc <= lastWipeAtUtc.Value)
        {
            localCandidate = localCandidate.AddDays(7);
            candidateUtc = ConvertToUtc(localCandidate, timeZone);
        }

        var elapsedWeeks = (int)Math.Round(
            (candidateUtc - lastWipeAtUtc.Value).TotalDays / 7,
            MidpointRounding.AwayFromZero);
        while (elapsedWeeks % recurrenceWeeks != 0)
        {
            localCandidate = localCandidate.AddDays(7);
            candidateUtc = ConvertToUtc(localCandidate, timeZone);
            elapsedWeeks++;
        }

        return candidateUtc;
    }

    private static DateTimeOffset ConvertToUtc(DateTime localCandidate, TimeZoneInfo timeZone)
    {
        if (timeZone.IsInvalidTime(localCandidate))
        {
            localCandidate = localCandidate.AddHours(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localCandidate, timeZone));
    }

    [GeneratedRegex(
        @"\b(?<day>Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)(?:s)?\s*\)?\s*(?:(?:@|at)\s*)?(?<hour>\d{1,2})(?::(?<minute>\d{2}))?\s*(?<meridiem>a\.?m\.?|p\.?m\.?)?\s*(?<zone>London(?:\s+Time)?|New\s+York(?:\s+Time)?|Eastern(?:\s+Time)?|Chicago(?:\s+Time)?|Central(?:\s+Time)?|Denver(?:\s+Time)?|Mountain(?:\s+Time)?|Los\s+Angeles(?:\s+Time)?|Pacific(?:\s+Time)?|Sydney(?:\s+Time)?|UTC(?:\s+Time)?|GMT(?:\s+Time)?|EST|EDT|CST|CDT|MST|MDT|PST|PDT|AEST|AEDT|UK|CET|CEST)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WeeklyScheduleRegex();

    [GeneratedRegex(
        @"\b(?<recurrence>weekly|biweekly)\s+map\s+wipe\s*(?:(?:@|at)\s*)?(?<hour>\d{1,2})(?::(?<minute>\d{2}))?\s*(?<meridiem>a\.?m\.?|p\.?m\.?)?\s*(?<zone>London(?:\s+Time)?|New\s+York(?:\s+Time)?|Eastern(?:\s+Time)?|Chicago(?:\s+Time)?|Central(?:\s+Time)?|Denver(?:\s+Time)?|Mountain(?:\s+Time)?|Los\s+Angeles(?:\s+Time)?|Pacific(?:\s+Time)?|Sydney(?:\s+Time)?|UTC(?:\s+Time)?|GMT(?:\s+Time)?|EST|EDT|CST|CDT|MST|MDT|PST|PDT|AEST|AEDT|UK|CET|CEST)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WeekdaylessScheduleRegex();

    [GeneratedRegex(
        @"\b(?:first|second|third|fourth|last)\s+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OrdinalSuffixRegex();

    [GeneratedRegex(
        @"\bmap\s*[-:]\s*(?:(?:weekly|biweekly)(?:\s+(?:on|\(every\s+other))?\s*)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MapScheduleLabelRegex();
}
