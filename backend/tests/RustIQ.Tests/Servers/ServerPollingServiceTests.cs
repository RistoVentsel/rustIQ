using RustIQ.Application.Maps;
using RustIQ.Application.ServerQuery;
using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;
using RustIQ.Tests.TestDoubles;

namespace RustIQ.Tests.Servers;

public sealed class ServerPollingServiceTests
{
    [Fact]
    public async Task PollConfiguredServerAsync_StoresLatestStateSnapshotMapMetadataAndCurrentPlayers()
    {
        var checkedAtUtc = DateTimeOffset.Parse("2026-07-06T12:00:00Z");
        var fetchedAtUtc = DateTimeOffset.Parse("2026-07-06T12:00:01Z");
        var repository = new InMemoryServerRepository();
        var queryClient = new FakeRustServerQueryClient(
            new RustServerQueryResult(
                "127.0.0.1",
                28015,
                "RustIQ Test Server",
                "Procedural Map",
                2,
                100,
                true,
                42,
                checkedAtUtc,
                null),
            [
                new RustServerPlayerResult("Alice", 10, 120),
                new RustServerPlayerResult("Bob", 5, 60),
                new RustServerPlayerResult(null, 0, 1)
            ]);
        var mapClient = new FakeRustServerMapMetadataClient(
            new RustServerMapMetadata(
                "127.0.0.1",
                28015,
                "Moose Maps",
                1337,
                4500,
                "https://rustmaps.com/map/test",
                "https://content.rustmaps.com/thumbnail.webp",
                "https://maps.rustmaps.com/test.map",
                "BattleMetrics",
                fetchedAtUtc,
                fetchedAtUtc,
                null));
        var service = new ServerPollingService(queryClient, mapClient, repository);

        var result = await service.PollConfiguredServerAsync(CancellationToken.None);

        var server = Assert.Single(repository.Servers);
        var snapshot = Assert.Single(repository.Snapshots);
        Assert.Equal(server.Id, result.Id);
        Assert.Equal("RustIQ Test Server", server.Name);
        Assert.Equal(ServerStatus.Online, server.Status);
        Assert.Equal(2, server.Players);
        Assert.Equal(100, server.MaxPlayers);
        Assert.Equal(checkedAtUtc, server.LastSeenAtUtc);
        Assert.Equal(checkedAtUtc, server.LastCheckedAtUtc);
        Assert.Equal(server.Id, snapshot.ServerId);
        Assert.True(snapshot.IsOnline);
        Assert.Equal(42, snapshot.PingMilliseconds);
        Assert.Equal("Moose Maps", server.MapMetadata?.MapName);
        Assert.Equal(1337, server.MapMetadata?.Seed);
        Assert.Equal(4500, server.MapMetadata?.Size);
        Assert.Equal("https://rustmaps.com/map/test", server.MapMetadata?.RustMapsUrl);
        Assert.Equal(2, repository.CurrentPlayers.Count);
        Assert.DoesNotContain(repository.CurrentPlayers, player => player.PlayerName is null);
        Assert.Equal("Moose Maps", result.MapMetadata?.MapName);
        Assert.Equal(2, result.CurrentPlayers.Count);
    }

    [Fact]
    public async Task PollConfiguredServerAsync_DoesNotImmediatelyMarkServerOfflineAfterOneFailedQuery()
    {
        var firstCheck = DateTimeOffset.Parse("2026-07-06T12:00:00Z");
        var failedCheck = firstCheck.AddSeconds(30);
        var repository = new InMemoryServerRepository();
        var queryClient = new FakeRustServerQueryClient(
            new RustServerQueryResult("example.test", 28015, "Online", "Procedural Map", 12, 100, true, 10, firstCheck, null),
            new RustServerQueryResult("example.test", 28015, null, null, 0, 0, false, null, failedCheck, "timeout"));
        var service = new ServerPollingService(
            queryClient,
            new FakeRustServerMapMetadataClient(),
            repository);

        await service.PollConfiguredServerAsync(
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            CancellationToken.None);
        var result = await service.PollConfiguredServerAsync(
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            CancellationToken.None);

        var server = Assert.Single(repository.Servers);
        Assert.Equal(ServerStatus.Online, server.Status);
        Assert.Equal(12, server.Players);
        Assert.Equal(firstCheck, server.LastSeenAtUtc);
        Assert.Equal(failedCheck, server.LastCheckedAtUtc);
        Assert.Equal(ServerStatus.Online, result.Status);
        Assert.Equal(2, repository.Snapshots.Count);
    }

    [Fact]
    public async Task PollConfiguredServerAsync_MarksServerOfflineAfterOfflineThresholdExpires()
    {
        var firstCheck = DateTimeOffset.Parse("2026-07-06T12:00:00Z");
        var failedCheck = firstCheck.AddSeconds(61);
        var repository = new InMemoryServerRepository();
        var queryClient = new FakeRustServerQueryClient(
            new RustServerQueryResult("example.test", 28015, "Online", "Procedural Map", 12, 100, true, 10, firstCheck, null),
            new RustServerQueryResult("example.test", 28015, null, null, 0, 0, false, null, failedCheck, "timeout"));
        var service = new ServerPollingService(
            queryClient,
            new FakeRustServerMapMetadataClient(),
            repository);

        await service.PollConfiguredServerAsync(
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            CancellationToken.None);
        var result = await service.PollConfiguredServerAsync(
            refreshCurrentPlayers: false,
            refreshMapMetadata: false,
            CancellationToken.None);

        var server = Assert.Single(repository.Servers);
        Assert.Equal(ServerStatus.Offline, server.Status);
        Assert.Equal(0, server.Players);
        Assert.Equal(firstCheck, server.LastSeenAtUtc);
        Assert.Equal(failedCheck, server.LastCheckedAtUtc);
        Assert.Equal(ServerStatus.Offline, result.Status);
    }

    [Fact]
    public async Task PollConfiguredServerAsync_DoesNotStoreUnusableMapMetadata()
    {
        var repository = new InMemoryServerRepository();
        var queryClient = new FakeRustServerQueryClient(
            new RustServerQueryResult(
                "example.test",
                28015,
                "RustIQ Test Server",
                "Procedural Map",
                1,
                100,
                true,
                10,
                DateTimeOffset.Parse("2026-07-06T12:00:00Z"),
                null));
        var mapClient = new FakeRustServerMapMetadataClient(
            new RustServerMapMetadata(
                "example.test",
                28015,
                null,
                null,
                null,
                null,
                null,
                null,
                "BattleMetrics",
                null,
                DateTimeOffset.Parse("2026-07-06T12:00:01Z"),
                "metadata unavailable"));
        var service = new ServerPollingService(queryClient, mapClient, repository);

        var result = await service.PollConfiguredServerAsync(
            refreshCurrentPlayers: false,
            refreshMapMetadata: true,
            CancellationToken.None);

        var server = Assert.Single(repository.Servers);
        Assert.Null(server.MapMetadata);
        Assert.Null(result.MapMetadata);
    }
}
