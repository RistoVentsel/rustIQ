using RustIQ.Domain.Servers;
using RustIQ.Domain.Study;

namespace RustIQ.Simulator.Data;

public sealed class SimulatorDataLoadOptions
{
    /// <summary>Check every file's sha256 against its manifest. Row counts are always checked.</summary>
    public bool VerifyChecksums { get; init; } = true;

    public bool LoadProbes { get; init; } = true;

    public bool LoadSnapshots { get; init; } = true;

    public Action<string>? Log { get; init; }
}

/// <summary>
/// Loads one or more export folders and unions them. Retention deletes a pruned server with its
/// snapshots, so a later export lacks rows an earlier one has:
/// snapshots are merged by id, servers by id (the later export wins), probes by (server_idx, t_ms),
/// and the study tables by their id.
/// </summary>
public static class SimulatorDataLoader
{
    public const string SampleServersTable = "study_sample_servers";
    public const string ProbesTable = "study_probes";
    public const string SnapshotsTable = "server_snapshots";
    public const string ServersTable = "servers";
    public const string RankingsTable = "server_rankings";
    public const string SampleRankingsTable = "study_sample_rankings";
    public const string PopulationTable = "study_population_samples";
    public const string TableWritesTable = "study_table_write_samples";
    public const string WipeObservationsTable = "study_server_wipe_observations";
    public const string PredictionSnapshotsTable = "study_prediction_snapshots";

    private const string SampleServersHeader = "server_idx,server_id,stratum,peak_players,country,selected_at";
    private const string ProbesHeader = "server_idx,t_ms,ok,players,queue";
    private const string SnapshotsHeader = "id,server_id,t_ms,players,max_players,online,ok,source";
    private const string ServersHeader =
        "id,server_type,status,players,max_players,queue,created_at,last_seen_at,last_checked_at,last_discovered_at,last_wipe_at,next_wipe_at";
    private const string RankingsHeader = "server_id,rank,peak_players,generated_at";
    private const string SampleRankingsHeader = "Id,SampledAtUtc,ServerId,Rank";
    private const string PopulationHeader = "Id,SampledAtUtc,TotalServers,OnlineServers,OnlineServersWithPlayers";
    private const string TableWritesHeader = "Id,SampledAtUtc,TableName,Inserts,Updates,Deletes";
    private const string WipeObservationsHeader =
        "id,server_id,observed_at,last_wipe_at,previous_last_wipe_at,previous_next_wipe_at,first_since_start,keywords";
    private const string PredictionSnapshotsHeader =
        "Id,ServerId,CapturedAtUtc,NextWipeAtUtc,DisplayedNextWipeAtUtc,LastWipeAtUtc";

    public static SimulatorData Load(IReadOnlyList<string> folders, SimulatorDataLoadOptions? options = null)
    {
        options ??= new SimulatorDataLoadOptions();
        if (folders.Count == 0)
        {
            throw new ArgumentException("At least one export folder is needed.", nameof(folders));
        }

        // Oldest export first, so "later export wins" is simply "last write wins".
        var manifests = folders
            .Select(ExportManifest.Load)
            .OrderBy(manifest => manifest.ExportedAtUtc)
            .ToArray();
        var log = options.Log ?? (_ => { });

        if (options.VerifyChecksums)
        {
            var tables = AllTables(options);
            try
            {
                Parallel.ForEach(
                    manifests.SelectMany(manifest => tables.Select(table => (manifest, table))),
                    item => item.manifest.VerifyChecksum(item.table));
            }
            catch (AggregateException exception)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(exception.InnerExceptions[0]);
            }

            log($"sha256 ok for {manifests.Length} export(s)");
        }

        var serverIndex = new ServerIndex();
        var sampleServers = LoadSampleServers(manifests, serverIndex);
        log($"{SampleServersTable}: {sampleServers.Count} servers");

        // Small tables first: they also register server ids, so the big files only look them up.
        var servers = LoadServers(manifests, serverIndex);
        log($"{ServersTable}: {servers.Count} servers");

