using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace RustIQ.Simulator.Data;

public static class CsvFile
{
    public static long Read(string path, string expectedHeader, Action<CsvRow> onRow)
    {
        using FileStream file = File.OpenRead(path);
        Stream stream;
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            stream = new GZipStream(file, CompressionMode.Decompress);
        }
        else
        {
            stream = file;
        }

        using StreamReader reader = new StreamReader(stream, bufferSize: 1 << 16);
        return Read(reader, expectedHeader, onRow, path);
    }

    public static long Read(TextReader reader, string expectedHeader, Action<CsvRow> onRow, string sourceName = "csv")
    {
        string? header = reader.ReadLine();
        if (header != null)
        {
            header = header.TrimStart('﻿');
        }

        if (!string.Equals(header, expectedHeader, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{sourceName}: header is '{header}', expected '{expectedHeader}'.");
        }

        int fieldCount = expectedHeader.Split(',').Length;
        CsvRow row = new CsvRow(fieldCount);
        long rows = 0;
        string? line = reader.ReadLine();
        while (line != null)
        {
            if (line.Length > 0)
            {
                while (HasOpenQuote(line))
                {
                    string? next = reader.ReadLine();
                    if (next == null)
                    {
                        throw new InvalidDataException($"{sourceName}: unterminated quote on row {rows + 1}.");
                    }

                    line = line + "\n" + next;
                }

                row.Parse(line, rows + 1, sourceName);
                onRow(row);
                rows++;
            }

            line = reader.ReadLine();
        }

        return rows;
    }

    private static bool HasOpenQuote(string line)
    {
        if (line.IndexOf('"') < 0)
        {
            return false;
        }

        bool open = false;
        foreach (char c in line)
        {
            if (c == '"')
            {
                open = !open;
            }
        }

        return open;
    }
}

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

    public int FieldCount
    {
        get { return _starts.Length; }
    }

    internal void Parse(string line, long rowNumber, string sourceName)
    {
        _line = line;
        int field = 0;
        int position = 0;
        while (true)
        {
            if (field >= _starts.Length)
            {
                throw new InvalidDataException($"{sourceName}: row {rowNumber} has more than {_starts.Length} fields.");
            }

            _unquoted[field] = null;
            if (position < line.Length && line[position] == '"')
            {
                StringBuilder builder = new StringBuilder();
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
                int end = line.IndexOf(',', position);
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

    public ReadOnlySpan<char> Span(int index)
    {
        string? text = _unquoted[index];
        if (text != null)
        {
            return text.AsSpan();
        }

        return _line.AsSpan(_starts[index], _lengths[index]);
    }

    public bool IsEmpty(int index)
    {
        return Span(index).IsEmpty;
    }

    public string String(int index)
    {
        return Span(index).ToString();
    }

    public string? NullableString(int index)
    {
        if (IsEmpty(index))
        {
            return null;
        }

        return String(index);
    }

    public int Int(int index)
    {
        return int.Parse(Span(index), NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    public int? NullableInt(int index)
    {
        if (IsEmpty(index))
        {
            return null;
        }

        return Int(index);
    }

    public long Long(int index)
    {
        return long.Parse(Span(index), NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    public Guid Guid(int index)
    {
        return System.Guid.Parse(Span(index));
    }

    public Guid? NullableGuid(int index)
    {
        if (IsEmpty(index))
        {
            return null;
        }

        return Guid(index);
    }

    public bool? NullableBool(int index)
    {
        ReadOnlySpan<char> span = Span(index);
        if (span.IsEmpty)
        {
            return null;
        }

        char first = span[0];
        if (first == '1' || first == 't' || first == 'T')
        {
            return true;
        }

        if (first == '0' || first == 'f' || first == 'F')
        {
            return false;
        }

        throw new FormatException($"'{span}' is not a boolean.");
    }

    public DateTimeOffset Timestamp(int index)
    {
        return PostgresTimestamp.Parse(Span(index));
    }

    public DateTimeOffset? NullableTimestamp(int index)
    {
        if (IsEmpty(index))
        {
            return null;
        }

        return Timestamp(index);
    }
}

public static class PostgresTimestamp
{
    public static DateTimeOffset Parse(ReadOnlySpan<char> text)
    {
        string value = text.ToString();
        if (value.Length >= 3)
        {
            char sign = value[value.Length - 3];
            char last = value[value.Length - 1];
            if ((sign == '+' || sign == '-') && char.IsDigit(last))
            {
                value += ":00";
            }
        }

        DateTimeOffset parsed = DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        return parsed.ToUniversalTime();
    }

    public static long ToUnixMs(DateTimeOffset value)
    {
        return value.ToUnixTimeMilliseconds();
    }

    public static DateTimeOffset FromUnixMs(long value)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(value);
    }
}
