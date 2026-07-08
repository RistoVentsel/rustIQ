using RustIQ.Application.ServerQuery;
using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;
using RustIQ.Tests.TestDoubles;

namespace RustIQ.Tests.Servers;

public sealed class ServerRefreshServiceTests
{
    [Fact]
    public async Task RefreshAllServersAsync_ResolvesBattleMetricsIdForA2sServerAndUpdatesPopulation()
    {
        var checkedAtUtc = DateTimeOffset.Parse("2026-07-07T12:00:00Z");
        var existingUpdatedAtUtc = checkedAtUtc.AddMinutes(-10);
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = Guid.NewGuid(),
            Host = "203.0.113.10",
            QueryPort = 28015,
            Name = "A2S Server",
            Players = 10,
            MaxPlayers = 50,
            Status = ServerStatus.Online,
            PlayerCountSource = ServerPlayerCountSource.A2S,
            CreatedAtUtc = existingUpdatedAtUtc,
            UpdatedAtUtc = existingUpdatedAtUtc
        });
        var battleMetricsClient = new FakeBattleMetricsPlayerCountClient(
            new Dictionary<(string Host, int QueryPort), long?> { [("203.0.113.10", 28015)] = 42 },
            new Dictionary<long, BattleMetricsPlayerCountResult?>
            {
                [42] = new BattleMetricsPlayerCountResult(
                    42,
                    77,
                    200,
                    6,
                    RustServerType.Official,
                    true,
                    "GB",
                    51.5,
                    -0.12,
                    DateTimeOffset.Parse("2026-07-07T08:00:00Z"),
                    DateTimeOffset.Parse("2026-07-14T08:00:00Z"),
                    checkedAtUtc)
            });
        var pollingService = new ServerPollingService(
            new FakeRustServerQueryClient(),
            battleMetricsClient,
            new FakeRustServerMapMetadataClient(),
            repository);
        var service = new ServerRefreshService(
            repository,
            new FakeRustServerCatalog(),
            new FakeBattleMetricsServerDirectoryClient(),
            battleMetricsClient,
            pollingService,
            battleMetricsDirectoryPageSize: 100,
            battleMetricsDirectoryMaxPages: 20,
            TimeSpan.FromSeconds(60));

        var beforeRefreshUtc = DateTimeOffset.UtcNow;
        await service.RefreshAllServersAsync(CancellationToken.None);
        var afterRefreshUtc = DateTimeOffset.UtcNow;

        var server = Assert.Single(repository.Servers);
        Assert.Equal(42, server.BattleMetricsServerId);
        Assert.Equal(ServerPlayerCountSource.BattleMetrics, server.PlayerCountSource);
        Assert.Equal(77, server.Players);
        Assert.Equal(200, server.MaxPlayers);
        Assert.Equal(RustServerType.Official, server.ServerType);
        Assert.Equal("GB", server.Country);
        Assert.Equal(checkedAtUtc, server.LastCheckedAtUtc);
        Assert.InRange(server.LastPlayerCountUpdatedAtUtc!.Value, beforeRefreshUtc, afterRefreshUtc);
        Assert.Equal(server.LastPlayerCountUpdatedAtUtc, server.UpdatedAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-07-07T08:00:00Z"), server.LastWipeAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-07-14T08:00:00Z"), server.NextWipeAtUtc);
    }

    [Fact]
    public async Task RefreshAllServersAsync_UsesOneUpdatedAtTimestampForBatch()
    {
        var repository = new InMemoryServerRepository();
        repository.Servers.AddRange(
        [
            new Server
            {
                Id = Guid.NewGuid(),
                BattleMetricsServerId = 1,
                Host = "one.test",
                QueryPort = 28015,
                Players = 1,
                MaxPlayers = 100,
                Status = ServerStatus.Online,
                CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
                UpdatedAtUtc = DateTimeOffset.UtcNow.AddHours(-1)
            },
            new Server
            {
                Id = Guid.NewGuid(),
                BattleMetricsServerId = 2,
                Host = "two.test",
                QueryPort = 28015,
                Players = 2,
                MaxPlayers = 100,
                Status = ServerStatus.Online,
                CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
                UpdatedAtUtc = DateTimeOffset.UtcNow.AddHours(-1)
            }
        ]);
        var battleMetricsClient = new FakeBattleMetricsPlayerCountClient(
            new Dictionary<(string Host, int QueryPort), long?>(),
            new Dictionary<long, BattleMetricsPlayerCountResult?>
            {
                [1] = new BattleMetricsPlayerCountResult(
                    1,
                    10,
                    100,
                    QueuePlayers: null,
                    ServerType: RustServerType.Unknown,
                    true,
                    Country: null,
                    Latitude: null,
                    Longitude: null,
                    LastWipeAtUtc: null,
                    NextWipeAtUtc: null,
                    DateTimeOffset.Parse("2026-07-07T12:00:00Z")),
                [2] = new BattleMetricsPlayerCountResult(
                    2,
                    20,
                    100,
                    QueuePlayers: null,
                    ServerType: RustServerType.Unknown,
                    true,
                    Country: null,
                    Latitude: null,
                    Longitude: null,
                    LastWipeAtUtc: null,
                    NextWipeAtUtc: null,
                    DateTimeOffset.Parse("2026-07-07T12:05:00Z"))
            });
        var pollingService = new ServerPollingService(
            new FakeRustServerQueryClient(),
            battleMetricsClient,
            new FakeRustServerMapMetadataClient(),
            repository);
        var service = new ServerRefreshService(
            repository,
            new FakeRustServerCatalog(),
            new FakeBattleMetricsServerDirectoryClient(),
            battleMetricsClient,
            pollingService,
            battleMetricsDirectoryPageSize: 100,
            battleMetricsDirectoryMaxPages: 20,
            TimeSpan.FromSeconds(60));

        await service.RefreshAllServersAsync(CancellationToken.None);

        Assert.Equal(10, repository.Servers.Single(server => server.Host == "one.test").Players);
        Assert.Equal(20, repository.Servers.Single(server => server.Host == "two.test").Players);
        Assert.Single(repository.Servers.Select(server => server.UpdatedAtUtc).Distinct());
    }

    [Fact]
    public async Task RefreshAllServersAsync_UsesBattleMetricsDirectorySummariesForPopulation()
    {
        var checkedAtUtc = DateTimeOffset.Parse("2026-07-07T12:00:00Z");
        var existingUpdatedAtUtc = checkedAtUtc.AddMinutes(-30);
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = Guid.NewGuid(),
            Host = "203.0.113.10",
            QueryPort = 28015,
            Name = "A2S Server",
            Players = 10,
            MaxPlayers = 50,
            Status = ServerStatus.Online,
            PlayerCountSource = ServerPlayerCountSource.A2S,
            CreatedAtUtc = existingUpdatedAtUtc,
            UpdatedAtUtc = existingUpdatedAtUtc
        });
        var battleMetricsClient = new FakeBattleMetricsPlayerCountClient();
        var directoryClient = new FakeBattleMetricsServerDirectoryClient(
        [
            new BattleMetricsServerSummary(
                42,
                "BattleMetrics Server",
                "203.0.113.10:28015",
                "203.0.113.10",
                28015,
                28015,
                "GB",
                51.5,
                -0.12,
                DateTimeOffset.Parse("2026-07-07T08:00:00Z"),
                DateTimeOffset.Parse("2026-07-14T08:00:00Z"),
                77,
                200,
                6,
                RustServerType.Official,
                true,
                checkedAtUtc)
        ]);
        var pollingService = new ServerPollingService(
            new FakeRustServerQueryClient(),
            battleMetricsClient,
            new FakeRustServerMapMetadataClient(),
            repository);
        var service = new ServerRefreshService(
            repository,
            new FakeRustServerCatalog(),
            directoryClient,
            battleMetricsClient,
            pollingService,
            battleMetricsDirectoryPageSize: 100,
            battleMetricsDirectoryMaxPages: 20,
            TimeSpan.FromSeconds(60));

        var beforeRefreshUtc = DateTimeOffset.UtcNow;
        await service.RefreshAllServersAsync(CancellationToken.None);
        var afterRefreshUtc = DateTimeOffset.UtcNow;

        var server = Assert.Single(repository.Servers);
        Assert.Equal(42, server.BattleMetricsServerId);
        Assert.Equal(77, server.Players);
        Assert.Equal(200, server.MaxPlayers);
        Assert.Equal(6, server.QueuePlayers);
        Assert.Equal(RustServerType.Official, server.ServerType);
        Assert.Equal(ServerPlayerCountSource.BattleMetrics, server.PlayerCountSource);
        Assert.Equal(checkedAtUtc, server.LastCheckedAtUtc);
        Assert.InRange(server.UpdatedAtUtc, beforeRefreshUtc, afterRefreshUtc);
    }

    [Fact]
    public async Task RefreshBattleMetricsDirectoryServersAsync_UpdatesPopulationWithoutPerServerFallback()
    {
        var checkedAtUtc = DateTimeOffset.Parse("2026-07-07T12:00:00Z");
        var existingUpdatedAtUtc = checkedAtUtc.AddMinutes(-30);
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = Guid.NewGuid(),
            BattleMetricsServerId = 42,
            Host = "203.0.113.10",
            QueryPort = 28015,
            Name = "BattleMetrics Server",
            Players = 10,
            MaxPlayers = 50,
            Status = ServerStatus.Online,
            PlayerCountSource = ServerPlayerCountSource.BattleMetrics,
            CreatedAtUtc = existingUpdatedAtUtc,
            UpdatedAtUtc = existingUpdatedAtUtc
        });
        repository.Servers.Add(new Server
        {
            Id = Guid.NewGuid(),
            BattleMetricsServerId = 43,
            Host = "203.0.113.11",
            QueryPort = 28015,
            Name = "Not In Directory Page",
            Players = 20,
            MaxPlayers = 100,
            Status = ServerStatus.Online,
            PlayerCountSource = ServerPlayerCountSource.BattleMetrics,
            CreatedAtUtc = existingUpdatedAtUtc,
            UpdatedAtUtc = existingUpdatedAtUtc
        });
        var battleMetricsClient = new FakeBattleMetricsPlayerCountClient(
            new Dictionary<(string Host, int QueryPort), long?>(),
            new Dictionary<long, BattleMetricsPlayerCountResult?>
            {
                [43] = new BattleMetricsPlayerCountResult(
                    43,
                    88,
                    250,
                    null,
                    RustServerType.Modded,
                    true,
                    null,
                    null,
                    null,
                    null,
                    null,
                    checkedAtUtc)
            });
        var directoryClient = new FakeBattleMetricsServerDirectoryClient(
        [
            new BattleMetricsServerSummary(
                42,
                "BattleMetrics Server",
                "203.0.113.10:28015",
                "203.0.113.10",
                28015,
                28015,
                "GB",
                51.5,
                -0.12,
                null,
                null,
                77,
                200,
                null,
                RustServerType.Community,
                true,
                checkedAtUtc)
        ]);
        var pollingService = new ServerPollingService(
            new FakeRustServerQueryClient(),
            battleMetricsClient,
            new FakeRustServerMapMetadataClient(),
            repository);
        var service = new ServerRefreshService(
            repository,
            new FakeRustServerCatalog(),
            directoryClient,
            battleMetricsClient,
            pollingService,
            battleMetricsDirectoryPageSize: 100,
            battleMetricsDirectoryMaxPages: 20,
            TimeSpan.FromSeconds(60));

        var refreshedServers = await service.RefreshBattleMetricsDirectoryServersAsync(CancellationToken.None);

        Assert.Equal(1, refreshedServers);
        var refreshedServer = repository.Servers.Single(server => server.BattleMetricsServerId == 42);
        Assert.Equal(77, refreshedServer.Players);
        Assert.Equal(RustServerType.Community, refreshedServer.ServerType);
        Assert.Equal(20, repository.Servers.Single(server => server.BattleMetricsServerId == 43).Players);
        Assert.Equal(0, battleMetricsClient.GetPlayerCountCallCount);
        Assert.Equal(0, battleMetricsClient.FindServerIdCallCount);
    }

    [Fact]
    public async Task RefreshAllServersAsync_StopsPerServerFallbackWhenBattleMetricsDirectoryIsRateLimited()
    {
        var checkedAtUtc = DateTimeOffset.Parse("2026-07-07T12:00:00Z");
        var existingUpdatedAtUtc = checkedAtUtc.AddMinutes(-30);
        var repository = new InMemoryServerRepository();
        repository.Servers.Add(new Server
        {
            Id = Guid.NewGuid(),
            BattleMetricsServerId = 42,
            Host = "203.0.113.10",
            QueryPort = 28015,
            Name = "Partial Server",
            Players = 10,
            MaxPlayers = 50,
            Status = ServerStatus.Online,
            PlayerCountSource = ServerPlayerCountSource.BattleMetrics,
            CreatedAtUtc = existingUpdatedAtUtc,
            UpdatedAtUtc = existingUpdatedAtUtc
        });
        repository.Servers.Add(new Server
        {
            Id = Guid.NewGuid(),
            BattleMetricsServerId = 43,
            Host = "203.0.113.11",
            QueryPort = 28015,
            Name = "Unfetched Server",
            Players = 20,
            MaxPlayers = 100,
            Status = ServerStatus.Online,
            PlayerCountSource = ServerPlayerCountSource.BattleMetrics,
            CreatedAtUtc = existingUpdatedAtUtc,
            UpdatedAtUtc = existingUpdatedAtUtc
        });
        var partialSummaries = new[]
        {
            new BattleMetricsServerSummary(
                42,
                "Partial Server",
                "203.0.113.10:28015",
                "203.0.113.10",
                28015,
                28015,
                "GB",
                51.5,
                -0.12,
                null,
                null,
                77,
                200,
                null,
                RustServerType.Modded,
                true,
                checkedAtUtc)
        };
        var directoryClient = new FakeBattleMetricsServerDirectoryClient(
            exception: new BattleMetricsRateLimitException(partialSummaries, retryAfter: null));
        var battleMetricsClient = new FakeBattleMetricsPlayerCountClient(
            new Dictionary<(string Host, int QueryPort), long?>(),
            new Dictionary<long, BattleMetricsPlayerCountResult?>
            {
                [43] = new BattleMetricsPlayerCountResult(
                    43,
                    88,
                    250,
                    null,
                    RustServerType.Unknown,
                    true,
                    null,
                    null,
                    null,
                    null,
                    null,
                    checkedAtUtc)
            });
        var pollingService = new ServerPollingService(
            new FakeRustServerQueryClient(),
            battleMetricsClient,
            new FakeRustServerMapMetadataClient(),
            repository);
        var service = new ServerRefreshService(
            repository,
            new FakeRustServerCatalog(),
            directoryClient,
            battleMetricsClient,
            pollingService,
            battleMetricsDirectoryPageSize: 100,
            battleMetricsDirectoryMaxPages: 20,
            TimeSpan.FromSeconds(60));

        await service.RefreshAllServersAsync(CancellationToken.None);

        Assert.Equal(77, repository.Servers.Single(server => server.BattleMetricsServerId == 42).Players);
        Assert.Equal(20, repository.Servers.Single(server => server.BattleMetricsServerId == 43).Players);
        Assert.Equal(0, battleMetricsClient.GetPlayerCountCallCount);
        Assert.Equal(0, battleMetricsClient.FindServerIdCallCount);
    }
}
