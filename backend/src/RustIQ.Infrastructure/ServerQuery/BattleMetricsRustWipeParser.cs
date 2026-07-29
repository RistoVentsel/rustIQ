using System.Globalization;
using System.Text.Json;

namespace RustIQ.Infrastructure.ServerQuery;

internal static class BattleMetricsRustWipeParser
{
    private static readonly string[] NextWipePropertyNames =
    [
        "rust_next_wipe",
        "rust_next_wipe_map",
        "rust_next_wipe_full",
        "rust_next_wipe_bp"
    ];

    public static DateTimeOffset? GetLastWipeAtUtc(JsonElement attributes, DateTimeOffset checkedAtUtc)
    {
        if (!TryGetDetails(attributes, out var details))
        {
            return null;
        }

        var lastWipe = GetDateTimeOffset(details, "rust_last_wipe");
        var born = GetDateTimeOffset(details, "rust_born");
        var nextWipe = GetNextWipeAtUtc(attributes, checkedAtUtc);

        if (born is not null && ShouldPreferBornAt(lastWipe, born.Value, nextWipe, checkedAtUtc))
        {
            return born.Value;
        }

        if (lastWipe is null)
        {
            return null;
        }

        return IsStaleLastWipe(lastWipe.Value, nextWipe, checkedAtUtc) ? null : lastWipe.Value;
    }

    public static DateTimeOffset? GetNextWipeAtUtc(JsonElement attributes, DateTimeOffset checkedAtUtc)
    {
        if (!TryGetDetails(attributes, out var details))
        {
            return null;
        }

        var candidates = new List<DateTimeOffset?>();
        foreach (var propertyName in NextWipePropertyNames)
        {
            candidates.Add(GetDateTimeOffset(details, propertyName));
        }

        if (details.TryGetProperty("rust_wipes", out var rustWipes)
            && rustWipes.ValueKind == JsonValueKind.Array)
        {
            foreach (var wipe in rustWipes.EnumerateArray())
            {
                candidates.Add(GetDateTimeOffset(wipe, "timestamp"));
            }
        }

        return candidates
            .OfType<DateTimeOffset>()
            .Where(candidate => candidate >= checkedAtUtc.AddMinutes(-5))
            .OrderBy(candidate => candidate)
            .Select(candidate => (DateTimeOffset?)candidate)
            .FirstOrDefault();
    }

    private static bool ShouldPreferBornAt(
        DateTimeOffset? lastWipe,
        DateTimeOffset born,
        DateTimeOffset? nextWipe,
        DateTimeOffset checkedAtUtc)
    {
        if (born > checkedAtUtc.AddMinutes(5))
        {
            return false;
        }

        if (nextWipe is not null && born > nextWipe.Value)
        {
            return false;
        }

        if (lastWipe is null)
        {
            return true;
        }

        return born > lastWipe.Value;
    }

    private static bool IsStaleLastWipe(
        DateTimeOffset lastWipe,
        DateTimeOffset? nextWipe,
        DateTimeOffset checkedAtUtc)
    {
        if (lastWipe > checkedAtUtc.AddMinutes(5))
        {
            return true;
        }

        if (checkedAtUtc - lastWipe <= TimeSpan.FromDays(90))
        {
            return false;
        }

        return nextWipe is null || nextWipe.Value >= checkedAtUtc.AddMinutes(-5);
    }

    private static bool TryGetDetails(JsonElement attributes, out JsonElement details)
    {
        details = default;
        return attributes.ValueKind == JsonValueKind.Object
               && attributes.TryGetProperty("details", out details)
               && details.ValueKind == JsonValueKind.Object;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var text = property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }
}
