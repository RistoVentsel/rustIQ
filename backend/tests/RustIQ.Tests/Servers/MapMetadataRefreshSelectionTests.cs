using RustIQ.Domain.Servers;
using RustIQ.Tests.TestDoubles;

namespace RustIQ.Tests.Servers;

public sealed class MapMetadataRefreshSelectionTests
{
    [Fact]
    public async Task GetServersForMapMetadataRefreshAsync_StopsAtTheRequestedCap()
    {
        var cutoffUtc = DateTimeOffset.Parse("2026-07-18T15:00:00Z");
        var repository = new InMemoryServerRepository();
        for (var index = 0; index < 10; index++)
        {
            repository.Servers.Add(ServerWithMap(index + 1, null, ServerStatus.Online, index));
        }

        var result = await repository.GetServersForMapMetadataRefreshAsync(
            cutoffUtc,
            take: 4,
            CancellationToken.None);

        // Unbounded, this pulled every server missing map metadata into memory at once. Discovery
        // registers far more than a scheduled run could ever query, so a run has to take a slice and
        // leave the rest for the next one.
        Assert.Equal(4, result.Count);
    }

    [Fact]
    public async Task GetServersForMapMetadataRefreshAsync_SelectsMissingAndStaleMapsOnly()
    {
        var cutoffUtc = DateTimeOffset.Parse("2026-07-18T15:00:00Z");
        var repository = new InMemoryServerRepository();
        var missingMap = ServerWithMap(1, null, ServerStatus.Online, 50);
        var staleMap = ServerWithMap(2, cutoffUtc.AddMinutes(-1), ServerStatus.Offline, 0);
        var freshMap = ServerWithMap(3, cutoffUtc, ServerStatus.Online, 100);
        freshMap.NextWipeAtUtc = cutoffUtc.AddDays(1);
        var noBattleMetricsId = ServerWithMap(null, null, ServerStatus.Online, 200);
        var freshMapWithoutNextWipe = ServerWithMap(4, cutoffUtc, ServerStatus.Online, 150);
        repository.Servers.AddRange([missingMap, staleMap, freshMap, noBattleMetricsId, freshMapWithoutNextWipe]);

        var result = await repository.GetServersForMapMetadataRefreshAsync(
            cutoffUtc,
            take: 100,
            CancellationToken.None);

        Assert.Equal(
            [noBattleMetricsId.Id, freshMapWithoutNextWipe.Id, missingMap.Id, staleMap.Id],
            result.Select(server => server.Id));
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
