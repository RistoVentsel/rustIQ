using System.Globalization;
using System.IO.Compression;

namespace RustIQ.Simulator.Data;

/// <summary>
/// Reads the gzip CSV files written by <c>export-simulator-data.sh</c> (PostgreSQL <c>COPY ... CSV HEADER</c>).
/// </summary>
public static class CsvFile
{
    /// <summary>
    /// Calls <paramref name="onRow"/> for every data row. The header is checked against
    /// <paramref name="expectedHeader"/> so a changed export fails loudly instead of shifting columns.
    /// </summary>
    /// <returns>The number of data rows.</returns>
    public static long Read(string path, string expectedHeader, Action<CsvRow> onRow)
    {
        using var file = File.OpenRead(path);
        Stream stream = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(file, CompressionMode.Decompress)
            : file;
        using var reader = new StreamReader(stream, bufferSize: 1 << 16);
        return Read(reader, expectedHeader, onRow, path);
    }

    public static long Read(TextReader reader, string expectedHeader, Action<CsvRow> onRow, string sourceName = "csv")
    {
        var header = reader.ReadLine()?.TrimStart('﻿');
        if (!string.Equals(header, expectedHeader, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{sourceName}: header is '{header}', expected '{expectedHeader}'.");
        }

        var fieldCount = expectedHeader.Split(',').Length;
        var row = new CsvRow(fieldCount);
        long rows = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            // A quoted field can contain a line break; keep reading until the quotes balance.
            while (HasOpenQuote(line))
            {
                var next = reader.ReadLine()
                    ?? throw new InvalidDataException($"{sourceName}: unterminated quote on row {rows + 1}.");
                line = line + "\n" + next;
            }

            row.Parse(line, rows + 1, sourceName);
            onRow(row);
            rows++;
        }

        return rows;
    }

    private static bool HasOpenQuote(string line)
    {
        if (line.IndexOf('"') < 0)
        {
            return false;
        }

        var open = false;
        foreach (var c in line)
        {
            if (c == '"')
            {
                open = !open;
            }
        }

        return open;
    }
}

/// <summary>
/// One parsed CSV row. The instance is reused between rows, so callers copy what they keep.
/// </summary>
public sealed class CsvRow
{
    private readonly int[] _starts;
    private readonly int[] _lengths;
    private readonly string?[] _unquoted;
    private string _line = string.Empty;

    public CsvRow(int fieldCount)
    {
        _starts = new int[fieldCount];
        _lengths = new int[fieldCount];
        _unquoted = new string?[fieldCount];
    }

    public int FieldCount => _starts.Length;

    internal void Parse(string line, long rowNumber, string sourceName)
    {
        _line = line;
        var field = 0;
        var position = 0;
        while (true)
        {
            if (field >= _starts.Length)
            {
                throw new InvalidDataException($"{sourceName}: row {rowNumber} has more than {_starts.Length} fields.");
            }

            _unquoted[field] = null;
            if (position < line.Length && line[position] == '"')
            {
                var builder = new System.Text.StringBuilder();
                position++;
                while (true)
                {
                    if (position >= line.Length)
                    {
                        throw new InvalidDataException($"{sourceName}: row {rowNumber} has an unterminated quote.");
                    }

                    if (line[position] == '"')
                    {
                        if (position + 1 < line.Length && line[position + 1] == '"')
                        {
                            builder.Append('"');
                            position += 2;
                            continue;
                        }

                        position++;
                        break;
                    }

                    builder.Append(line[position]);
                    position++;
                }

                _unquoted[field] = builder.ToString();
                _starts[field] = 0;
                _lengths[field] = 0;
            }
            else
            {
                var end = line.IndexOf(',', position);
                if (end < 0)
                {
                    end = line.Length;
                }

                _starts[field] = position;
                _lengths[field] = end - position;
                position = end;
            }

            field++;
            if (position >= line.Length)
            {
                break;
            }

            if (line[position] != ',')
            {
                throw new InvalidDataException($"{sourceName}: row {rowNumber} has text after a closing quote.");
            }

            position++;
            if (position == line.Length)
            {
                // Trailing comma: one more empty field.
                if (field >= _starts.Length)
                {
                    throw new InvalidDataException($"{sourceName}: row {rowNumber} has more than {_starts.Length} fields.");
                }

                _unquoted[field] = null;
                _starts[field] = position;
                _lengths[field] = 0;
                field++;
                break;
            }
        }

        if (field != _starts.Length)
        {
            throw new InvalidDataException($"{sourceName}: row {rowNumber} has {field} fields, expected {_starts.Length}.");
        }
    }

    public ReadOnlySpan<char> Span(int index) =>
        _unquoted[index] is { } text ? text.AsSpan() : _line.AsSpan(_starts[index], _lengths[index]);

    public bool IsEmpty(int index) => Span(index).IsEmpty;

    public string String(int index) => Span(index).ToString();

    public string? NullableString(int index) => IsEmpty(index) ? null : String(index);

    public int Int(int index) => int.Parse(Span(index), NumberStyles.Integer, CultureInfo.InvariantCulture);

    public int? NullableInt(int index) => IsEmpty(index) ? null : Int(index);

    public long Long(int index) => long.Parse(Span(index), NumberStyles.Integer, CultureInfo.InvariantCulture);

    public Guid Guid(int index) => System.Guid.Parse(Span(index));

    public Guid? NullableGuid(int index) => IsEmpty(index) ? null : Guid(index);

    /// <summary>PostgreSQL writes booleans cast to int as 0/1 and unconverted ones as t/f.</summary>
    public bool? NullableBool(int index)
    {
        var span = Span(index);
        if (span.IsEmpty)
        {
            return null;
        }

        return span[0] switch
        {
            '1' or 't' or 'T' => true,
            '0' or 'f' or 'F' => false,
            _ => throw new FormatException($"'{span}' is not a boolean.")
        };
    }

    public DateTimeOffset Timestamp(int index) => PostgresTimestamp.Parse(Span(index));

    public DateTimeOffset? NullableTimestamp(int index) => IsEmpty(index) ? null : Timestamp(index);
}

public static class PostgresTimestamp
{
    /// <summary>
    /// Parses PostgreSQL <c>timestamptz</c> text output, e.g. <c>2026-11-09 13:15:46.044697+00</c>.
    /// </summary>
    public static DateTimeOffset Parse(ReadOnlySpan<char> text)
    {
        var value = text.ToString();
        // PostgreSQL writes the offset as +00 (hours only); .NET wants +00:00.
        if (value.Length >= 3 && (value[^3] == '+' || value[^3] == '-') && char.IsDigit(value[^1]))
        {
            value += ":00";
        }

        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
            .ToUniversalTime();
    }

    public static long ToUnixMs(DateTimeOffset value) => value.ToUnixTimeMilliseconds();

    public static DateTimeOffset FromUnixMs(long value) => DateTimeOffset.FromUnixTimeMilliseconds(value);
}
