using System.Runtime.ExceptionServices;
using RustIQ.Domain.Servers;
using RustIQ.Domain.Study;

namespace RustIQ.Simulator.Data;

public sealed class SimulatorDataLoadOptions
{
    public bool VerifyChecksums { get; init; } = true;

    public bool LoadProbes { get; init; } = true;

    public bool LoadSnapshots { get; init; } = true;

    public Action<string>? Log { get; init; }
}

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
        if (options == null)
        {
            options = new SimulatorDataLoadOptions();
        }

        if (folders.Count == 0)
        {
            throw new ArgumentException("At least one export folder is needed.", nameof(folders));
        }

        ExportManifest[] manifests = new ExportManifest[folders.Count];
        for (int i = 0; i < folders.Count; i++)
        {
            manifests[i] = ExportManifest.Load(folders[i]);
        }

        SortByExportTime(manifests);

        Action<string> log;
        if (options.Log != null)
        {
            log = options.Log;
        }
        else
        {
            log = message => { };
        }

        if (options.VerifyChecksums)
        {
            string[] tables = AllTables(options);
            List<ExportManifest> checkManifests = new List<ExportManifest>();
            List<string> checkTables = new List<string>();
            foreach (ExportManifest manifest in manifests)
            {
                foreach (string table in tables)
                {
                    checkManifests.Add(manifest);
                    checkTables.Add(table);
                }
            }

            try
            {
                Parallel.For(0, checkManifests.Count, i => checkManifests[i].VerifyChecksum(checkTables[i]));
            }
            catch (AggregateException exception)
            {
                ExceptionDispatchInfo.Throw(exception.InnerExceptions[0]);
            }

            log($"sha256 ok for {manifests.Length} export(s)");
        }

        ServerIndex serverIndex = new ServerIndex();
        IReadOnlyList<SampleServer> sampleServers = LoadSampleServers(manifests, serverIndex);
        log($"{SampleServersTable}: {sampleServers.Count} servers");

        Dictionary<Guid, ServerRecord> servers = LoadServers(manifests, serverIndex);
        log($"{ServersTable}: {servers.Count} servers");

        List<ServerRankingRow> rankings = LoadSmall<ServerRankingRow, Guid>(
            manifests, RankingsTable, RankingsHeader, ParseRanking, RankingKey, true);
        List<SampleRankingRow> sampleRankings = LoadSmall<SampleRankingRow, long>(
            manifests, SampleRankingsTable, SampleRankingsHeader, ParseSampleRanking, SampleRankingKey);
        List<PopulationSample> population = LoadSmall<PopulationSample, long>(
            manifests, PopulationTable, PopulationHeader, ParsePopulation, PopulationKey);
        List<TableWriteSample> tableWrites = LoadSmall<TableWriteSample, long>(
            manifests, TableWritesTable, TableWritesHeader, ParseTableWrite, TableWriteKey);
        List<WipeObservationRow> wipeObservations = LoadSmall<WipeObservationRow, long>(
            manifests, WipeObservationsTable, WipeObservationsHeader, ParseWipeObservation, WipeObservationKey);
        List<PredictionSnapshotRow> predictions = LoadSmall<PredictionSnapshotRow, long>(
            manifests, PredictionSnapshotsTable, PredictionSnapshotsHeader, ParsePrediction, PredictionKey);
        log($"small tables: {rankings.Count} rankings, {sampleRankings.Count} sample ranks, " +
            $"{population.Count} population rows, {tableWrites.Count} write rows, " +
            $"{wipeObservations.Count} wipe observations, {predictions.Count} prediction snapshots");

        foreach (ServerRankingRow row in rankings)
        {
            serverIndex.GetOrAdd(row.ServerId);
        }

        foreach (SampleRankingRow row in sampleRankings)
        {
            serverIndex.GetOrAdd(row.ServerId);
        }

        IReadOnlyList<ProbeSeries> probes = new ProbeSeries[0];
        SnapshotSeriesBuilder? snapshotBuilder = null;
        Task probeTask = Task.Run(() =>
        {
            if (options.LoadProbes)
            {
                probes = LoadProbes(manifests, sampleServers.Count);
                log($"{ProbesTable}: {CountProbes(probes)} probes after merge");
            }
        });
        Task snapshotTask = Task.Run(() =>
        {
            if (options.LoadSnapshots)
            {
                snapshotBuilder = LoadSnapshots(manifests, serverIndex);
            }
        });
        Task.WhenAll(probeTask, snapshotTask).GetAwaiter().GetResult();

        int serverCount = serverIndex.Count;
        SnapshotSeries[] snapshots = new SnapshotSeries[serverCount];
        for (int i = 0; i < serverCount; i++)
        {
            if (snapshotBuilder != null)
            {
                snapshots[i] = snapshotBuilder.Build(i);
            }
            else
            {
                snapshots[i] = SnapshotSeries.Empty;
            }
        }

        if (snapshotBuilder != null)
        {
            log($"{SnapshotsTable}: {CountSnapshots(snapshots)} snapshots after merge " +
                $"({snapshotBuilder.Duplicates} duplicates dropped)");
        }

        ServerRecord?[] serverRecords = new ServerRecord?[serverCount];
        foreach (ServerRecord record in servers.Values)
        {
            serverRecords[serverIndex.GetOrAdd(record.Id)] = record;
        }

        IReadOnlyList<ProbeSeries> finalProbes;
        if (options.LoadProbes)
        {
            finalProbes = probes;
        }
        else
        {
            ProbeSeries[] emptyProbes = new ProbeSeries[sampleServers.Count];
            for (int i = 0; i < emptyProbes.Length; i++)
            {
                emptyProbes[i] = ProbeSeries.Empty;
            }

            finalProbes = emptyProbes;
        }

        return new SimulatorData
        {
            Manifests = manifests,
            SampleServers = sampleServers,
            Probes = finalProbes,
            ServerIndex = serverIndex,
            Servers = serverRecords,
            Snapshots = snapshots,
            Rankings = rankings,
            SampleRankings = sampleRankings,
            PopulationSamples = SortPopulation(population),
            TableWriteSamples = SortTableWrites(tableWrites),
            WipeObservations = wipeObservations,
            PredictionSnapshots = predictions
        };
    }

    private static void SortByExportTime(ExportManifest[] manifests)
    {
        for (int i = 1; i < manifests.Length; i++)
        {
            ExportManifest current = manifests[i];
            int j = i - 1;
            while (j >= 0 && manifests[j].ExportedAtUtc > current.ExportedAtUtc)
            {
                manifests[j + 1] = manifests[j];
                j--;
            }

            manifests[j + 1] = current;
        }
    }

    private static PopulationSample[] SortPopulation(List<PopulationSample> rows)
    {
        int[] positions = new int[rows.Count];
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] = i;
        }

        Array.Sort(positions, (a, b) =>
        {
            int result = rows[a].SampledAtUtc.CompareTo(rows[b].SampledAtUtc);
            if (result != 0)
            {
                return result;
            }

            return a.CompareTo(b);
        });

        PopulationSample[] sorted = new PopulationSample[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            sorted[i] = rows[positions[i]];
        }

        return sorted;
    }

    private static TableWriteSample[] SortTableWrites(List<TableWriteSample> rows)
    {
        int[] positions = new int[rows.Count];
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] = i;
        }

        Comparer<string> nameComparer = Comparer<string>.Default;
        Array.Sort(positions, (a, b) =>
        {
            int result = rows[a].SampledAtUtc.CompareTo(rows[b].SampledAtUtc);
            if (result != 0)
            {
                return result;
            }

            result = nameComparer.Compare(rows[a].TableName, rows[b].TableName);
            if (result != 0)
            {
                return result;
            }

            return a.CompareTo(b);
        });

        TableWriteSample[] sorted = new TableWriteSample[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            sorted[i] = rows[positions[i]];
        }

        return sorted;
    }

    private static long CountProbes(IReadOnlyList<ProbeSeries> probes)
    {
        long total = 0;
        foreach (ProbeSeries series in probes)
        {
            total += series.Count;
        }

        return total;
    }

    private static long CountSnapshots(SnapshotSeries[] snapshots)
    {
        long total = 0;
        foreach (SnapshotSeries series in snapshots)
        {
            total += series.Count;
        }

        return total;
    }

    private static string[] AllTables(SimulatorDataLoadOptions options)
    {
        List<string> tables = new List<string>();
        tables.Add(SampleServersTable);
        tables.Add(ServersTable);
        tables.Add(RankingsTable);
        tables.Add(SampleRankingsTable);
        tables.Add(PopulationTable);
        tables.Add(TableWritesTable);
        tables.Add(WipeObservationsTable);
        tables.Add(PredictionSnapshotsTable);
        if (options.LoadProbes)
        {
            tables.Add(ProbesTable);
        }

        if (options.LoadSnapshots)
        {
            tables.Add(SnapshotsTable);
        }

        return tables.ToArray();
    }

    private static IReadOnlyList<SampleServer> LoadSampleServers(IReadOnlyList<ExportManifest> manifests, ServerIndex serverIndex)
    {
        SampleServer[]? first = null;
        foreach (ExportManifest manifest in manifests)
        {
            List<SampleServer> rows = new List<SampleServer>();
            long count = CsvFile.Read(manifest.PathOf(SampleServersTable), SampleServersHeader, row =>
            {
                Guid serverId = row.Guid(1);
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

            SampleServer[] ordered = rows.ToArray();
            Array.Sort(ordered, CompareByServerIdx);
            for (int i = 0; i < ordered.Length; i++)
            {
                if (ordered[i].ServerIdx != i + 1)
                {
                    throw new InvalidDataException($"{manifest.PathOf(SampleServersTable)}: server_idx is not 1..{ordered.Length}.");
                }
            }

            if (first == null)
            {
                first = ordered;
            }
            else if (!SameSample(first, ordered))
            {
                throw new InvalidDataException($"{manifest.Folder}: the sample differs from the earlier export.");
            }
        }

        return first!;
    }

    private static int CompareByServerIdx(SampleServer a, SampleServer b)
    {
        return a.ServerIdx.CompareTo(b.ServerIdx);
    }

    private static bool SameSample(SampleServer[] first, SampleServer[] other)
    {
        if (first.Length != other.Length)
        {
            return false;
        }

        for (int i = 0; i < first.Length; i++)
        {
            if (first[i].ServerIdx != other[i].ServerIdx || first[i].ServerId != other[i].ServerId)
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<Guid, ServerRecord> LoadServers(IReadOnlyList<ExportManifest> manifests, ServerIndex serverIndex)
    {
        Dictionary<Guid, ServerRecord> servers = new Dictionary<Guid, ServerRecord>();
        for (int m = 0; m < manifests.Count; m++)
        {
            ExportManifest manifest = manifests[m];
            bool isLatest = m == manifests.Count - 1;
            long count = CsvFile.Read(manifest.PathOf(ServersTable), ServersHeader, row =>
            {
                Guid id = row.Guid(0);
                serverIndex.GetOrAdd(id);
                servers[id] = new ServerRecord(
                    id,
                    Enum.Parse<RustServerType>(row.String(1)),
                    Enum.Parse<ServerStatus>(row.String(2)),
                    IntOrZero(row, 3),
                    IntOrZero(row, 4),
                    IntOrZero(row, 5),
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
        Dictionary<TKey, T> rows = new Dictionary<TKey, T>();
        List<TKey> order = new List<TKey>();
        for (int m = 0; m < manifests.Count; m++)
        {
            ExportManifest manifest = manifests[m];
            List<T> parsed = new List<T>();
            long count = CsvFile.Read(manifest.PathOf(table), header, row => parsed.Add(parse(row)));
            manifest.VerifyRowCount(table, count);
            if (latestOnly && m != manifests.Count - 1)
            {
                continue;
            }

            foreach (T item in parsed)
            {
                TKey key = keyOf(item);
                if (!rows.ContainsKey(key))
                {
                    order.Add(key);
                }

                rows[key] = item;
            }
        }

        List<T> result = new List<T>();
        foreach (TKey key in order)
        {
            result.Add(rows[key]);
        }

        return result;
    }

    private static ServerRankingRow ParseRanking(CsvRow row)
    {
        return new ServerRankingRow(row.Guid(0), row.Int(1), row.Int(2), row.Timestamp(3));
    }

    private static Guid RankingKey(ServerRankingRow row)
    {
        return row.ServerId;
    }

    private static SampleRankingRow ParseSampleRanking(CsvRow row)
    {
        return new SampleRankingRow(row.Long(0), row.Timestamp(1), row.Guid(2), row.NullableInt(3));
    }

    private static long SampleRankingKey(SampleRankingRow row)
    {
        return row.Id;
    }

    private static PopulationSample ParsePopulation(CsvRow row)
    {
        return new PopulationSample(row.Long(0), row.Timestamp(1), row.Int(2), row.Int(3), row.Int(4));
    }

    private static long PopulationKey(PopulationSample row)
    {
        return row.Id;
    }

    private static TableWriteSample ParseTableWrite(CsvRow row)
    {
        return new TableWriteSample(row.Long(0), row.Timestamp(1), row.String(2), row.Long(3), row.Long(4), row.Long(5));
    }

    private static long TableWriteKey(TableWriteSample row)
    {
        return row.Id;
    }

    private static WipeObservationRow ParseWipeObservation(CsvRow row)
    {
        return new WipeObservationRow(
            row.Long(0),
            row.Guid(1),
            row.Timestamp(2),
            row.NullableTimestamp(3),
            row.NullableTimestamp(4),
            row.NullableTimestamp(5),
            BoolOrFalse(row, 6),
            row.NullableString(7));
    }

    private static long WipeObservationKey(WipeObservationRow row)
    {
        return row.Id;
    }

    private static PredictionSnapshotRow ParsePrediction(CsvRow row)
    {
        return new PredictionSnapshotRow(
            row.Long(0),
            row.Guid(1),
            row.Timestamp(2),
            row.NullableTimestamp(3),
            row.NullableTimestamp(4),
            row.NullableTimestamp(5));
    }

    private static long PredictionKey(PredictionSnapshotRow row)
    {
        return row.Id;
    }

    private static int IntOrZero(CsvRow row, int index)
    {
        int? value = row.NullableInt(index);
        if (value == null)
        {
            return 0;
        }

        return value.Value;
    }

    private static bool BoolOrFalse(CsvRow row, int index)
    {
        bool? value = row.NullableBool(index);
        if (value == null)
        {
            return false;
        }

        return value.Value;
    }

    private static IReadOnlyList<ProbeSeries> LoadProbes(IReadOnlyList<ExportManifest> manifests, int sampleCount)
    {
        ProbeSeriesBuilder[] builders = new ProbeSeriesBuilder[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            builders[i] = new ProbeSeriesBuilder();
        }

        foreach (ExportManifest manifest in manifests)
        {
            long count = CsvFile.Read(manifest.PathOf(ProbesTable), ProbesHeader, row =>
            {
                int idx = row.Int(0);
                if (idx < 1 || idx > sampleCount)
                {
                    throw new InvalidDataException($"{manifest.PathOf(ProbesTable)}: server_idx {idx} is not in the sample.");
                }

                bool ok = BoolOrFalse(row, 2);
                builders[idx - 1].Add(
                    row.Long(1),
                    ok,
                    (short)IntOrZero(row, 3),
                    (short)IntOrZero(row, 4));
            });
            manifest.VerifyRowCount(ProbesTable, count);
        }

        ProbeSeries[] result = new ProbeSeries[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            result[i] = builders[i].Build();
        }

        return result;
    }

    private static SnapshotSeriesBuilder LoadSnapshots(IReadOnlyList<ExportManifest> manifests, ServerIndex serverIndex)
    {
        SnapshotSeriesBuilder builder = new SnapshotSeriesBuilder(manifests.Count > 1);
        foreach (ExportManifest manifest in manifests)
        {
            long count = CsvFile.Read(manifest.PathOf(SnapshotsTable), SnapshotsHeader, row =>
            {
                Guid serverId = row.Guid(1);
                int index = serverIndex.GetOrAdd(serverId);

                Guid snapshotId = Guid.Empty;
                if (builder.DedupById)
                {
                    snapshotId = row.Guid(0);
                }

                builder.Add(
                    snapshotId,
                    index,
                    row.Long(2),
                    (short)IntOrZero(row, 3),
                    (short)IntOrZero(row, 4),
                    BoolOrFalse(row, 5),
                    row.NullableBool(6),
                    ParsePollSource(row.Span(7)));
            });
            manifest.VerifyRowCount(SnapshotsTable, count);
        }

        return builder;
    }

    private static ServerPollSource ParsePollSource(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return ServerPollSource.Unknown;
        }

        return Enum.Parse<ServerPollSource>(text);
    }

    private sealed class ProbeSeriesBuilder
    {
        private readonly List<long> _times = new List<long>();
        private readonly List<bool> _ok = new List<bool>();
        private readonly List<short> _players = new List<short>();
        private readonly List<short> _queue = new List<short>();

        public void Add(long timeMs, bool ok, short players, short queue)
        {
            _times.Add(timeMs);
            _ok.Add(ok);
            _players.Add(players);
            _queue.Add(queue);
        }

        public ProbeSeries Build()
        {
            int[] order = SortedUniqueOrder(_times);
            long[] times = new long[order.Length];
            bool[] ok = new bool[order.Length];
            short[] players = new short[order.Length];
            short[] queue = new short[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                int position = order[i];
                times[i] = _times[position];
                ok[i] = _ok[position];
                players[i] = _players[position];
                queue[i] = _queue[position];
            }

            ProbeSeries series = new ProbeSeries(times, ok, players, queue);
            _times.Clear();
            _ok.Clear();
            _players.Clear();
            _queue.Clear();
            return series;
        }

        private static int[] SortedUniqueOrder(List<long> times)
        {
            long[] keys = times.ToArray();
            int[] order = new int[keys.Length];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            Array.Sort(keys, order);
            List<int> unique = new List<int>(order.Length);
            for (int i = 0; i < order.Length; i++)
            {
                if (i == 0 || keys[i] != keys[i - 1])
                {
                    unique.Add(order[i]);
                }
            }

            return unique.ToArray();
        }
    }

    private sealed class SnapshotSeriesBuilder
    {
        private readonly HashSet<Guid> _seenIds = new HashSet<Guid>();
        private List<Row>?[] _rows = new List<Row>?[1024];

        public SnapshotSeriesBuilder(bool dedupById)
        {
            DedupById = dedupById;
        }

        public bool DedupById { get; }

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

            sbyte okValue;
            if (ok == null)
            {
                okValue = -1;
            }
            else if (ok.Value)
            {
                okValue = 1;
            }
            else
            {
                okValue = 0;
            }

            List<Row>? list = _rows[serverIndex];
            if (list == null)
            {
                list = new List<Row>();
                _rows[serverIndex] = list;
            }

            list.Add(new Row(timeMs, players, maxPlayers, online, okValue, source));
        }

        public SnapshotSeries Build(int serverIndex)
        {
            if (serverIndex >= _rows.Length)
            {
                return SnapshotSeries.Empty;
            }

            List<Row>? rows = _rows[serverIndex];
            if (rows == null || rows.Count == 0)
            {
                return SnapshotSeries.Empty;
            }

            Row[] sorted = rows.ToArray();
            int[] positions = new int[sorted.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = i;
            }

            Array.Sort(positions, (a, b) =>
            {
                if (sorted[a].TimeMs != sorted[b].TimeMs)
                {
                    return sorted[a].TimeMs.CompareTo(sorted[b].TimeMs);
                }

                return a.CompareTo(b);
            });
            _rows[serverIndex] = null;

            int length = positions.Length;
            long[] times = new long[length];
            short[] players = new short[length];
            short[] maxPlayers = new short[length];
            bool[] online = new bool[length];
            sbyte[] ok = new sbyte[length];
            ServerPollSource[] sources = new ServerPollSource[length];
            for (int i = 0; i < length; i++)
            {
                Row row = sorted[positions[i]];
                times[i] = row.TimeMs;
                players[i] = row.Players;
                maxPlayers[i] = row.MaxPlayers;
                online[i] = row.Online;
                ok[i] = row.Ok;
                sources[i] = row.Source;
            }

            return new SnapshotSeries(times, players, maxPlayers, online, ok, sources);
        }

        private readonly struct Row
        {
            public readonly long TimeMs;
            public readonly short Players;
            public readonly short MaxPlayers;
            public readonly bool Online;
            public readonly sbyte Ok;
            public readonly ServerPollSource Source;

            public Row(long timeMs, short players, short maxPlayers, bool online, sbyte ok, ServerPollSource source)
            {
                TimeMs = timeMs;
                Players = players;
                MaxPlayers = maxPlayers;
                Online = online;
                Ok = ok;
                Source = source;
            }
        }
    }
}
