using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RustIQ.Application.News;

namespace RustIQ.Infrastructure.News;

public sealed partial class FacepunchRustPatchNoteClient(HttpClient httpClient) : IRustPatchNoteClient
{
    private const int MaxPatchNotes = 10;

    public async Task<IReadOnlyList<RustPatchNoteDto>> GetLatestPatchNotesAsync(
        int count,
        CancellationToken cancellationToken)
    {
        var take = Math.Clamp(count, 1, MaxPatchNotes);

        using var response = await httpClient.GetAsync(
            "rss/news",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await XDocument.LoadAsync(responseStream, LoadOptions.None, cancellationToken);

        return document.Root?
            .Element("channel")?
            .Elements("item")
            .Select(ParsePatchNote)
            .Where(patchNote => patchNote is not null)
            .Cast<RustPatchNoteDto>()
            .OrderByDescending(patchNote => patchNote.PublishedAtUtc)
            .Take(take)
            .ToArray()
            ?? [];
    }

    private static RustPatchNoteDto? ParsePatchNote(XElement item)
    {
        var title = NormalizeText(item.Element("title")?.Value);
        var url = NormalizeText(item.Element("link")?.Value)
            ?? NormalizeText(item.Element("guid")?.Value);
        var rawDescription = item.Element("description")?.Value ?? string.Empty;
        var publishedAt = ParsePublishedAt(item.Element("pubDate")?.Value);

        if (string.IsNullOrWhiteSpace(title)
            || string.IsNullOrWhiteSpace(url)
            || publishedAt is null)
        {
            return null;
        }

        var decodedDescription = WebUtility.HtmlDecode(rawDescription);
        var imageUrl = ExtractImageUrl(decodedDescription);
        var summary = ExtractSummary(decodedDescription);

        return new RustPatchNoteDto(
            title,
            summary,
            url,
            imageUrl,
            publishedAt.Value);
    }

    private static DateTimeOffset? ParsePublishedAt(string? value)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var publishedAt)
            ? publishedAt
            : null;
    }

    private static string ExtractSummary(string description)
    {
        var withoutTags = HtmlTagRegex()
            .Replace(description, " ")
            .Replace('\u00a0', ' ');

        return NormalizeWhitespace(withoutTags);
    }

    private static string? ExtractImageUrl(string description)
    {
        var match = ImageSourceRegex().Match(description);

        return match.Success
            ? WebUtility.HtmlDecode(match.Groups["src"].Value)
            : null;
    }

    private static string? NormalizeText(string? value)
    {
        var normalized = NormalizeWhitespace(value ?? string.Empty);

        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }

    private static string NormalizeWhitespace(string value)
    {
        return WhitespaceRegex()
            .Replace(WebUtility.HtmlDecode(value), " ")
            .Trim();
    }

    [GeneratedRegex("<img\\s+[^>]*src=[\"'](?<src>[^\"']+)[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex ImageSourceRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
