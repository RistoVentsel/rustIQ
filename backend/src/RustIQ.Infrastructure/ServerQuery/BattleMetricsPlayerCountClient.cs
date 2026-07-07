using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using RustIQ.Application.ServerQuery;

namespace RustIQ.Infrastructure.ServerQuery;

public sealed class BattleMetricsPlayerCountClient(IHttpClientFactory httpClientFactory)
    : IBattleMetricsPlayerCountClient
{
    public async Task<long?> FindServerIdAsync(
        string host,
        int queryPort,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("BattleMetricsApi");
        var requestUri = $"servers?filter[game]=rust&filter[search]={Uri.EscapeDataString(host)}&page[size]=10";
        using var response = await client.GetAsync(requestUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (!TryFindServer(payload, host, queryPort, out var server)
            || !server.TryGetProperty("attributes", out var attributes))
        {
            return null;
        }

        return GetLong(attributes, "id");
    }

    public async Task<BattleMetricsPlayerCountResult?> GetPlayerCountAsync(
        long battleMetricsServerId,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("BattleMetricsApi");
        using var response = await client.GetAsync($"servers/{battleMetricsServerId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (!TryGetServerAttributes(payload, out var attributes))
        {
            return null;
        }

        var status = GetString(attributes, "status");
        return new BattleMetricsPlayerCountResult(
            battleMetricsServerId,
            GetInt(attributes, "players") ?? 0,
            GetInt(attributes, "maxPlayers") ?? 0,
            string.Equals(status, "online", StringComparison.OrdinalIgnoreCase),
            DateTimeOffset.UtcNow);
    }

    private static bool TryFindServer(
        JsonElement payload,
        string host,
        int queryPort,
        out JsonElement server)
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
            var candidateQueryPort = GetInt(attributes, "portQuery");
            if (string.Equals(ip, host, StringComparison.OrdinalIgnoreCase) && candidateQueryPort == queryPort)
            {
                server = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetServerAttributes(JsonElement payload, out JsonElement attributes)
    {
        attributes = default;
        return payload.TryGetProperty("data", out var data)
               && data.ValueKind == JsonValueKind.Object
               && data.TryGetProperty("attributes", out attributes);
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
