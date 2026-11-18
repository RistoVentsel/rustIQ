using RustIQ.Domain.Servers;
using RustIQ.Domain.Study;

namespace RustIQ.Simulator.Data;

/// <summary>
/// Everything the simulator reads from one or more merged exports. Times are Unix milliseconds UTC.
/// </summary>
public sealed record SimulatorData
{
    public required IReadOnlyList<ExportManifest> Manifests { get; init; }

    /// <summary>Indexed by <c>server_idx - 1</c>.</summary>
    public required IReadOnlyList<SampleServer> SampleServers { get; init; }

    /// <summary>Ground truth, indexed like <see cref="SampleServers"/>. Sorted by time, unique times.</summary>
    public required IReadOnlyList<ProbeSeries> Probes { get; init; }

    /// <summary>Every server seen in any file. A server's position is its index everywhere else.</summary>
    public required ServerIndex ServerIndex { get; init; }

    /// <summary>The <c>servers</c> row per server index, or null when no export still had the server.</summary>
    public required IReadOnlyList<ServerRecord?> Servers { get; init; }

    /// <summary>Production polls per server index, sorted by time.</summary>
    public required IReadOnlyList<SnapshotSeries> Snapshots { get; init; }

    public required IReadOnlyList<ServerRankingRow> Rankings { get; init; }

    public required IReadOnlyList<SampleRankingRow> SampleRankings { get; init; }

    public required IReadOnlyList<PopulationSample> PopulationSamples { get; init; }

    public required IReadOnlyList<TableWriteSample> TableWriteSamples { get; init; }

    public required IReadOnlyList<WipeObservationRow> WipeObservations { get; init; }

    public required IReadOnlyList<PredictionSnapshotRow> PredictionSnapshots { get; init; }

    public long SnapshotCount => Snapshots.Sum(series => (long)series.Count);

    public long ProbeCount => Probes.Sum(series => (long)series.Count);
}

public sealed record SampleServer(
    int ServerIdx,
    Guid ServerId,
    int ServerIndex,
    StudyStratum Stratum,
    int PeakPlayers,
    string? Country,
    DateTimeOffset SelectedAtUtc);

/// <summary>
/// Maps server ids to dense indexes, so per-server data can live in arrays.
/// </summary>
public sealed class ServerIndex
{
    private readonly Dictionary<Guid, int> _indexes = new();
    private readonly List<Guid> _ids = [];

    public int Count => _ids.Count;

    public IReadOnlyList<Guid> Ids => _ids;

    public int GetOrAdd(Guid id)
    {
        if (_indexes.TryGetValue(id, out var index))
        {
            return index;
        }

        index = _ids.Count;
        _indexes.Add(id, index);
        _ids.Add(id);
        return index;
    }

    public bool TryGet(Guid id, out int index) => _indexes.TryGetValue(id, out index);
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

/// <summary>
/// One sample server's probes as parallel arrays (time, ok, players, queue), sorted by time.
/// Failed probes have <c>Ok = false</c> and zero players.
/// </summary>
public sealed class ProbeSeries
{
    public static readonly ProbeSeries Empty = new([], [], [], []);

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
    public int Count => TimesMs.Length;
}

/// <summary>
/// One server's production polls as parallel arrays, sorted by time.
/// </summary>
public sealed class SnapshotSeries
{
    public static readonly SnapshotSeries Empty = new([], [], [], [], [], []);

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

    /// <summary><c>QuerySucceeded</c>: 1, 0, or -1 when null (rows before 2026-11-03).</summary>
    public sbyte[] Ok { get; }

    public ServerPollSource[] Sources { get; }
    public int Count => TimesMs.Length;

    /// <summary>Whether the query was answered; falls back to <c>online</c> where <c>ok</c> is null.</summary>
    public bool Answered(int i) => Ok[i] switch
    {
        1 => true,
        0 => false,
        _ => Online[i]
    };
}
