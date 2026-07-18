using RustIQ.Domain.Servers;
using RustIQ.Tests.TestDoubles;

namespace RustIQ.Tests.Servers;

public sealed class MapMetadataRefreshSelectionTests
{
    [Fact]
    public async Task GetServersForMapMetadataRefreshAsync_SelectsMissingAndStaleMapsOnly()
    {
        var cutoffUtc = DateTimeOffset.Parse("2026-07-18T15:00:00Z");
        var repository = new InMemoryServerRepository();
        var missingMap = ServerWithMap(1, null, ServerStatus.Online, 50);
        var staleMap = ServerWithMap(2, cutoffUtc.AddMinutes(-1), ServerStatus.Offline, 0);
        var freshMap = ServerWithMap(3, cutoffUtc, ServerStatus.Online, 100);
        var noBattleMetricsId = ServerWithMap(null, null, ServerStatus.Online, 200);
        repository.Servers.AddRange([missingMap, staleMap, freshMap, noBattleMetricsId]);

        var result = await repository.GetServersForMapMetadataRefreshAsync(
            cutoffUtc,
            CancellationToken.None);

        Assert.Equal([missingMap.Id, staleMap.Id], result.Select(server => server.Id));
    }

    private static Server ServerWithMap(
        long? battleMetricsServerId,
        DateTimeOffset? fetchedAtUtc,
        ServerStatus status,
        int players)
    {
        var id = Guid.NewGuid();
        return new Server
        {
            Id = id,
            BattleMetricsServerId = battleMetricsServerId,
            Host = $"{id:N}.test",
            QueryPort = 28015,
            Status = status,
            Players = players,
            CreatedAtUtc = DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
            UpdatedAtUtc = DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
            MapMetadata = fetchedAtUtc is null
                ? null
                : new ServerMapMetadata
                {
                    Id = Guid.NewGuid(),
                    ServerId = id,
                    FetchedAtUtc = fetchedAtUtc.Value,
                    CreatedAtUtc = fetchedAtUtc.Value,
                    UpdatedAtUtc = fetchedAtUtc.Value
                }
        };
    }
}
