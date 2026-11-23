using System.Globalization;
using System.Security.Cryptography;

namespace RustIQ.Simulator.Data;

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
        string path = Path.Combine(folder, FileName);
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
        Dictionary<string, long> rowCounts = new Dictionary<string, long>(StringComparer.Ordinal);
        Dictionary<string, string> hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        string section = "";

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("from:", StringComparison.Ordinal))
            {
                from = line.Substring("from:".Length).Trim();
            }
            else if (line.StartsWith("to:", StringComparison.Ordinal))
            {
                to = line.Substring("to:".Length).Trim();
            }
            else if (line.StartsWith("exported_at:", StringComparison.Ordinal))
            {
                exportedAt = PostgresTimestamp.Parse(line.Substring("exported_at:".Length).Trim());
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
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
                    string fileName = parts[1];
                    if (fileName.StartsWith("./", StringComparison.Ordinal))
                    {
                        fileName = fileName.Substring(2);
                    }

                    hashes[fileName] = parts[0].ToLowerInvariant();
                }
            }
        }

        if (from == null || to == null || exportedAt == null)
        {
            throw new InvalidDataException($"{folder}/{FileName}: from, to or exported_at is missing.");
        }

        return new ExportManifest(folder, from, to, exportedAt.Value, rowCounts, hashes);
    }

    public string PathOf(string table)
    {
        return Path.Combine(Folder, table + ".csv.gz");
    }

    public long ExpectedRows(string table)
    {
        string fileName = table + ".csv.gz";
        long rows;
        if (!RowCounts.TryGetValue(fileName, out rows))
        {
            throw new InvalidDataException($"{Folder}/{FileName} has no row count for {fileName}.");
        }

        return rows;
    }

    public void VerifyChecksum(string table)
    {
        string fileName = table + ".csv.gz";
        string? expected;
        if (!Sha256.TryGetValue(fileName, out expected))
        {
            throw new InvalidDataException($"{Folder}/{FileName} has no sha256 for {fileName}.");
        }

        using FileStream stream = File.OpenRead(PathOf(table));
        string actual = Convert.ToHexStringLower(SHA256.HashData(stream));
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{PathOf(table)}: sha256 is {actual}, manifest says {expected}.");
        }
    }

    public void VerifyRowCount(string table, long actualRows)
    {
        long expected = ExpectedRows(table);
        if (actualRows != expected)
        {
            throw new InvalidDataException($"{PathOf(table)}: {actualRows} rows read, manifest says {expected}.");
        }
    }
}