        var rankings = LoadSmall(manifests, RankingsTable, RankingsHeader,
            row => new ServerRankingRow(row.Guid(0), row.Int(1), row.Int(2), row.Timestamp(3)),
            keyOf: row => row.ServerId, latestOnly: true);
        var sampleRankings = LoadSmall(manifests, SampleRankingsTable, SampleRankingsHeader,
            row => new SampleRankingRow(row.Long(0), row.Timestamp(1), row.Guid(2), row.NullableInt(3)),
            keyOf: row => row.Id);
        var population = LoadSmall(manifests, PopulationTable, PopulationHeader,
            row => new PopulationSample(row.Long(0), row.Timestamp(1), row.Int(2), row.Int(3), row.Int(4)),
            keyOf: row => row.Id);
        var tableWrites = LoadSmall(manifests, TableWritesTable, TableWritesHeader,
            row => new TableWriteSample(row.Long(0), row.Timestamp(1), row.String(2), row.Long(3), row.Long(4), row.Long(5)),
            keyOf: row => row.Id);
        var wipeObservations = LoadSmall(manifests, WipeObservationsTable, WipeObservationsHeader,
            row => new WipeObservationRow(
                row.Long(0),
                row.Guid(1),
                row.Timestamp(2),
                row.NullableTimestamp(3),
                row.NullableTimestamp(4),
                row.NullableTimestamp(5),
                row.NullableBool(6) ?? false,
                row.NullableString(7)),
            keyOf: row => row.Id);
        var predictions = LoadSmall(manifests, PredictionSnapshotsTable, PredictionSnapshotsHeader,
            row => new PredictionSnapshotRow(
                row.Long(0),
                row.Guid(1),
                row.Timestamp(2),
                row.NullableTimestamp(3),
                row.NullableTimestamp(4),
                row.NullableTimestamp(5)),
            keyOf: row => row.Id);
        log($"small tables: {rankings.Count} rankings, {sampleRankings.Count} sample ranks, " +
            $"{population.Count} population rows, {tableWrites.Count} write rows, " +
            $"{wipeObservations.Count} wipe observations, {predictions.Count} prediction snapshots");

        foreach (var row in rankings) serverIndex.GetOrAdd(row.ServerId);
        foreach (var row in sampleRankings) serverIndex.GetOrAdd(row.ServerId);

        // The two big files are independent; read them side by side.
        IReadOnlyList<ProbeSeries> probes = [];
        SnapshotSeriesBuilder? snapshotBuilder = null;
        var probeTask = Task.Run(() =>
        {
            if (options.LoadProbes)
            {
                probes = LoadProbes(manifests, sampleServers.Count);
                log($"{ProbesTable}: {probes.Sum(series => (long)series.Count)} probes after merge");
            }
        });
        var snapshotTask = Task.Run(() =>
        {
            if (options.LoadSnapshots)
            {
                snapshotBuilder = LoadSnapshots(manifests, serverIndex);
            }
        });
        // GetResult rethrows the first failure itself rather than an AggregateException.
        Task.WhenAll(probeTask, snapshotTask).GetAwaiter().GetResult();

        var serverCount = serverIndex.Count;
        var snapshots = new SnapshotSeries[serverCount];
        for (var i = 0; i < serverCount; i++)
        {
            snapshots[i] = snapshotBuilder?.Build(i) ?? SnapshotSeries.Empty;
        }

        if (snapshotBuilder is not null)
        {
            log($"{SnapshotsTable}: {snapshots.Sum(series => (long)series.Count)} snapshots after merge " +
                $"({snapshotBuilder.Duplicates} duplicates dropped)");
        }

        var serverRecords = new ServerRecord?[serverCount];
        foreach (var record in servers.Values)
        {
            serverRecords[serverIndex.GetOrAdd(record.Id)] = record;
        }

