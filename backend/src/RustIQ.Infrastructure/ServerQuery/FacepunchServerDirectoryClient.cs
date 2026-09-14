using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Domain.Servers;

namespace RustIQ.Infrastructure.ServerQuery;

/// <summary>
/// Reads Facepunch's own public Rust server list.
/// </summary>
/// <remarks>
/// This is the list the Rust client seeds its server browser from, and it is the only free source
/// that carries where a server actually is. Neither of the alternatives does: Steam's
/// IGameServersService/GetServerList returns no country and no coordinates, and A2S has nowhere to
/// put them, so every server discovered through those two showed its location as unknown. Country
/// used to arrive from BattleMetrics, which is now answering 403 to anonymous callers.
///
/// It needs no API key, and one response covers every listed server, so it also runs as a discovery
/// source in its own right rather than only as an enrichment pass.
/// </remarks>
public sealed class FacepunchServerDirectoryClient(
    IHttpClientFactory httpClientFactory,
    IOptions<FacepunchDiscoveryOptions> options)
    : IFacepunchServerDirectoryClient
{
    public const string HttpClientName = "FacepunchServerList";

    private const string RequestUri = "api/public/serverlist";

    private readonly FacepunchDiscoveryOptions _options = options.Value;

    public async Task<IReadOnlyList<ConfiguredRustServer>> GetRustServersAsync(
        int maxServers,
        CancellationToken cancellationToken)
    {
        var limit = Math.Max(1, Math.Min(maxServers, _options.MaxServersPerRefresh));

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(RequestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (!payload.TryGetProperty("Servers", out var serversElement)
            || serversElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var discoveredServers = new List<ConfiguredRustServer>(
            Math.Min(limit, serversElement.GetArrayLength()));
        var seenEndpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var server in serversElement.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (discoveredServers.Count >= limit)
            {
                break;
            }

            var configuredServer = ToConfiguredServer(server);
            if (configuredServer is null)
            {
                continue;
            }

            var endpointKey = $"{configuredServer.Host}:{configuredServer.QueryPort}";
            if (seenEndpoints.Add(endpointKey))
            {
                discoveredServers.Add(configuredServer);
            }
        }

        return discoveredServers;
    }

    private static ConfiguredRustServer? ToConfiguredServer(JsonElement server)
    {
        var host = NullIfWhiteSpace(GetString(server, "ip"));
        var queryPort = GetInt(server, "query_port");
        if (host is null || queryPort is not (> 0 and <= 65535))
        {
            return null;
        }

        var keywords = GetString(server, "tags");
        var name = NullIfWhiteSpace(GetString(server, "hostname"));

        return new ConfiguredRustServer
        {
            Host = host,
            QueryPort = queryPort.Value,
            GamePort = GetInt(server, "port"),
            Name = name,
            Map = NullIfWhiteSpace(GetString(server, "map")),

            // "loc" is an ISO 3166-1 alpha-2 code, which is the shape the rest of RustIQ stores and
            // the frontend turns into a flag.
            Country = GetCountry(server),
            Latitude = GetDouble(server, "la"),
            Longitude = GetDouble(server, "lo"),

            QueuePlayers = RustServerKeywordParser.GetQueuePlayers(keywords),
            ServerType = RustServerTypeParser.GetServerType(name, keywords),

            DiscoverySource = ServerDiscoverySource.Facepunch,

            // The list carries a player count, but it is a cached one. A2S stays the summary and
            // player count source so the numbers keep coming from the server itself.
            SummarySource = ServerSummarySource.A2S,
            PlayerCountSource = ServerPlayerCountSource.A2S,
            PlayerListSource = PlayerListSource.A2S,
            BattleMetricsServerId = null
        };
    }

    private static string? GetCountry(JsonElement server)
    {
        var country = GetString(server, "loc")?.Trim();

        return country?.Length == 2
            ? country.ToUpperInvariant()
            : null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null
        };
    }

    private static double? GetDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        var coordinate = value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var number) => number,
            JsonValueKind.String when double.TryParse(
                value.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => (double?)null
        };

        // A server the list has no fix for reports 0,0, which is a point in the Atlantic rather than
        // a location.
        return coordinate is 0 or null ? null : coordinate;
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
