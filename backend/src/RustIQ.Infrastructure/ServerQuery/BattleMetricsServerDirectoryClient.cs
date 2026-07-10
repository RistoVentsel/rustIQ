using System.Globalization;
using System.Net;
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
        IReadOnlyList<BattleMetricsServerSummary> summaries;
        try
        {
            summaries = await GetRustServerSummariesAsync(pageSize, maxPages, cancellationToken);
        }
        catch (BattleMetricsRateLimitException exception)
        {
            summaries = exception.PartialSummaries;
        }

        return summaries.Select(ToConfiguredServer)
            .ToArray();
    }

    public async Task<IReadOnlyList<BattleMetricsServerSummary>> GetRustServerSummariesAsync(
        int pageSize,
        int maxPages,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("BattleMetricsApi");
        var results = new List<BattleMetricsServerSummary>();
        string? requestUri = $"servers?filter[game]=rust&page[size]={pageSize}&sort=rank";
        var checkedAtUtc = DateTimeOffset.UtcNow;

        for (var pageIndex = 0; pageIndex < maxPages && requestUri is not null; pageIndex++)
        {
            using var response = await client.GetAsync(requestUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new BattleMetricsRateLimitException(
                    results.ToArray(),
                    response.Headers.RetryAfter?.ToString());
            }

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
                var battleMetricsServerId = GetLong(server, "id");
                if (battleMetricsServerId is null
                    || string.IsNullOrWhiteSpace(host)
                    || queryPort is null)
                {
                    continue;
                }

                var status = GetString(attributes, "status");
                results.Add(new BattleMetricsServerSummary(
                    battleMetricsServerId.Value,
                    GetString(attributes, "name"),
                    GetString(attributes, "address"),
                    host,
                    GetInt(attributes, "port"),
                    queryPort.Value,
                    GetString(attributes, "country"),
                    GetLatitude(attributes),
                    GetLongitude(attributes),
                    BattleMetricsRustWipeParser.GetLastWipeAtUtc(attributes, checkedAtUtc),
                    BattleMetricsRustWipeParser.GetNextWipeAtUtc(attributes, checkedAtUtc),
                    GetInt(attributes, "players") ?? 0,
                    GetInt(attributes, "maxPlayers") ?? 0,
                    GetInt(attributes, "details", "rust_queued_players"),
                    GetServerType(attributes),
                    string.Equals(status, "online", StringComparison.OrdinalIgnoreCase),
                    checkedAtUtc));
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

    private static int? GetInt(
        JsonElement element,
        string objectPropertyName,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(objectPropertyName, out var nestedElement))
        {
            return null;
        }

        return GetInt(nestedElement, propertyName);
    }

    private static string? GetString(
        JsonElement element,
        string objectPropertyName,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(objectPropertyName, out var nestedElement))
        {
            return null;
        }

        return GetString(nestedElement, propertyName);
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

    private static RustServerType GetServerType(JsonElement attributes)
    {
        var rustType = GetString(attributes, "details", "rust_type");

        return rustType?.Trim().ToLowerInvariant() switch
        {
            "official" => RustServerType.Official,
            "community" => RustServerType.Community,
            "modded" => RustServerType.Modded,
            _ => RustServerType.Unknown
        };
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static ConfiguredRustServer ToConfiguredServer(BattleMetricsServerSummary summary)
    {
        return new ConfiguredRustServer
        {
            BattleMetricsServerId = summary.BattleMetricsServerId,
            Name = summary.Name,
            Address = summary.Address,
            Host = summary.Host,
            GamePort = summary.GamePort,
            QueryPort = summary.QueryPort,
            Country = summary.Country,
            Latitude = summary.Latitude,
            Longitude = summary.Longitude,
            LastWipeAtUtc = summary.LastWipeAtUtc,
            NextWipeAtUtc = summary.NextWipeAtUtc,
            QueuePlayers = summary.QueuePlayers,
            ServerType = summary.ServerType,
            DiscoverySource = ServerDiscoverySource.BattleMetrics,
            SummarySource = ServerSummarySource.BattleMetrics,
            PlayerCountSource = ServerPlayerCountSource.BattleMetrics,
            PlayerListSource = PlayerListSource.A2S
        };
    }
}
