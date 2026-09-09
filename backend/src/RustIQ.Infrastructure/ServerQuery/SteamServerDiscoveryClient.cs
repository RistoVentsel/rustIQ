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
    private const byte MasterServerQueryOpcode = 0x31;
    private const byte AllRegionsByte = 0xFF;
    private const byte StringTerminator = 0x00;
    private const string ListStartSeed = "0.0.0.0:0";

    private readonly SteamDiscoveryOptions _options = options.Value;

    // The master server pages by "give me the servers listed after this address", so one refresh only
    // ever sees MaxServersPerRefresh entries. Remembering where the previous refresh stopped lets
    // successive refreshes walk the whole list without making any single refresh more expensive. It
    // restarts once the master reports the end of the list, so newly listed servers get picked up on
    // the next pass. Only the discovery worker calls this, one refresh at a time.
    private string _resumeSeed = ListStartSeed;

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
        Exception? lastException = null;

        foreach (var masterEndpoint in masterEndpoints)
        {
            // Scoped per master server: falling back to another master has to start from a clean
            // slate, otherwise endpoints get dropped as "already seen" from a master that then failed.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var results = new List<IPEndPoint>();
            var seed = _resumeSeed;
            var reachedEndOfList = false;

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

                    var newEndpointsFromResponse = 0;
                    foreach (var endpoint in parsedEndpoints)
                    {
                        if (endpoint.Address.Equals(IPAddress.Any) && endpoint.Port == 0)
                        {
                            reachedEndOfList = true;
                            break;
                        }

                        var key = $"{endpoint.Address}:{endpoint.Port}";
                        seed = key;

                        if (seen.Add(key))
                        {
                            results.Add(endpoint);
                            newEndpointsFromResponse++;
                            if (results.Count >= maxServers)
                            {
                                break;
                            }
                        }
                    }

                    if (reachedEndOfList)
                    {
                        break;
                    }

                    // A page that only repeats endpoints we already hold means the seed is no longer
                    // advancing the master's cursor. Stop rather than request the same page forever.
                    if (newEndpointsFromResponse == 0)
                    {
                        break;
                    }

                    if (_options.RequestDelayMilliseconds > 0)
                    {
                        await Task.Delay(_options.RequestDelayMilliseconds, cancellationToken);
                    }
                }

                if (results.Count > 0)
                {
                    _resumeSeed = reachedEndOfList ? ListStartSeed : seed;
                    return results;
                }

                // Nothing usable came back for this slice, so restart the walk on the next refresh
                // instead of parking on a seed the master will not advance past.
                _resumeSeed = ListStartSeed;
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

        // The region byte has to be written as a raw byte. Building this as a string and running it
        // through Encoding.ASCII replaced 0xFF ("all regions") with '?' (0x3F), which is not a valid
        // region, so the master server had no reason to return any servers at all.
        var request = new List<byte>(seed.Length + filterText.Length + 4)
        {
            MasterServerQueryOpcode,
            AllRegionsByte
        };

        request.AddRange(Encoding.ASCII.GetBytes(seed));
        request.Add(StringTerminator);
        request.AddRange(Encoding.ASCII.GetBytes(filterText));
        request.Add(StringTerminator);

        return request.ToArray();
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
