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
            battleMetricsClient,
            pollingService,
            TimeSpan.FromSeconds(60));

        await service.RefreshAllServersAsync(CancellationToken.None);

        var server = Assert.Single(repository.Servers);
        Assert.Equal(42, server.BattleMetricsServerId);
        Assert.Equal(ServerPlayerCountSource.BattleMetrics, server.PlayerCountSource);
        Assert.Equal(77, server.Players);
        Assert.Equal(200, server.MaxPlayers);
        Assert.Equal("GB", server.Country);
        Assert.Equal(checkedAtUtc, server.LastCheckedAtUtc);
        Assert.Equal(checkedAtUtc, server.LastPlayerCountUpdatedAtUtc);
        Assert.Equal(checkedAtUtc, server.UpdatedAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-07-07T08:00:00Z"), server.LastWipeAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-07-14T08:00:00Z"), server.NextWipeAtUtc);
    }
}