        return new SimulatorData
        {
            Manifests = manifests,
            SampleServers = sampleServers,
            Probes = options.LoadProbes ? probes : Enumerable.Repeat(ProbeSeries.Empty, sampleServers.Count).ToArray(),
            ServerIndex = serverIndex,
            Servers = serverRecords,
            Snapshots = snapshots,
            Rankings = rankings,
            SampleRankings = sampleRankings,
            PopulationSamples = population.OrderBy(row => row.SampledAtUtc).ToArray(),
            TableWriteSamples = tableWrites.OrderBy(row => row.SampledAtUtc).ThenBy(row => row.TableName).ToArray(),
            WipeObservations = wipeObservations,
            PredictionSnapshots = predictions
        };
    }

    private static string[] AllTables(SimulatorDataLoadOptions options)
    {
        var tables = new List<string>
        {
            SampleServersTable, ServersTable, RankingsTable, SampleRankingsTable, PopulationTable,
            TableWritesTable, WipeObservationsTable, PredictionSnapshotsTable
        };
        if (options.LoadProbes) tables.Add(ProbesTable);
        if (options.LoadSnapshots) tables.Add(SnapshotsTable);
        return tables.ToArray();
    }

    private static IReadOnlyList<SampleServer> LoadSampleServers(IReadOnlyList<ExportManifest> manifests, ServerIndex serverIndex)
    {
        SampleServer[]? first = null;
        foreach (var manifest in manifests)
        {
            var rows = new List<SampleServer>();
            var count = CsvFile.Read(manifest.PathOf(SampleServersTable), SampleServersHeader, row =>
            {
                var serverId = row.Guid(1);
                rows.Add(new SampleServer(
                    row.Int(0),
                    serverId,
                    serverIndex.GetOrAdd(serverId),
                    Enum.Parse<StudyStratum>(row.String(2)),
                    row.Int(3),
                    row.NullableString(4),
                    row.Timestamp(5)));
            });
            manifest.VerifyRowCount(SampleServersTable, count);

            var ordered = rows.OrderBy(server => server.ServerIdx).ToArray();
            for (var i = 0; i < ordered.Length; i++)
            {
                if (ordered[i].ServerIdx != i + 1)
                {
                    throw new InvalidDataException($"{manifest.PathOf(SampleServersTable)}: server_idx is not 1..{ordered.Length}.");
                }
            }

            if (first is null)
            {
                first = ordered;
            }
            else if (!first.Select(server => (server.ServerIdx, server.ServerId))
                         .SequenceEqual(ordered.Select(server => (server.ServerIdx, server.ServerId))))
            {
                // server_idx must mean the same server in every export, or the probes cannot be merged.
                throw new InvalidDataException($"{manifest.Folder}: the sample differs from the earlier export.");
            }
        }

        return first!;
    }

    private static Dictionary<Guid, ServerRecord> LoadServers(IReadOnlyList<ExportManifest> manifests, ServerIndex serverIndex)
    {
        var servers = new Dictionary<Guid, ServerRecord>();
        for (var m = 0; m < manifests.Count; m++)
        {
            var manifest = manifests[m];
            var isLatest = m == manifests.Count - 1;
            var count = CsvFile.Read(manifest.PathOf(ServersTable), ServersHeader, row =>
            {
                var id = row.Guid(0);
                serverIndex.GetOrAdd(id);
                servers[id] = new ServerRecord(
                    id,
                    Enum.Parse<RustServerType>(row.String(1)),
                    Enum.Parse<ServerStatus>(row.String(2)),
                    // About 50 servers never answered and have no counts at all.
                    row.NullableInt(3) ?? 0,
                    row.NullableInt(4) ?? 0,
                    row.NullableInt(5) ?? 0,
                    row.Timestamp(6),
                    row.NullableTimestamp(7),
                    row.NullableTimestamp(8),
                    row.NullableTimestamp(9),
                    row.NullableTimestamp(10),
                    row.NullableTimestamp(11),
                    isLatest);
            });
            manifest.VerifyRowCount(ServersTable, count);
        }

        // A server only in an earlier export keeps InLatestExport = false: it was pruned before the latest one.
        return servers;
    }

    private static List<T> LoadSmall<T, TKey>(
        IReadOnlyList<ExportManifest> manifests,
        string table,
        string header,
        Func<CsvRow, T> parse,
        Func<T, TKey> keyOf,
        bool latestOnly = false)
        where TKey : notnull
    {
        // The study tables are never pruned, so a union by id keeps every row; a later copy of a row
        // replaces the earlier one. server_rankings holds only the latest ranking, so only the latest
        // export's copy is meaningful.
        var rows = new Dictionary<TKey, T>();
        var order = new List<TKey>();
        IReadOnlyList<ExportManifest> selected = latestOnly ? [manifests[^1]] : manifests;
        foreach (var manifest in manifests)
        {
            var parsed = new List<T>();
            var count = CsvFile.Read(manifest.PathOf(table), header, row => parsed.Add(parse(row)));
            manifest.VerifyRowCount(table, count);
            if (!selected.Contains(manifest))
            {
                continue;
            }

            foreach (var item in parsed)
            {
                var key = keyOf(item);
                if (!rows.ContainsKey(key))
                {
                    order.Add(key);
                }

                rows[key] = item;
            }
        }

        return order.Select(key => rows[key]).ToList();
    }

    private static IReadOnlyList<ProbeSeries> LoadProbes(IReadOnlyList<ExportManifest> manifests, int sampleCount)
    {
        var builders = new ProbeSeriesBuilder[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            builders[i] = new ProbeSeriesBuilder();
        }

        foreach (var manifest in manifests)
        {
            var count = CsvFile.Read(manifest.PathOf(ProbesTable), ProbesHeader, row =>
            {
                var idx = row.Int(0);
                if (idx < 1 || idx > sampleCount)
                {
                    throw new InvalidDataException($"{manifest.PathOf(ProbesTable)}: server_idx {idx} is not in the sample.");
                }

                var ok = row.NullableBool(2) ?? false;
                builders[idx - 1].Add(
                    row.Long(1),
                    ok,
                    (short)(row.NullableInt(3) ?? 0),
                    (short)(row.NullableInt(4) ?? 0));
            });
            manifest.VerifyRowCount(ProbesTable, count);
        }

        return builders.Select(builder => builder.Build()).ToArray();
    }

    private static SnapshotSeriesBuilder LoadSnapshots(IReadOnlyList<ExportManifest> manifests, ServerIndex serverIndex)
    {
        var builder = new SnapshotSeriesBuilder(dedupById: manifests.Count > 1);
        foreach (var manifest in manifests)
        {
            var count = CsvFile.Read(manifest.PathOf(SnapshotsTable), SnapshotsHeader, row =>
            {
                var serverId = row.Guid(1);
                // Only this task registers ids while the big files load.
                var index = serverIndex.GetOrAdd(serverId);

                builder.Add(
                    builder.DedupById ? row.Guid(0) : Guid.Empty,
                    index,
                    row.Long(2),
                    (short)(row.NullableInt(3) ?? 0),
                    (short)(row.NullableInt(4) ?? 0),
                    row.NullableBool(5) ?? false,
                    row.NullableBool(6),
                    ParsePollSource(row.Span(7)));
            });
            manifest.VerifyRowCount(SnapshotsTable, count);
        }

        return builder;
    }

    private static ServerPollSource ParsePollSource(ReadOnlySpan<char> text) =>
        text.IsEmpty ? ServerPollSource.Unknown : Enum.Parse<ServerPollSource>(text);

    private sealed class ProbeSeriesBuilder
    {
        private readonly List<long> _times = [];
        private readonly List<bool> _ok = [];
        private readonly List<short> _players = [];
        private readonly List<short> _queue = [];

        public void Add(long timeMs, bool ok, short players, short queue)
        {
            _times.Add(timeMs);
            _ok.Add(ok);
            _players.Add(players);
            _queue.Add(queue);
        }

        public ProbeSeries Build()
        {
            var order = SortedUniqueOrder(_times);
            var series = new ProbeSeries(
                order.Select(i => _times[i]).ToArray(),
                order.Select(i => _ok[i]).ToArray(),
                order.Select(i => _players[i]).ToArray(),
                order.Select(i => _queue[i]).ToArray());
            _times.Clear();
            _ok.Clear();
            _players.Clear();
            _queue.Clear();
            return series;
        }

        /// <summary>Indexes sorted by time; for equal times (the same probe in two exports) keeps the first.</summary>
        private static int[] SortedUniqueOrder(List<long> times)
        {
            var keys = times.ToArray();
            var order = Enumerable.Range(0, keys.Length).ToArray();
            Array.Sort(keys, order);
            // Array.Sort is not stable; equal times are the same probe, so any one of them will do.
            var unique = new List<int>(order.Length);
            for (var i = 0; i < order.Length; i++)
            {
                if (i == 0 || keys[i] != keys[i - 1])
                {
                    unique.Add(order[i]);
                }
            }

            return unique.ToArray();
        }
    }

    private sealed class SnapshotSeriesBuilder(bool dedupById)
    {
        private readonly HashSet<Guid> _seenIds = [];
        private List<Row>?[] _rows = new List<Row>?[1024];

        public bool DedupById { get; } = dedupById;

        public long Duplicates { get; private set; }

        public void Add(
            Guid id,
            int serverIndex,
            long timeMs,
            short players,
            short maxPlayers,
            bool online,
            bool? ok,
            ServerPollSource source)
        {
            if (DedupById && !_seenIds.Add(id))
            {
                Duplicates++;
                return;
            }

            if (serverIndex >= _rows.Length)
            {
                Array.Resize(ref _rows, Math.Max(_rows.Length * 2, serverIndex + 1));
            }

            (_rows[serverIndex] ??= []).Add(new Row(timeMs, players, maxPlayers, online, ok switch
            {
                true => (sbyte)1,
                false => (sbyte)0,
                null => (sbyte)-1
            }, source));
        }

        public SnapshotSeries Build(int serverIndex)
        {
            if (serverIndex >= _rows.Length || _rows[serverIndex] is not { Count: > 0 } rows)
            {
                return SnapshotSeries.Empty;
            }

            var sorted = rows.ToArray();
            // Stable by time, so two polls in the same millisecond keep their export order.
            var positions = Enumerable.Range(0, sorted.Length).ToArray();
            Array.Sort(positions, (a, b) => sorted[a].TimeMs != sorted[b].TimeMs
                ? sorted[a].TimeMs.CompareTo(sorted[b].TimeMs)
                : a.CompareTo(b));
            _rows[serverIndex] = null;

            return new SnapshotSeries(
                positions.Select(i => sorted[i].TimeMs).ToArray(),
                positions.Select(i => sorted[i].Players).ToArray(),
                positions.Select(i => sorted[i].MaxPlayers).ToArray(),
                positions.Select(i => sorted[i].Online).ToArray(),
                positions.Select(i => sorted[i].Ok).ToArray(),
                positions.Select(i => sorted[i].Source).ToArray());
        }

        private readonly record struct Row(
            long TimeMs,
            short Players,
            short MaxPlayers,
            bool Online,
            sbyte Ok,
            ServerPollSource Source);
    }
}
