using System.Diagnostics;
using Microsoft.Extensions.Options;
using RustIQ.Application.ServerQuery;
using RustIQ.Infrastructure.ServerQuery;
using RustIQ.Tests.TestDoubles;

namespace RustIQ.Tests.ServerQuery;

public sealed class SteamRustServerQueryClientTests
{
    [Fact]
    public async Task QueryServerAsync_CompletesTheChallengeHandshake()
    {
        using var server = new FakeA2sServer { RequireChallenge = true };
        var client = CreateClient();

        var result = await client.QueryServerAsync(ConfiguredServer(server.Port), CancellationToken.None);

        // The replaced A2S package sent one A2S_INFO and gave up on the challenge, so a server behaving
        // like this was recorded as offline.
        Assert.True(result.IsOnline);
        Assert.Equal("Fake Rust Server", result.Name);
        Assert.Equal("Procedural Map", result.Map);
        Assert.Equal(2, server.RequestCount);
    }

    [Fact]
    public async Task QueryServerAsync_AnswersServersThatSkipTheChallenge()
    {
        using var server = new FakeA2sServer { RequireChallenge = false };
        var client = CreateClient();

        var result = await client.QueryServerAsync(ConfiguredServer(server.Port), CancellationToken.None);

        Assert.True(result.IsOnline);
        Assert.Equal(1, server.RequestCount);
    }

    [Fact]
    public async Task QueryServerAsync_PrefersKeywordCountsOverTheSingleByteFields()
    {
        // Rust reports counts above 255 in its keywords, because the A2S fields are single bytes.
        using var server = new FakeA2sServer
        {
            AdvertisedPlayers = 255,
            AdvertisedMaxPlayers = 255,
            Keywords = "mp500,cp420,qp12"
        };
        var client = CreateClient();

        var result = await client.QueryServerAsync(ConfiguredServer(server.Port), CancellationToken.None);

        Assert.Equal(420, result.Players);
        Assert.Equal(500, result.MaxPlayers);
        Assert.Equal(12, result.QueuePlayers);
    }

    [Fact]
    public async Task QueryServerAsync_FallsBackToTheSingleByteFieldsWithoutKeywords()
    {
        using var server = new FakeA2sServer
        {
            AdvertisedPlayers = 17,
            AdvertisedMaxPlayers = 50,
            Keywords = null
        };
        var client = CreateClient();

        var result = await client.QueryServerAsync(ConfiguredServer(server.Port), CancellationToken.None);

        Assert.True(result.IsOnline);
        Assert.Equal(17, result.Players);
        Assert.Equal(50, result.MaxPlayers);
    }

    [Fact]
    public async Task QueryServerAsync_RecoversFromADroppedPacket()
    {
        using var server = new FakeA2sServer { RequireChallenge = false, DropFirstRequests = 1 };
        var client = CreateClient();

        var result = await client.QueryServerAsync(ConfiguredServer(server.Port), CancellationToken.None);

        // One lost datagram must not read as "this server is down".
        Assert.True(result.IsOnline);
        Assert.Equal(2, server.RequestCount);
    }

    [Fact]
    public async Task QueryServerAsync_ReportsOfflineWithinTheAttemptBudgetForASilentServer()
    {
        using var server = new FakeA2sServer { Silent = true };
        var client = CreateClient(timeoutMilliseconds: 300, maxQueryAttempts: 2);

        var stopwatch = Stopwatch.StartNew();
        var result = await client.QueryServerAsync(ConfiguredServer(server.Port), CancellationToken.None);
        stopwatch.Stop();

        Assert.False(result.IsOnline);
        Assert.Equal(0, result.Players);

        // The whole point of owning the socket: the query is actually bounded. The A2S package this
        // replaced ignored its timeout argument and was measured still blocking after 45 seconds,
        // leaking the thread it was running on for every unreachable server.
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Query took {stopwatch.Elapsed}, which means it is not bounded by the timeout.");
    }

    [Fact]
    public async Task QueryServerAsync_HonoursCancellationInsteadOfReportingOffline()
    {
        using var server = new FakeA2sServer { Silent = true };
        var client = CreateClient(timeoutMilliseconds: 10_000);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Shutdown has to surface as cancellation, not as every server suddenly being offline.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.QueryServerAsync(ConfiguredServer(server.Port), cancellation.Token));
    }

    [Fact]
    public async Task QueryPlayersAsync_ReadsThePlayerListThroughTheChallengeHandshake()
    {
        using var server = new FakeA2sServer
        {
            RequireChallenge = true,
            Players = ["alpha", "bravo", "charlie"]
        };
        var client = CreateClient();

        var players = await client.QueryPlayersAsync(ConfiguredServer(server.Port), CancellationToken.None);

        Assert.Equal(["alpha", "bravo", "charlie"], players.Select(player => player.Name));
        Assert.Equal([60d, 120d, 180d], players.Select(player => player.DurationSeconds));
    }

    [Fact]
    public async Task QueryPlayersAsync_ReassemblesASplitPlayerList()
    {
        // A populated Rust server's player list does not fit in one datagram.
        using var server = new FakeA2sServer
        {
            RequireChallenge = true,
            SplitPlayerResponse = true,
            Players = ["alpha", "bravo", "charlie", "delta", "echo"]
        };
        var client = CreateClient();

        var players = await client.QueryPlayersAsync(ConfiguredServer(server.Port), CancellationToken.None);

        Assert.Equal(["alpha", "bravo", "charlie", "delta", "echo"], players.Select(player => player.Name));
    }

    [Fact]
    public async Task QueryPlayersAsync_ReturnsEmptyWhenTheServerWillNotAnswer()
    {
        using var server = new FakeA2sServer { Silent = true };
        var client = CreateClient(timeoutMilliseconds: 300, maxQueryAttempts: 2);

        var players = await client.QueryPlayersAsync(ConfiguredServer(server.Port), CancellationToken.None);

        Assert.Empty(players);
    }

    private static SteamRustServerQueryClient CreateClient(
        int timeoutMilliseconds = 2000,
        int maxQueryAttempts = 2)
    {
        return new SteamRustServerQueryClient(Options.Create(new RustServerQueryOptions
        {
            TimeoutMilliseconds = timeoutMilliseconds,
            MaxQueryAttempts = maxQueryAttempts
        }));
    }

    private static ConfiguredRustServer ConfiguredServer(int queryPort)
    {
        return new ConfiguredRustServer
        {
            Host = "127.0.0.1",
            QueryPort = queryPort
        };
    }
}
