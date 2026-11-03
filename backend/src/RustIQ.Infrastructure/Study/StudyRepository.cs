using Microsoft.EntityFrameworkCore;
using RustIQ.Application.Study;
using RustIQ.Domain.Servers;
using RustIQ.Domain.Study;
using RustIQ.Infrastructure.Data;

namespace RustIQ.Infrastructure.Study;

public sealed class StudyRepository(RustIqDbContext dbContext) : IStudyRepository
{
    public async Task<IReadOnlyList<StudySampleServer>> GetSampleAsync(CancellationToken cancellationToken)
    {
        return await dbContext.StudySampleServers
            .AsNoTracking()
            .OrderBy(server => server.Stratum)
            .ThenBy(server => server.Host)
            .ThenBy(server => server.QueryPort)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StudySelectionCandidate>> GetSelectionCandidatesAsync(
        DateTimeOffset seenAfterUtc,
        DateTimeOffset peakWindowStartUtc,
        int minimumPeakPlayers,
        CancellationToken cancellationToken)
    {
        var peaks = dbContext.ServerSnapshots
            .Where(snapshot => snapshot.CheckedAtUtc >= peakWindowStartUtc)
            .GroupBy(snapshot => snapshot.ServerId)
            .Select(group => new { ServerId = group.Key, PeakPlayers = group.Max(snapshot => snapshot.Players) })
            // Filtered here: EF cannot translate a filter applied after the record constructor below.
            .Where(peak => peak.PeakPlayers >= minimumPeakPlayers);

        return await dbContext.Servers
            .AsNoTracking()
            .Where(server => server.LastSeenAtUtc >= seenAfterUtc)
            .Join(
                peaks,
                server => server.Id,
                peak => peak.ServerId,
                (server, peak) => new StudySelectionCandidate(
                    server.Id,
                    server.Host,
                    server.QueryPort,
                    server.Name,
                    server.Country,
                    server.BattleMetricsServerId,
                    peak.PeakPlayers))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, StudyWipeSignal>> GetLatestWipeSignalsAsync(
        CancellationToken cancellationToken)
    {
        var latestSignals = await dbContext.StudyWipeSignals
            .AsNoTracking()
            .GroupBy(signal => signal.ServerId)
            .Select(group => group.OrderByDescending(signal => signal.ObservedAtUtc).First())
            .ToListAsync(cancellationToken);

        return latestSignals.ToDictionary(signal => signal.ServerId);
    }

    public async Task SetBattleMetricsServerIdAsync(
        Guid serverId,
        long battleMetricsServerId,
        CancellationToken cancellationToken)
    {
        await dbContext.StudySampleServers
            .Where(server => server.ServerId == serverId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(server => server.BattleMetricsServerId, battleMetricsServerId),
                cancellationToken);
    }

    public async Task<StudyPopulationSample> GetPopulationAsync(
        DateTimeOffset sampledAtUtc,
        CancellationToken cancellationToken)
    {
        var counts = await dbContext.Servers
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Online = group.Count(server => server.Status == ServerStatus.Online),
                OnlineWithPlayers = group.Count(server => server.Status == ServerStatus.Online && server.Players > 0)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return new StudyPopulationSample
        {
            SampledAtUtc = sampledAtUtc,
            TotalServers = counts?.Total ?? 0,
            OnlineServers = counts?.Online ?? 0,
            OnlineServersWithPlayers = counts?.OnlineWithPlayers ?? 0
        };
    }

    public async Task<IReadOnlyList<StudyTableWriteSample>> GetTableWritesAsync(
        DateTimeOffset sampledAtUtc,
        CancellationToken cancellationToken)
    {
        var counters = await dbContext.Database
            .SqlQueryRaw<TableWriteCounter>(
                """
                SELECT relname AS "TableName", n_tup_ins AS "Inserts", n_tup_upd AS "Updates", n_tup_del AS "Deletes"
                FROM pg_stat_user_tables
                """)
            .ToListAsync(cancellationToken);

        return counters
            .Select(counter => new StudyTableWriteSample
            {
                SampledAtUtc = sampledAtUtc,
                TableName = counter.TableName,
                Inserts = counter.Inserts,
                Updates = counter.Updates,
                Deletes = counter.Deletes
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<StudySampleRanking>> GetSampleRankingsAsync(
        DateTimeOffset sampledAtUtc,
        CancellationToken cancellationToken)
    {
        return await (
                from sample in dbContext.StudySampleServers
                join ranking in dbContext.ServerRankings on sample.ServerId equals ranking.ServerId into rankings
                from ranking in rankings.DefaultIfEmpty()
                select new StudySampleRanking
                {
                    SampledAtUtc = sampledAtUtc,
                    ServerId = sample.ServerId,
                    Rank = ranking == null ? null : ranking.Rank
                })
            .ToListAsync(cancellationToken);
    }

    public void AddSample(IEnumerable<StudySampleServer> sample)
    {
        dbContext.StudySampleServers.AddRange(sample);
    }

    public void AddProbes(IEnumerable<StudyProbe> probes)
    {
        dbContext.StudyProbes.AddRange(probes);
    }

    public void AddWipeSignals(IEnumerable<StudyWipeSignal> signals)
    {
        dbContext.StudyWipeSignals.AddRange(signals);
    }

    public void AddBattleMetricsWipeSnapshot(StudyBattleMetricsWipeSnapshot snapshot)
    {
        dbContext.StudyBattleMetricsWipeSnapshots.Add(snapshot);
    }

    public void AddServerDescriptions(IEnumerable<StudyServerDescription> descriptions)
    {
        dbContext.StudyServerDescriptions.AddRange(descriptions);
    }

    public void AddMeasurements(
        IEnumerable<StudyResourceSample> resourceSamples,
        IEnumerable<StudyPopulationSample> populationSamples,
        IEnumerable<StudyTableWriteSample> tableWriteSamples,
        IEnumerable<StudySampleRanking> sampleRankings)
    {
        dbContext.StudyResourceSamples.AddRange(resourceSamples);
        dbContext.StudyPopulationSamples.AddRange(populationSamples);
        dbContext.StudyTableWriteSamples.AddRange(tableWriteSamples);
        dbContext.StudySampleRankings.AddRange(sampleRankings);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class TableWriteCounter
    {
        public string TableName { get; set; } = string.Empty;

        public long Inserts { get; set; }

        public long Updates { get; set; }

        public long Deletes { get; set; }
    }
}
