using System.Globalization;
using System.Security.Cryptography;

namespace RustIQ.Simulator.Data;

/// <summary>
/// The <c>manifest.txt</c> of one export folder: its period, row count and sha256 per file.
/// </summary>
public sealed record ExportManifest(
    string Folder,
    string From,
    string To,
    DateTimeOffset ExportedAtUtc,
    IReadOnlyDictionary<string, long> RowCounts,
    IReadOnlyDictionary<string, string> Sha256)
{
    public const string FileName = "manifest.txt";

    public static ExportManifest Load(string folder)
    {
        var path = Path.Combine(folder, FileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Export folder has no {FileName}.", path);
        }

        return Parse(folder, File.ReadAllLines(path));
    }

    public static ExportManifest Parse(string folder, IEnumerable<string> lines)
    {
        string? from = null;
        string? to = null;
        DateTimeOffset? exportedAt = null;
        var rowCounts = new Dictionary<string, long>(StringComparer.Ordinal);
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var section = "";

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("from:", StringComparison.Ordinal))
            {
                from = line["from:".Length..].Trim();
            }
            else if (line.StartsWith("to:", StringComparison.Ordinal))
            {
                to = line["to:".Length..].Trim();
            }
            else if (line.StartsWith("exported_at:", StringComparison.Ordinal))
            {
                exportedAt = PostgresTimestamp.Parse(line["exported_at:".Length..].Trim());
            }
            else if (line.StartsWith("rows", StringComparison.Ordinal))
            {
                section = "rows";
            }
            else if (line.StartsWith("sha256", StringComparison.Ordinal))
            {
                section = "sha256";
            }
            else
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                {
                    throw new InvalidDataException($"{folder}/{FileName}: cannot read line '{line}'.");
                }

                if (section == "rows")
                {
                    rowCounts[parts[0]] = long.Parse(parts[1], CultureInfo.InvariantCulture);
                }
                else if (section == "sha256")
                {
                    var fileName = parts[1].StartsWith("./", StringComparison.Ordinal) ? parts[1][2..] : parts[1];
                    hashes[fileName] = parts[0].ToLowerInvariant();
                }
            }
        }

        if (from is null || to is null || exportedAt is null)
        {
            throw new InvalidDataException($"{folder}/{FileName}: from, to or exported_at is missing.");
        }

        return new ExportManifest(folder, from, to, exportedAt.Value, rowCounts, hashes);
    }

    public string PathOf(string table) => Path.Combine(Folder, table + ".csv.gz");

    public long ExpectedRows(string table)
    {
        var fileName = table + ".csv.gz";
        return RowCounts.TryGetValue(fileName, out var rows)
            ? rows
            : throw new InvalidDataException($"{Folder}/{FileName} has no row count for {fileName}.");
    }

    /// <summary>Checks a file's sha256 against the manifest.</summary>
    public void VerifyChecksum(string table)
    {
        var fileName = table + ".csv.gz";
        if (!Sha256.TryGetValue(fileName, out var expected))
        {
            throw new InvalidDataException($"{Folder}/{FileName} has no sha256 for {fileName}.");
        }

        using var stream = File.OpenRead(PathOf(table));
        var actual = Convert.ToHexStringLower(SHA256.HashData(stream));
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{PathOf(table)}: sha256 is {actual}, manifest says {expected}.");
        }
    }

    public void VerifyRowCount(string table, long actualRows)
    {
        var expected = ExpectedRows(table);
        if (actualRows != expected)
        {
            throw new InvalidDataException($"{PathOf(table)}: {actualRows} rows read, manifest says {expected}.");
        }
    }
}
