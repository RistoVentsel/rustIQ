using RustIQ.Domain.Study;

namespace RustIQ.Application.Study;

public interface IStudyRepository
{
    Task<IReadOnlyList<StudySampleServer>> GetSampleAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<StudySelectionCandidate>> GetSelectionCandidatesAsync(
        DateTimeOffset seenAfterUtc,
        DateTimeOffset peakWindowStartUtc,
        int minimumPeakPlayers,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, StudyWipeSignal>> GetLatestWipeSignalsAsync(
        CancellationToken cancellationToken);

    Task SetBattleMetricsServerIdAsync(
        Guid serverId,
        long battleMetricsServerId,
        CancellationToken cancellationToken);

    Task<StudyPopulationSample> GetPopulationAsync(
        DateTimeOffset sampledAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StudyTableWriteSample>> GetTableWritesAsync(
        DateTimeOffset sampledAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StudySampleRanking>> GetSampleRankingsAsync(
        DateTimeOffset sampledAtUtc,
        CancellationToken cancellationToken);

    void AddSample(IEnumerable<StudySampleServer> sample);

    void AddProbes(IEnumerable<StudyProbe> probes);

    void AddWipeSignals(IEnumerable<StudyWipeSignal> signals);

    void AddBattleMetricsWipeSnapshot(StudyBattleMetricsWipeSnapshot snapshot);

    void AddServerDescriptions(IEnumerable<StudyServerDescription> descriptions);

    void AddMeasurements(
        IEnumerable<StudyResourceSample> resourceSamples,
        IEnumerable<StudyPopulationSample> populationSamples,
        IEnumerable<StudyTableWriteSample> tableWriteSamples,
        IEnumerable<StudySampleRanking> sampleRankings);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
