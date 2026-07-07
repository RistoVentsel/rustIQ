using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using RustIQ.Application.ServerQuery;
using RustIQ.Domain.Servers;

namespace RustIQ.Infrastructure.ServerQuery;

public sealed class BattleMetricsServerDirectoryClient(IHttpClientFactory httpClientFactory)
    : IBattleMetricsServerDirectoryClient
{
    public async Task<IReadOnlyList<ConfiguredRustServer>> GetRustServersAsync(
        int pageSize,
        int maxPages,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("BattleMetricsApi");
        var results = new List<ConfiguredRustServer>();
        string? requestUri = $"servers?filter[game]=rust&page[size]={pageSize}&sort=rank";

        for (var pageIndex = 0; pageIndex < maxPages && requestUri is not null; pageIndex++)
        {
            using var response = await client.GetAsync(requestUri, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (!payload.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            foreach (var server in data.EnumerateArray())
            {
                if (!server.TryGetProperty("attributes", out var attributes))
                {
                    continue;
                }

                var host = GetString(attributes, "ip");
                var queryPort = GetInt(attributes, "portQuery");
                if (string.IsNullOrWhiteSpace(host) || queryPort is null)
                {
                    continue;
                }

                results.Add(new ConfiguredRustServer
                {
                    BattleMetricsServerId = GetLong(attributes, "id"),
                    Name = GetString(attributes, "name"),
                    Address = GetString(attributes, "address"),
                    Host = host,
                    GamePort = GetInt(attributes, "port"),
                    QueryPort = queryPort.Value,
                    Country = GetString(attributes, "country"),
                    Latitude = GetLatitude(attributes),
                    Longitude = GetLongitude(attributes),
                    LastWipeAtUtc = GetDateTimeOffset(attributes, "details", "rust_last_wipe"),
                    NextWipeAtUtc = GetDateTimeOffset(attributes, "details", "rust_next_wipe"),
                    DiscoverySource = ServerDiscoverySource.BattleMetrics,
                    SummarySource = ServerSummarySource.BattleMetrics,
                    PlayerCountSource = ServerPlayerCountSource.BattleMetrics,
                    PlayerListSource = PlayerListSource.A2S
                });
            }

            requestUri = payload.TryGetProperty("links", out var links)
                && links.TryGetProperty("next", out var nextLink)
                && nextLink.ValueKind == JsonValueKind.String
                    ? nextLink.GetString()
                    : null;
        }

        return results
            .GroupBy(server => $"{server.Host.Trim().ToLowerInvariant()}:{server.QueryPort}")
            .Select(group => group.First())
            .ToArray();
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

    private static long? GetLong(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var number))
        {
            return number;
        }

        var text = property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();

        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static double? GetLatitude(JsonElement attributes)
    {
        return GetLocationCoordinate(attributes, index: 1);
    }

    private static double? GetLongitude(JsonElement attributes)
    {
        return GetLocationCoordinate(attributes, index: 0);
    }

    private static double? GetLocationCoordinate(JsonElement attributes, int index)
    {
        if (attributes.ValueKind != JsonValueKind.Object
            || !attributes.TryGetProperty("location", out var location)
            || location.ValueKind != JsonValueKind.Array
            || location.GetArrayLength() <= index)
        {
            return null;
        }

        var coordinate = location[index];
        if (coordinate.ValueKind == JsonValueKind.Number && coordinate.TryGetDouble(out var number))
        {
            return number;
        }

        var text = coordinate.ValueKind == JsonValueKind.String ? coordinate.GetString() : coordinate.ToString();

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static DateTimeOffset? GetDateTimeOffset(
        JsonElement element,
        string objectPropertyName,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(objectPropertyName, out var nestedElement))
        {
            return null;
        }

        var text = GetString(nestedElement, propertyName);

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
