using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using RustIQ.Domain.Servers;
using RustIQ.Domain.Study;
using RustIQ.Simulator.Data;

namespace RustIQ.Tests.Simulation;

public sealed class SimulatorDataLoaderTests : IDisposable
{
    private static readonly Guid SampleA = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid SampleB = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid Pruned = Guid.Parse("00000000-0000-0000-0000-0000000000cc");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "rustiq-sim-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Load_ReadsOneExport()
    {
        var folder = WriteExport("e1", "2026-11-15 21:38:03+00", First());

        var data = SimulatorDataLoader.Load([folder]);

        Assert.Equal(2, data.SampleServers.Count);
        Assert.Equal(StudyStratum.Popular, data.SampleServers[0].Stratum);
        Assert.Equal([1000L, 16000L], data.Probes[0].TimesMs);
        Assert.False(data.Probes[0].Ok[1]);
        Assert.Equal(3, data.SnapshotCount);

        var a = data.Snapshots[data.SampleServers[0].ServerIndex];
        Assert.Equal([1000L, 2000L], a.TimesMs);
        Assert.Equal(ServerPollSource.Discovery, a.Sources[0]);
        Assert.Equal(-1, a.Ok[0]);
        Assert.True(a.Answered(0)); // ok is null, online = 1
        Assert.Equal(ServerPollSource.FastLane, a.Sources[1]);
    }

    [Fact]
    public void Load_UnionsExportsAndLetsTheLaterServerRowWin()
    {
        var first = WriteExport("e1", "2026-11-15 21:38:03+00", First());
        var second = WriteExport("e2", "2026-11-18 00:05:00+00", Second());

        // The order on the command line does not matter: exported_at decides which export is later.
        var data = SimulatorDataLoader.Load([second, first]);

        // Probes: union by (server_idx, t_ms).
        Assert.Equal([1000L, 16000L, 31000L], data.Probes[0].TimesMs);

        // Snapshots: union by id; the pruned server's snapshots survive from the first export.
        Assert.Equal(4, data.SnapshotCount);
        Assert.True(data.ServerIndex.TryGet(Pruned, out var prunedIndex));
        Assert.Equal(1, data.Snapshots[prunedIndex].Count);

        // Servers: later export wins; a server missing from the latest export is marked as pruned.
        Assert.True(data.ServerIndex.TryGet(SampleA, out var aIndex));
        Assert.Equal(55, data.Servers[aIndex]!.Players);
        Assert.True(data.Servers[aIndex]!.InLatestExport);
        Assert.False(data.Servers[prunedIndex]!.InLatestExport);

        // Study tables: union by id.
        Assert.Equal(3, data.PopulationSamples.Count);
    }

    [Fact]
    public void Load_FailsWhenARowCountDiffersFromTheManifest()
    {
        var files = First();
        var folder = WriteExport("e1", "2026-11-15 21:38:03+00", files, rowCountOverrides: new() { ["study_probes"] = 5 });

        var exception = Assert.Throws<InvalidDataException>(() => SimulatorDataLoader.Load([folder]));
        Assert.Contains("manifest says 5", exception.Message);
    }

    [Fact]
    public void Load_FailsWhenAChecksumDiffers()
    {
        var folder = WriteExport("e1", "2026-11-15 21:38:03+00", First());
        WriteGzip(Path.Combine(folder, "servers.csv.gz"), First()["servers"] + "\n\n");

        var exception = Assert.Throws<InvalidDataException>(() => SimulatorDataLoader.Load([folder]));
        Assert.Contains("sha256", exception.Message);
    }

    [Fact]
    public void Load_FailsWhenTheSampleDiffersBetweenExports()
    {
        var first = WriteExport("e1", "2026-11-15 21:38:03+00", First());
        var changed = Second();
        changed["study_sample_servers"] =
            "server_idx,server_id,stratum,peak_players,country,selected_at\n" +
            $"1,{SampleB},Popular,300,DE,2026-11-03 16:37:54.52+00\n" +
            $"2,{SampleA},Small,3,US,2026-11-03 16:37:54.52+00";
        var second = WriteExport("e2", "2026-11-18 00:05:00+00", changed);

        Assert.Throws<InvalidDataException>(() => SimulatorDataLoader.Load([first, second]));
    }

