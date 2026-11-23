using RustIQ.Domain.Servers;
using RustIQ.Domain.Study;

namespace RustIQ.Simulator.Data;

public sealed record SimulatorData
{
    public required IReadOnlyList<ExportManifest> Manifests { get; init; }

    public required IReadOnlyList<SampleServer> SampleServers { get; init; }

    public required IReadOnlyList<ProbeSeries> Probes { get; init; }

    public required ServerIndex ServerIndex { get; init; }

    public required IReadOnlyList<ServerRecord?> Servers { get; init; }

    public required IReadOnlyList<SnapshotSeries> Snapshots { get; init; }

    public required IReadOnlyList<ServerRankingRow> Rankings { get; init; }

    public required IReadOnlyList<SampleRankingRow> SampleRankings { get; init; }

    public required IReadOnlyList<PopulationSample> PopulationSamples { get; init; }

    public required IReadOnlyList<TableWriteSample> TableWriteSamples { get; init; }

    public required IReadOnlyList<WipeObservationRow> WipeObservations { get; init; }

    public required IReadOnlyList<PredictionSnapshotRow> PredictionSnapshots { get; init; }

    public long SnapshotCount
    {
        get
        {
            long total = 0;
            foreach (SnapshotSeries series in Snapshots)
            {
                total += series.Count;
            }

            return total;
        }
    }

    public long ProbeCount
    {
        get
        {
            long total = 0;
            foreach (ProbeSeries series in Probes)
            {
                total += series.Count;
            }

            return total;
        }
    }
}

public sealed record SampleServer(
    int ServerIdx,
    Guid ServerId,
    int ServerIndex,
    StudyStratum Stratum,
    int PeakPlayers,
    string? Country,
    DateTimeOffset SelectedAtUtc);

public sealed class ServerIndex
{
    private readonly Dictionary<Guid, int> _indexes = new Dictionary<Guid, int>();
    private readonly List<Guid> _ids = new List<Guid>();

    public int Count
    {
        get { return _ids.Count; }
    }

    public IReadOnlyList<Guid> Ids
    {
        get { return _ids; }
    }

    public int GetOrAdd(Guid id)
    {
        int index;
        if (_indexes.TryGetValue(id, out index))
        {
            return index;
        }

        index = _ids.Count;
        _indexes.Add(id, index);
        _ids.Add(id);
        return index;
    }

    public bool TryGet(Guid id, out int index)
    {
        return _indexes.TryGetValue(id, out index);
    }
}

public sealed record ServerRecord(
    Guid Id,
    RustServerType ServerType,
    ServerStatus Status,
    int Players,
    int MaxPlayers,
    int QueuePlayers,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastSeenAtUtc,
    DateTimeOffset? LastCheckedAtUtc,
    DateTimeOffset? LastDiscoveredAtUtc,
    DateTimeOffset? LastWipeAtUtc,
    DateTimeOffset? NextWipeAtUtc,
    bool InLatestExport);

public sealed record ServerRankingRow(Guid ServerId, int Rank, int PeakPlayers, DateTimeOffset GeneratedAtUtc);

public sealed record SampleRankingRow(long Id, DateTimeOffset SampledAtUtc, Guid ServerId, int? Rank);

public sealed record PopulationSample(
    long Id,
    DateTimeOffset SampledAtUtc,
    int TotalServers,
    int OnlineServers,
    int OnlineServersWithPlayers);

public sealed record TableWriteSample(
    long Id,
    DateTimeOffset SampledAtUtc,
    string TableName,
    long Inserts,
    long Updates,
    long Deletes);

public sealed record WipeObservationRow(
    long Id,
    Guid ServerId,
    DateTimeOffset ObservedAtUtc,
    DateTimeOffset? LastWipeAtUtc,
    DateTimeOffset? PreviousLastWipeAtUtc,
    DateTimeOffset? PreviousNextWipeAtUtc,
    bool FirstSinceStart,
    string? Keywords);

public sealed record PredictionSnapshotRow(
    long Id,
    Guid ServerId,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset? NextWipeAtUtc,
    DateTimeOffset? DisplayedNextWipeAtUtc,
    DateTimeOffset? LastWipeAtUtc);

public sealed class ProbeSeries
{
    public static readonly ProbeSeries Empty = new ProbeSeries(new long[0], new bool[0], new short[0], new short[0]);

    public ProbeSeries(long[] timesMs, bool[] ok, short[] players, short[] queue)
    {
        TimesMs = timesMs;
        Ok = ok;
        Players = players;
        Queue = queue;
    }

    public long[] TimesMs { get; }
    public bool[] Ok { get; }
    public short[] Players { get; }
    public short[] Queue { get; }

    public int Count
    {
        get { return TimesMs.Length; }
    }
}

public sealed class SnapshotSeries
{
    public static readonly SnapshotSeries Empty = new SnapshotSeries(
        new long[0],
        new short[0],
        new short[0],
        new bool[0],
        new sbyte[0],
        new ServerPollSource[0]);

    public SnapshotSeries(
        long[] timesMs,
        short[] players,
        short[] maxPlayers,
        bool[] online,
        sbyte[] ok,
        ServerPollSource[] sources)
    {
        TimesMs = timesMs;
        Players = players;
        MaxPlayers = maxPlayers;
        Online = online;
        Ok = ok;
        Sources = sources;
    }

    public long[] TimesMs { get; }
    public short[] Players { get; }
    public short[] MaxPlayers { get; }
    public bool[] Online { get; }
    public sbyte[] Ok { get; }
    public ServerPollSource[] Sources { get; }

    public int Count
    {
        get { return TimesMs.Length; }
    }

    public bool Answered(int i)
    {
        if (Ok[i] == 1)
        {
            return true;
        }

        if (Ok[i] == 0)
        {
            return false;
        }

        return Online[i];
    }
}
