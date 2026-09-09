using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Domain.Servers;

namespace RustIQ.Infrastructure.ServerQuery;

/// <summary>
/// Discovers Rust servers through the Steam Web API.
/// </summary>
/// <remarks>
/// This replaces the UDP master server query. hl2master.steampowered.com stopped resolving and the
/// legacy master addresses answer on neither 27011 nor 27015, so that protocol has no server behind
/// it any more. IGameServersService/GetServerList is Valve's replacement: one HTTPS call returns the
/// whole filtered list, which also removes the paging, rate limiting and UDP exposure that came with
/// the master server.
/// </remarks>
public sealed class SteamWebApiServerDiscoveryClient(
    IHttpClientFactory httpClientFactory,
    IOptions<SteamDiscoveryOptions> options)
    : ISteamServerDiscoveryClient
{
    public const string HttpClientName = "SteamWebApi";

    private readonly SteamDiscoveryOptions _options = options.Value;

    public async Task<IReadOnlyList<ConfiguredRustServer>> DiscoverServersAsync(
        int maxServers,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Steam discovery needs an API key. Create one at https://steamcommunity.com/dev/apikey "
                + "and set SteamDiscovery:ApiKey.");
        }

        var limit = Math.Max(1, Math.Min(maxServers, _options.MaxServersPerRefresh));
        var requestUri = "IGameServersService/GetServerList/v1/"
            + $"?key={Uri.EscapeDataString(_options.ApiKey.Trim())}"
            + $"&filter={Uri.EscapeDataString(_options.Filter)}"
            + $"&limit={limit.ToString(CultureInfo.InvariantCulture)}";

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(requestUri, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException(
                "Steam rejected the discovery API key. Check SteamDiscovery:ApiKey.");
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        if (!payload.TryGetProperty("response", out var responseElement)
            || !responseElement.TryGetProperty("servers", out var serversElement)
            || serversElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var discoveredServers = new List<ConfiguredRustServer>(serversElement.GetArrayLength());
        var seenEndpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var server in serversElement.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var configuredServer = ToConfiguredServer(server);
            if (configuredServer is null)
            {
                continue;
            }

            // Steam lists a server once per endpoint, but proxies and relisted servers can repeat one.
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
        // "addr" is the address to query, which is the one RustIQ polls over A2S. "gameport" is what
        // players connect to and is kept only for display.
        if (!TryReadEndpoint(GetString(server, "addr"), out var host, out var queryPort))
        {
            return null;
        }

        return new ConfiguredRustServer
        {
            Host = host,
            QueryPort = queryPort,
            GamePort = GetInt(server, "gameport"),
            Name = NullIfWhiteSpace(GetString(server, "name")),
            Map = NullIfWhiteSpace(GetString(server, "map")),

            // Steam calls the A2S keyword string "gametype". Rust puts its live counts in there, and
            // the queue depth is the one field worth carrying before the first poll.
            QueuePlayers = RustServerKeywordParser.GetQueuePlayers(GetString(server, "gametype")),

            DiscoverySource = ServerDiscoverySource.Steam,
            SummarySource = ServerSummarySource.A2S,
            PlayerCountSource = ServerPlayerCountSource.A2S,
            PlayerListSource = PlayerListSource.A2S,
            BattleMetricsServerId = null
        };
    }

    private static bool TryReadEndpoint(string? address, out string host, out int queryPort)
    {
        host = string.Empty;
        queryPort = 0;

        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var separatorIndex = address.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= address.Length - 1)
        {
            return false;
        }

        var candidateHost = address[..separatorIndex].Trim();
        if (candidateHost.Length == 0
            || !int.TryParse(
                address[(separatorIndex + 1)..],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var candidatePort)
            || candidatePort is <= 0 or > 65535)
        {
            return false;
        }

        host = candidateHost;
        queryPort = candidatePort;
        return true;
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

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
