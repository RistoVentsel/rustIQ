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

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
