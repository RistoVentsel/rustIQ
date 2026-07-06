using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RustIQ.Application.Maps;
using RustIQ.Application.ServerQuery;

namespace RustIQ.Infrastructure.Maps;

public sealed class BattleMetricsRustServerMapMetadataClient(
    HttpClient httpClient,
    IOptions<RustServerQueryOptions> options)
    : IRustServerMapMetadataClient
{
    private const string SourceName = "BattleMetrics";

    private readonly RustServerQueryOptions _options = options.Value;

    public async Task<RustServerMapMetadata> GetMapMetadataAsync(CancellationToken cancellationToken)
    {
        var fetchedAtUtc = DateTimeOffset.UtcNow;

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(TimeSpan.FromMilliseconds(_options.TimeoutMilliseconds));

        try
        {
            var requestUri = BuildRequestUri();
            using var response = await httpClient.GetAsync(requestUri, timeoutCancellation.Token);

            if (!response.IsSuccessStatusCode)
            {
                return Unavailable(
                    fetchedAtUtc,
                    $"BattleMetrics returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: timeoutCancellation.Token);

            if (!TryFindConfiguredServer(payload, out var server))
            {
                return Unavailable(fetchedAtUtc, "BattleMetrics did not return metadata for the configured server.");
            }

            if (!server.TryGetProperty("attributes", out var attributes))
            {
                return Unavailable(fetchedAtUtc, "BattleMetrics server response did not include attributes.");
            }

            if (!attributes.TryGetProperty("details", out var details))
            {
                return Unavailable(fetchedAtUtc, "BattleMetrics server response did not include Rust details.");
            }

            var rustMaps = details.TryGetProperty("rust_maps", out var rustMapsElement)
                ? rustMapsElement
                : default;

            var mapName = GetString(details, "map");
            var seed = GetInt(details, "rust_world_seed");
            var size = GetInt(details, "rust_world_size");
            var rustMapsUrl = GetString(rustMaps, "url");
            var thumbnailUrl = GetString(rustMaps, "thumbnailUrl");
            var mapFileUrl = GetString(rustMaps, "mapUrl");
            var sourceUpdatedAtUtc = GetDateTimeOffset(rustMaps, "updatedAt");

            var error = rustMapsUrl is null && thumbnailUrl is null
                ? "BattleMetrics did not include RustMaps links for this server."
                : null;

            return new RustServerMapMetadata(
                _options.Host,
                _options.QueryPort,
                mapName,
                seed,
                size,
                rustMapsUrl,
                thumbnailUrl,
                mapFileUrl,
                SourceName,
                sourceUpdatedAtUtc,
                fetchedAtUtc,
                error);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Unavailable(DateTimeOffset.UtcNow, exception.Message);
        }
    }

    private string BuildRequestUri()
    {
        var encodedHost = Uri.EscapeDataString(_options.Host);

        return $"servers?filter[game]=rust&filter[search]={encodedHost}&page[size]=10";
    }

    private bool TryFindConfiguredServer(JsonElement payload, out JsonElement server)
    {
        server = default;

        if (!payload.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var candidate in data.EnumerateArray())
        {
            if (!candidate.TryGetProperty("attributes", out var attributes))
            {
                continue;
            }

            var ip = GetString(attributes, "ip");
            var queryPort = GetInt(attributes, "portQuery");

            if (string.Equals(ip, _options.Host, StringComparison.OrdinalIgnoreCase)
                && queryPort == _options.QueryPort)
            {
                server = candidate;
                return true;
            }
        }

        return false;
    }

    private RustServerMapMetadata Unavailable(DateTimeOffset fetchedAtUtc, string error)
    {
        return new RustServerMapMetadata(
            _options.Host,
            _options.QueryPort,
            MapName: null,
            Seed: null,
            Size: null,
            RustMapsUrl: null,
            ThumbnailUrl: null,
            MapFileUrl: null,
            SourceName,
            SourceUpdatedAtUtc: null,
            fetchedAtUtc,
            error);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? NullIfWhiteSpace(property.GetString())
            : NullIfWhiteSpace(property.ToString());
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        var text = property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, string propertyName)
    {
        var text = GetString(element, propertyName);

        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
