using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Infrastructure.ServerQuery;

namespace RustIQ.Tests.ServerQuery;

public sealed class SteamServerDiscoveryClientTests
{
    private const string RustFilter = "\\appid\\252490";
    private const string ListStartSeed = "0.0.0.0:0";

    [Fact]
    public async Task DiscoverServersAsync_RequestsAllRegionsAndParsesEndpoints()
    {
        await using var master = new FakeMasterServer();
        master.EnqueueResponse(Endpoint("203.0.113.10", 28015), Endpoint("203.0.113.11", 28016), Terminator());
        var client = CreateClient(master.Port, maxServers: 100);

        var servers = await client.DiscoverServersAsync(100, CancellationToken.None);

        var request = Assert.Single(master.Requests);
        Assert.Equal(0x31, request[0]);

        // 0xFF means "all regions". This used to go out as 0x3F because the request was built as a
        // string and run through Encoding.ASCII, leaving the master no reason to return any servers.
        Assert.Equal(0xFF, request[1]);

        Assert.Equal(ListStartSeed, ReadNullTerminatedString(request, 2, out var filterOffset));
        Assert.Equal(RustFilter, ReadNullTerminatedString(request, filterOffset, out _));
        Assert.Equal(
            ["203.0.113.10:28015", "203.0.113.11:28016"],
            servers.Select(Describe));
    }

    [Fact]
    public async Task DiscoverServersAsync_ResumesFromWhereThePreviousRefreshStopped()
    {
        await using var master = new FakeMasterServer();
        master.EnqueueResponse(Endpoint("203.0.113.10", 28015), Endpoint("203.0.113.11", 28016));
        master.EnqueueResponse(Endpoint("203.0.113.12", 28017), Endpoint("203.0.113.13", 28018));
        var client = CreateClient(master.Port, maxServers: 2);

        var firstRefresh = await client.DiscoverServersAsync(2, CancellationToken.None);
        var secondRefresh = await client.DiscoverServersAsync(2, CancellationToken.None);

        Assert.Equal(2, master.Requests.Count);
        Assert.Equal(ListStartSeed, ReadNullTerminatedString(master.Requests[0], 2, out _));

        // Without this the master replays the same first page on every refresh, so servers past the
        // first MaxServersPerRefresh entries are never discovered.
        Assert.Equal("203.0.113.11:28016", ReadNullTerminatedString(master.Requests[1], 2, out _));
        Assert.Equal(["203.0.113.10:28015", "203.0.113.11:28016"], firstRefresh.Select(Describe));
        Assert.Equal(["203.0.113.12:28017", "203.0.113.13:28018"], secondRefresh.Select(Describe));
    }

    [Fact]
    public async Task DiscoverServersAsync_RestartsTheWalkAfterTheMasterReportsTheEndOfTheList()
    {
        await using var master = new FakeMasterServer();
        master.EnqueueResponse(Endpoint("203.0.113.10", 28015), Terminator());
        master.EnqueueResponse(Endpoint("203.0.113.10", 28015), Terminator());
        var client = CreateClient(master.Port, maxServers: 100);

        await client.DiscoverServersAsync(100, CancellationToken.None);
        await client.DiscoverServersAsync(100, CancellationToken.None);

        Assert.Equal(2, master.Requests.Count);
        Assert.Equal(ListStartSeed, ReadNullTerminatedString(master.Requests[1], 2, out _));
    }

    [Fact]
    public async Task DiscoverServersAsync_StopsWhenTheMasterKeepsReplayingTheSamePage()
    {
        await using var master = new FakeMasterServer();
        master.AlwaysRespondWith(Response([Endpoint("203.0.113.10", 28015)]));
        var client = CreateClient(master.Port, maxServers: 100);

        var servers = await client.DiscoverServersAsync(100, CancellationToken.None);

        // The seed stops advancing, so the walk has to give up instead of asking forever.
        Assert.Equal("203.0.113.10:28015", Describe(Assert.Single(servers)));
        Assert.Equal(2, master.Requests.Count);
    }

    private static string Describe(ConfiguredRustServer server)
    {
        return $"{server.Host}:{server.QueryPort}";
    }

    private static SteamServerDiscoveryClient CreateClient(int port, int maxServers)
    {
        return new SteamServerDiscoveryClient(Options.Create(new SteamDiscoveryOptions
        {
            Host = "127.0.0.1",
            Port = port,
            TimeoutMilliseconds = 2000,
            MaxServersPerRefresh = maxServers,
            RequestDelayMilliseconds = 0,
            Filter = RustFilter,
            FallbackMasterServers = []
        }));
    }

    private static byte[] Endpoint(string address, int port)
    {
        var addressBytes = IPAddress.Parse(address).GetAddressBytes();
        return
        [
            addressBytes[0],
            addressBytes[1],
            addressBytes[2],
            addressBytes[3],
            (byte)(port >> 8),
            (byte)(port & 0xFF)
        ];
    }

    private static byte[] Terminator()
    {
        return Endpoint("0.0.0.0", 0);
    }

    private static byte[] Response(IReadOnlyList<byte[]> endpoints)
    {
        var payload = new List<byte> { 0xFF, 0xFF, 0xFF, 0xFF, 0x66, 0x0A };
        foreach (var endpoint in endpoints)
        {
            payload.AddRange(endpoint);
        }

        return payload.ToArray();
    }

    private static string ReadNullTerminatedString(byte[] buffer, int offset, out int nextOffset)
    {
        var end = offset;
        while (end < buffer.Length && buffer[end] != 0x00)
        {
            end++;
        }

        nextOffset = end + 1;
        return Encoding.ASCII.GetString(buffer, offset, end - offset);
    }

    private sealed class FakeMasterServer : IAsyncDisposable
    {
        private readonly UdpClient _listener = new(new IPEndPoint(IPAddress.Loopback, 0));
        private readonly Queue<byte[]> _responses = new();
        private readonly List<byte[]> _requests = [];
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Task _receiveLoop;
        private byte[]? _standingResponse;

        public FakeMasterServer()
        {
            _receiveLoop = Task.Run(ReceiveLoopAsync);
        }

        public int Port => ((IPEndPoint)_listener.Client.LocalEndPoint!).Port;

        public IReadOnlyList<byte[]> Requests
        {
            get
            {
                lock (_requests)
                {
                    return _requests.ToArray();
                }
            }
        }

        public void EnqueueResponse(params byte[][] endpoints)
        {
            lock (_responses)
            {
                _responses.Enqueue(Response(endpoints));
            }
        }

        public void AlwaysRespondWith(byte[] response)
        {
            Volatile.Write(ref _standingResponse, response);
        }

        public async ValueTask DisposeAsync()
        {
            await _cancellation.CancelAsync();
            _listener.Dispose();

            try
            {
                await _receiveLoop;
            }
            catch (OperationCanceledException)
            {
            }

            _cancellation.Dispose();
        }

        private async Task ReceiveLoopAsync()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                UdpReceiveResult received;
                try
                {
                    received = await _listener.ReceiveAsync(_cancellation.Token);
                }
                catch (Exception exception)
                    when (exception is OperationCanceledException or SocketException or ObjectDisposedException)
                {
                    return;
                }

                lock (_requests)
                {
                    _requests.Add(received.Buffer);
                }

                var response = Volatile.Read(ref _standingResponse);
                if (response is null)
                {
                    lock (_responses)
                    {
                        if (_responses.Count == 0)
                        {
                            continue;
                        }

                        response = _responses.Dequeue();
                    }
                }

                await _listener.SendAsync(response, received.RemoteEndPoint, _cancellation.Token);
            }
        }
    }
}
