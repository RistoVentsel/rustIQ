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
            var searchHtml = await pageFetcher.SearchRustServersAsync(
                configuredServer.Name,
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
        var candidates = new HashSet<long>();
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
                candidates.Add(serverId);
            }
        }

        return candidates.Count == 1 ? candidates.Single() : null;
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
        return WhitespaceRegex().Replace(value, " ").Trim();
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
        @"\s+",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex WhitespaceRegex();
}
