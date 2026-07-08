using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using RustIQ.Application.ServerQuery;
using RustIQ.Domain.Servers;

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

        return GetLong(server, "id");
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
            GetInt(attributes, "details", "rust_queued_players"),
            GetServerType(attributes),
            string.Equals(status, "online", StringComparison.OrdinalIgnoreCase),
            GetString(attributes, "country"),
            GetLatitude(attributes),
            GetLongitude(attributes),
            GetDateTimeOffset(attributes, "details", "rust_last_wipe"),
            GetDateTimeOffset(attributes, "details", "rust_next_wipe"),
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
}