    private static Dictionary<string, string> First() => new()
    {
        ["study_sample_servers"] =
            "server_idx,server_id,stratum,peak_players,country,selected_at\n" +
            $"1,{SampleA},Popular,300,DE,2026-11-03 16:37:54.52+00\n" +
            $"2,{SampleB},Small,3,US,2026-11-03 16:37:54.52+00",
        ["study_probes"] =
            "server_idx,t_ms,ok,players,queue\n" +
            "1,16000,0,,\n" +
            "1,1000,1,50,2\n" +
            "2,1000,1,1,0",
        ["server_snapshots"] =
            "id,server_id,t_ms,players,max_players,online,ok,source\n" +
            $"10000000-0000-0000-0000-000000000001,{SampleA},2000,50,100,1,1,FastLane\n" +
            $"10000000-0000-0000-0000-000000000002,{SampleA},1000,49,100,1,,Discovery\n" +
            $"10000000-0000-0000-0000-000000000003,{Pruned},1500,0,0,0,0,VeryStaleOfflineLane",
        ["servers"] =
            "id,server_type,status,players,max_players,queue,created_at,last_seen_at,last_checked_at,last_discovered_at,last_wipe_at,next_wipe_at\n" +
            $"{SampleA},Community,Online,50,100,0,2026-10-01 00:00:00+00,2026-11-15 21:00:00+00,2026-11-15 21:00:00+00,,,\n" +
            $"{SampleB},Official,Online,1,100,0,2026-10-01 00:00:00+00,,,,,\n" +
            $"{Pruned},Modded,Offline,,,,2026-10-01 00:00:00+00,,,,,",
        ["server_rankings"] =
            "server_id,rank,peak_players,generated_at\n" +
            $"{SampleA},1,300,2026-11-09 13:15:46.044697+00",
        ["study_sample_rankings"] =
            "Id,SampledAtUtc,ServerId,Rank\n" +
            $"1,2026-11-03 17:15:15.510455+00,{SampleA},1\n" +
            $"2,2026-11-03 17:15:15.510455+00,{SampleB},",
        ["study_population_samples"] =
            "Id,SampledAtUtc,TotalServers,OnlineServers,OnlineServersWithPlayers\n" +
            "1,2026-11-03 16:37:53.521955+00,18542,10536,3408\n" +
            "2,2026-11-03 16:38:55.301336+00,18522,10550,3414",
        ["study_table_write_samples"] =
            "Id,SampledAtUtc,TableName,Inserts,Updates,Deletes\n" +
            "1,2026-11-03 16:37:53.521955+00,server_snapshots,1,2,3",
        ["study_server_wipe_observations"] =
            "id,server_id,observed_at,last_wipe_at,previous_last_wipe_at,previous_next_wipe_at,first_since_start,keywords\n" +
            $"1,{SampleA},2026-11-03 16:37:56.108481+00,2026-11-03 16:00:00+00,,,1,\"ptrak,EU,born1793721600\"",
        ["study_prediction_snapshots"] =
            "Id,ServerId,CapturedAtUtc,NextWipeAtUtc,DisplayedNextWipeAtUtc,LastWipeAtUtc\n" +
            $"1,{SampleA},2026-11-13 08:48:39.70367+00,2026-12-03 19:00:00+00,,"
    };

    /// <summary>A later export: the pruned server and its snapshot are gone, and server A has moved on.</summary>
    private static Dictionary<string, string> Second()
    {
        var files = First();
        files["study_probes"] =
            "server_idx,t_ms,ok,players,queue\n" +
            "1,16000,0,,\n" +
            "1,31000,1,55,0";
        files["server_snapshots"] =
            "id,server_id,t_ms,players,max_players,online,ok,source\n" +
            $"10000000-0000-0000-0000-000000000001,{SampleA},2000,50,100,1,1,FastLane\n" +
            $"10000000-0000-0000-0000-000000000004,{SampleA},31000,55,100,1,1,FastLane";
        files["servers"] =
            "id,server_type,status,players,max_players,queue,created_at,last_seen_at,last_checked_at,last_discovered_at,last_wipe_at,next_wipe_at\n" +
            $"{SampleA},Community,Online,55,100,0,2026-10-01 00:00:00+00,2026-11-17 21:00:00+00,2026-11-17 21:00:00+00,,,\n" +
            $"{SampleB},Official,Online,1,100,0,2026-10-01 00:00:00+00,,,,,";
        files["study_population_samples"] +=
            "\n3,2026-11-17 16:37:53.521955+00,18000,10000,3000";
        return files;
    }

    private string WriteExport(
        string name,
        string exportedAt,
        Dictionary<string, string> files,
        Dictionary<string, long>? rowCountOverrides = null)
    {
        var folder = Path.Combine(_root, name);
        Directory.CreateDirectory(folder);
        var manifest = new StringBuilder()
            .AppendLine("from: 2026-11-04 00:00+00")
            .AppendLine("to:   2026-11-15 21:36+00")
            .AppendLine($"exported_at: {exportedAt}")
            .AppendLine()
            .AppendLine("rows (without header):");
        var hashes = new StringBuilder().AppendLine("sha256:");

        foreach (var (table, content) in files)
        {
            var path = Path.Combine(folder, table + ".csv.gz");
            WriteGzip(path, content + "\n");
            var rows = rowCountOverrides?.GetValueOrDefault(table) is > 0 and var count
                ? count
                : content.Split('\n').Length - 1;
            manifest.AppendLine($"  {table}.csv.gz {rows}");
            hashes.AppendLine($"{Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))}  ./{table}.csv.gz");
        }

        File.WriteAllText(Path.Combine(folder, "manifest.txt"), manifest.AppendLine().Append(hashes).ToString());
        return folder;
    }

    private static void WriteGzip(string path, string content)
    {
        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionLevel.Fastest);
        gzip.Write(Encoding.UTF8.GetBytes(content));
    }
}
