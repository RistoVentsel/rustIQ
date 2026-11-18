using RustIQ.Simulator.Data;

namespace RustIQ.Tests.Simulation;

public sealed class CsvFileTests
{
    [Fact]
    public void Read_SplitsQuotedFieldsWithCommasAndEscapedQuotes()
    {
        var rows = ReadAll("a,b,c\n1,\"x,y \"\"z\"\"\",\n2,,last");

        Assert.Equal(2, rows.Count);
        Assert.Equal(["1", "x,y \"z\"", ""], rows[0]);
        Assert.Equal(["2", "", "last"], rows[1]);
    }

    [Fact]
    public void Read_FailsOnAnUnexpectedHeader()
    {
        var exception = Assert.Throws<InvalidDataException>(() => ReadAll("a,b,x\n1,2,3"));
        Assert.Contains("header", exception.Message);
    }

    [Fact]
    public void Read_FailsOnAWrongFieldCount()
    {
        Assert.Throws<InvalidDataException>(() => ReadAll("a,b,c\n1,2"));
        Assert.Throws<InvalidDataException>(() => ReadAll("a,b,c\n1,2,3,4"));
    }

    [Fact]
    public void PostgresTimestamp_ReadsHourOnlyOffsetsAsUtc()
    {
        var value = PostgresTimestamp.Parse("2026-11-09 13:15:46.044697+00");

        Assert.Equal(TimeSpan.Zero, value.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 13, 15, 46, TimeSpan.Zero).AddTicks(446970), value);
        Assert.Equal(DateTimeOffset.Parse("2026-11-04T00:00:00Z"), PostgresTimestamp.Parse("2026-11-04 00:00+00"));
    }

    [Fact]
    public void ExportManifest_ReadsRowCountsAndHashes()
    {
        var manifest = ExportManifest.Parse("folder",
        [
            "from: 2026-11-04 00:00+00",
            "to:   2026-11-15 21:36+00",
            "exported_at: 2026-11-15 21:38:03+00",
            "",
            "rows (without header):",
            "  server_rankings.csv.gz                   250",
            "",
            "sha256:",
            "EF46122a8d  ./server_rankings.csv.gz"
        ]);

        Assert.Equal("2026-11-04 00:00+00", manifest.From);
        Assert.Equal(250, manifest.ExpectedRows("server_rankings"));
        Assert.Equal("ef46122a8d", manifest.Sha256["server_rankings.csv.gz"]);
        Assert.Equal(DateTimeOffset.Parse("2026-11-15T21:38:03Z"), manifest.ExportedAtUtc);
    }

    private static List<string[]> ReadAll(string csv)
    {
        var rows = new List<string[]>();
        CsvFile.Read(new StringReader(csv), "a,b,c", row =>
            rows.Add(Enumerable.Range(0, row.FieldCount).Select(row.String).ToArray()));
        return rows;
    }
}
