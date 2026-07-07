using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Domain.Servers;

namespace RustIQ.Infrastructure.ServerQuery;

public sealed class SteamServerDiscoveryClient(
    IOptions<SteamDiscoveryOptions> options)
    : ISteamServerDiscoveryClient
{
    private readonly SteamDiscoveryOptions _options = options.Value;

    public async Task<IReadOnlyList<ConfiguredRustServer>> DiscoverServersAsync(
        int maxServers,
        CancellationToken cancellationToken)
    {
        using var udpClient = new UdpClient(AddressFamily.InterNetwork);
        udpClient.Client.ReceiveTimeout = _options.TimeoutMilliseconds;
        udpClient.Client.SendTimeout = _options.TimeoutMilliseconds;

        var endpoints = await QueryMasterServerAsync(
            udpClient,
            Math.Min(maxServers, _options.MaxServersPerRefresh),
            cancellationToken);

        var discoveredServers = new List<ConfiguredRustServer>(endpoints.Count);
        foreach (var endpoint in endpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();

            discoveredServers.Add(new ConfiguredRustServer
            {
                Host = endpoint.Address.ToString(),
                QueryPort = endpoint.Port,
                DiscoverySource = ServerDiscoverySource.Steam,
                SummarySource = ServerSummarySource.A2S,
                PlayerCountSource = ServerPlayerCountSource.A2S,
                PlayerListSource = PlayerListSource.A2S,
                BattleMetricsServerId = null
            });
        }

        return discoveredServers;
    }

    private async Task<IReadOnlyList<IPEndPoint>> QueryMasterServerAsync(
        UdpClient udpClient,
        int maxServers,
        CancellationToken cancellationToken)
    {
        var masterEndpoints = await GetMasterEndpointsAsync(cancellationToken);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Exception? lastException = null;

        foreach (var masterEndpoint in masterEndpoints)
        {
            var results = new List<IPEndPoint>();
            var seed = "0.0.0.0:0";

            try
            {
                while (results.Count < maxServers)
                {
                    var request = BuildRequest(seed, _options.Filter);
                    await udpClient.SendAsync(request, masterEndpoint, cancellationToken);

                    using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeoutCancellation.CancelAfter(_options.TimeoutMilliseconds);
                    var response = await udpClient.ReceiveAsync(timeoutCancellation.Token);
                    var parsedEndpoints = ParseResponse(response.Buffer);
                    if (parsedEndpoints.Count == 0)
                    {
                        break;
                    }

                    var reachedTerminator = false;
                    foreach (var endpoint in parsedEndpoints)
                    {
                        if (endpoint.Address.Equals(IPAddress.Any) && endpoint.Port == 0)
                        {
                            reachedTerminator = true;
                            break;
                        }

                        var key = $"{endpoint.Address}:{endpoint.Port}";
                        if (seen.Add(key))
                        {
                            results.Add(endpoint);
                            if (results.Count >= maxServers)
                            {
                                break;
                            }
                        }

                        seed = key;
                    }

                    if (reachedTerminator)
                    {
                        break;
                    }
                }

                if (results.Count > 0)
                {
                    return results;
                }
            }
            catch (Exception exception) when (exception is SocketException or OperationCanceledException)
            {
                lastException = exception;
            }
        }

        if (lastException is not null)
        {
            throw lastException;
        }

        return [];
    }

    private async Task<IReadOnlyList<IPEndPoint>> GetMasterEndpointsAsync(CancellationToken cancellationToken)
    {
        var endpoints = new List<IPEndPoint>();

        try
        {
            var masterAddresses = await Dns.GetHostAddressesAsync(_options.Host, cancellationToken);
            endpoints.AddRange(masterAddresses
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => new IPEndPoint(address, _options.Port)));
        }
        catch (SocketException)
        {
        }

        foreach (var fallback in _options.FallbackMasterServers)
        {
            if (TryParseEndpoint(fallback, out var endpoint))
            {
                endpoints.Add(endpoint);
            }
        }

        return endpoints
            .DistinctBy(endpoint => $"{endpoint.Address}:{endpoint.Port}")
            .ToArray();
    }

    private static bool TryParseEndpoint(string value, out IPEndPoint endpoint)
    {
        endpoint = null!;

        var separatorIndex = value.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= value.Length - 1)
        {
            return false;
        }

        var host = value[..separatorIndex];
        var portText = value[(separatorIndex + 1)..];
        return IPAddress.TryParse(host, out var address)
               && int.TryParse(portText, out var port)
               && port is > 0 and <= 65535
               && TryCreateEndpoint(address, port, out endpoint);
    }

    private static bool TryCreateEndpoint(IPAddress address, int port, out IPEndPoint endpoint)
    {
        endpoint = new IPEndPoint(address, port);
        return true;
    }

    private static byte[] BuildRequest(string seed, string filter)
    {
        var filterText = string.IsNullOrWhiteSpace(filter) ? string.Empty : filter;
        var payload = $"1{(char)0xFF}{seed}{(char)0x00}{filterText}{(char)0x00}";
        return Encoding.ASCII.GetBytes(payload);
    }

    private static IReadOnlyList<IPEndPoint> ParseResponse(byte[] buffer)
    {
        var results = new List<IPEndPoint>();
        if (buffer.Length < 6)
        {
            return results;
        }

        // Skip the leading header bytes and parse 6-byte endpoint tuples.
        for (var offset = 6; offset + 5 < buffer.Length; offset += 6)
        {
            var ip = new IPAddress(buffer.AsSpan(offset, 4));
            var port = (buffer[offset + 4] << 8) | buffer[offset + 5];
            results.Add(new IPEndPoint(ip, port));
        }

        return results;
    }
}
