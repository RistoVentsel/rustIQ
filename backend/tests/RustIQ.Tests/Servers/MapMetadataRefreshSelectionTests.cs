using RustIQ.Application.Servers;
using RustIQ.Domain.Servers;
using RustIQ.Tests.TestDoubles;

namespace RustIQ.Tests.Servers;

public sealed class MapMetadataRefreshSelectionTests
{
    private static readonly DateTimeOffset CutoffUtc = DateTimeOffset.Parse("2026-07-18T15:00:00Z");

    [Fact]
    public async Task GetServersForMapMetadataRefreshAsync_StopsAtTheRequestedCap()
    {
        var repository = new InMemoryServerRepository();
        for (var index = 0; index < 10; index++)
        {
            repository.Servers.Add(ServerWithMap(index + 1, null, ServerStatus.Online, index));
        }

        var batch = await SelectAsync(repository, take: 4, priorityTake: 250);

        // Unbounded, this pulled every server missing map metadata into memory at once. Discovery
        // registers far more than a scheduled run could ever query, so a run takes a slice and leaves
        // the rest for the next one.
        Assert.Equal(4, batch.Servers.Count);
    }

    [Fact]
    public async Task GetServersForMapMetadataRefreshAsync_SelectsMissingAndStaleMapsOnly()
    {
        var repository = new InMemoryServerRepository();
        var missingMap = ServerWithMap(1, null, ServerStatus.Online, 50);
        var staleMap = ServerWithMap(2, CutoffUtc.AddMinutes(-1), ServerStatus.Offline, 0);
        var freshMap = ServerWithMap(3, CutoffUtc, ServerStatus.Online, 100);
        freshMap.NextWipeAtUtc = CutoffUtc.AddDays(1);
        var noBattleMetricsId = ServerWithMap(null, null, ServerStatus.Online, 200);
        var freshMapWithoutNextWipe = ServerWithMap(4, CutoffUtc, ServerStatus.Online, 150);
        repository.Servers.AddRange([missingMap, staleMap, freshMap, noBattleMetricsId, freshMapWithoutNextWipe]);

        var batch = await SelectAsync(repository, take: 100, priorityTake: 250);

        Assert.Equal(
            [noBattleMetricsId.Id, freshMapWithoutNextWipe.Id, missingMap.Id, staleMap.Id],
            batch.Servers.Select(server => server.Id));
    }

    [Fact]
    public async Task GetServersForMapMetadataRefreshAsync_PrefersLivePopulatedServersOverServersMissingData()
    {
        var repository = new InMemoryServerRepository();

        // Nothing is recorded when an A2S_RULES query fails, so a server that never answers keeps its
        // empty wipe and map fields forever. Leading the order with "missing data first" therefore put
        // exactly those servers at the front of every run - fine while the run was unbounded, but once
        // it was capped they occupied the whole budget and no other server was ever reached.
        var dataStarved = ServerWithMap(1, null, ServerStatus.Offline, 0);
        var livePopular = ServerWithMap(2, CutoffUtc.AddMinutes(-1), ServerStatus.Online, 300);
        livePopular.NextWipeAtUtc = CutoffUtc.AddDays(1);
        repository.Servers.AddRange([dataStarved, livePopular]);

        var batch = await SelectAsync(repository, take: 1, priorityTake: 1);

        Assert.Equal(livePopular.Id, Assert.Single(batch.Servers).Id);
    }

    [Fact]
    public async Task GetServersForMapMetadataRefreshAsync_RotationReachesEveryCandidateAcrossRuns()
    {
        var repository = new InMemoryServerRepository();
        for (var index = 0; index < 5; index++)
        {
            // Offline, so the priority slice stays empty and everything goes through the rotation.
            repository.Servers.Add(ServerWithMap(index + 1, null, ServerStatus.Offline, 0));
        }

        var seenServerIds = new HashSet<Guid>();
        Guid? rotationCursor = null;
        var firstRunIds = Array.Empty<Guid>();

        for (var run = 0; run < 3; run++)
        {
            var batch = await SelectAsync(repository, take: 2, priorityTake: 0, rotationCursor);
            var batchIds = batch.Servers.Select(server => server.Id).ToArray();

            if (run == 0)
            {
                firstRunIds = batchIds;
            }
            else if (run == 1)
            {
                // Consecutive runs must not re-read the same slice, which is what starved the rest.
                Assert.Empty(firstRunIds.Intersect(batchIds));
            }

            foreach (var serverId in batchIds)
            {
                seenServerIds.Add(serverId);
            }

            rotationCursor = batch.NextRotationCursor;
        }

        Assert.Equal(5, seenServerIds.Count);
    }

    [Fact]
    public async Task GetServersForMapMetadataRefreshAsync_RotationWrapsAtTheEndOfTheCandidates()
    {
        var repository = new InMemoryServerRepository();
        for (var index = 0; index < 3; index++)
        {
            repository.Servers.Add(ServerWithMap(index + 1, null, ServerStatus.Offline, 0));
        }

        var firstRun = await SelectAsync(repository, take: 2, priorityTake: 0);
        var secondRun = await SelectAsync(repository, take: 2, priorityTake: 0, firstRun.NextRotationCursor);

        // One candidate is left after the first run, so the second has to wrap to fill its budget
        // rather than coming back short and leaving the rotation stalled at the end of the ids.
        Assert.Equal(2, secondRun.Servers.Count);
    }

    private static Task<MapMetadataRefreshBatch> SelectAsync(
        InMemoryServerRepository repository,
        int take,
        int priorityTake,
        Guid? rotationCursor = null)
    {
        return repository.GetServersForMapMetadataRefreshAsync(
            CutoffUtc,
            take,
            priorityTake,
            rotationCursor,
            CancellationToken.None);
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
