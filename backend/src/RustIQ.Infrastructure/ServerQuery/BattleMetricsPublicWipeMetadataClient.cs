using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;

namespace RustIQ.Infrastructure.ServerQuery;

public sealed partial class BattleMetricsPublicWipeMetadataClient(
    IBattleMetricsPublicPageFetcher pageFetcher,
    IOptions<BattleMetricsWipeFallbackOptions> options)
    : IBattleMetricsWipeMetadataClient
{
    private readonly bool _enabled = options.Value.Enabled;

    public async Task<BattleMetricsWipeMetadata?> GetWipeMetadataAsync(
        ConfiguredRustServer configuredServer,
        CancellationToken cancellationToken)
    {
        if (!_enabled)
        {
            return null;
        }

        var battleMetricsServerId = configuredServer.BattleMetricsServerId;
        if (battleMetricsServerId is null && !string.IsNullOrWhiteSpace(configuredServer.Name))
        {
            // A server's advertised name often contains a changing wipe date. Searching by the
            // endpoint keeps the result set tied to the actual server instead of that transient name.
            var searchHtml = await pageFetcher.SearchRustServersAsync(
                $"{configuredServer.Host}:{configuredServer.QueryPort}",
                cancellationToken);
            battleMetricsServerId = string.IsNullOrWhiteSpace(searchHtml)
                ? null
                : FindServerId(searchHtml, configuredServer.Name);
        }

        if (battleMetricsServerId is null)
        {
            return null;
        }

        var html = await pageFetcher.FetchRustSettingsAsync(
            battleMetricsServerId.Value,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var checkedAtUtc = DateTimeOffset.UtcNow;
        var nextWipeAtUtc = GetNextMapWipeAtUtc(html, checkedAtUtc);
        return nextWipeAtUtc is null
            ? null
            : new BattleMetricsWipeMetadata(
                battleMetricsServerId.Value,
                LastWipeAtUtc: null,
                nextWipeAtUtc);
    }

    public static long? FindServerId(string html, string serverName)
    {
        var normalizedServerName = NormalizeName(serverName);
        foreach (Match match in ServerLinkRegex().Matches(html))
        {
            var candidateName = NormalizeName(WebUtility.HtmlDecode(match.Groups["name"].Value));
            if (string.Equals(candidateName, normalizedServerName, StringComparison.OrdinalIgnoreCase)
                && long.TryParse(
                    match.Groups["id"].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var serverId))
            {
                // BattleMetrics can retain duplicate/stale entries for the same endpoint. Its
                // search page ranks the current matching server first, so retain that ordering.
                return serverId;
            }
        }

        return null;
    }

    public static DateTimeOffset? GetNextMapWipeAtUtc(
        string html,
        DateTimeOffset checkedAtUtc)
    {
        var headingIndex = html.IndexOf(">Upcoming Wipes<", StringComparison.OrdinalIgnoreCase);
        if (headingIndex < 0)
        {
            return null;
        }

        var listStart = html.IndexOf("<ul", headingIndex, StringComparison.OrdinalIgnoreCase);
        var listEnd = listStart < 0
            ? -1
            : html.IndexOf("</ul>", listStart, StringComparison.OrdinalIgnoreCase);
        if (listStart < 0 || listEnd < 0)
        {
            return null;
        }

        var upcomingWipes = html[listStart..(listEnd + "</ul>".Length)];
        var candidates = new List<DateTimeOffset>();
        foreach (Match match in WipeTimeRegex().Matches(upcomingWipes))
        {
            var wipeType = WebUtility.HtmlDecode(StripTagsRegex().Replace(match.Groups["type"].Value, string.Empty));
            if (!wipeType.Contains("map", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dateTimeText = WebUtility.HtmlDecode(match.Groups["date"].Value);
            if (DateTimeOffset.TryParse(
                    dateTimeText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed)
                && parsed >= checkedAtUtc.AddMinutes(-5))
            {
                candidates.Add(parsed);
            }
        }

        return candidates.Count == 0 ? null : candidates.Min();
    }

    private static string NormalizeName(string value)
    {
        var withoutChangingWipeDetails = WipeDateRegex().Replace(value, " ");
        withoutChangingWipeDetails = WipeTimeOfDayRegex().Replace(withoutChangingWipeDetails, " ");
        withoutChangingWipeDetails = WipeStatusWordRegex().Replace(withoutChangingWipeDetails, " ");
        withoutChangingWipeDetails = NameSeparatorRegex().Replace(withoutChangingWipeDetails, " ");
        return WhitespaceRegex().Replace(withoutChangingWipeDetails, " ").Trim();
    }

    [GeneratedRegex(
        @"<li\b[^>]*>.*?<strong\b[^>]*>(?<type>.*?)</strong>.*?<time\b[^>]*\bdateTime=""(?<date>[^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex WipeTimeRegex();

    [GeneratedRegex(
        @"<[^>]+>",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex StripTagsRegex();

    [GeneratedRegex(
        @"<a\b[^>]*\btitle=""(?<name>[^""]+)""[^>]*\bhref=""/servers/rust/(?<id>\d+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex ServerLinkRegex();

    [GeneratedRegex(
        @"(?<![\p{L}\d])\d{1,2}[./-]\d{1,2}(?:[./-]\d{2,4})?(?![\p{L}\d])",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex WipeDateRegex();

    [GeneratedRegex(
        @"(?<!\d)\d{1,2}:\d{2}(?!\d)",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex WipeTimeOfDayRegex();

    [GeneratedRegex(
        @"\b(?:just\s+)?(?:full\s*)?wiped?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex WipeStatusWordRegex();

    [GeneratedRegex(
        @"[^\p{L}\p{N}]+",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex NameSeparatorRegex();

    [GeneratedRegex(
        @"\s+",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex WhitespaceRegex();
}
