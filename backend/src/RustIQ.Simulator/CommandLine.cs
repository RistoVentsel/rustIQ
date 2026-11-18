using System.Globalization;

namespace RustIQ.Simulator;

/// <summary>
/// <c>&lt;command&gt; --name value [value ...] --flag</c>. An option may take several values (e.g. <c>--data a b</c>).
/// </summary>
public sealed class CommandLine
{
    private readonly Dictionary<string, List<string>> _options = new(StringComparer.Ordinal);

    private CommandLine(string command)
    {
        Command = command;
    }

    public string Command { get; }

    public IReadOnlyDictionary<string, List<string>> Options => _options;

    public static CommandLine Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("The first argument must be a command.");
        }

        var commandLine = new CommandLine(args[0]);
        List<string>? current = null;
        foreach (var arg in args.Skip(1))
        {
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                var name = arg[2..];
                if (commandLine._options.ContainsKey(name))
                {
                    throw new ArgumentException($"--{name} is given twice.");
                }

                current = [];
                commandLine._options[name] = current;
            }
            else if (current is null)
            {
                throw new ArgumentException($"'{arg}' does not belong to an option.");
            }
            else
            {
                current.Add(arg);
            }
        }

        return commandLine;
    }

    public bool Has(string name) => _options.ContainsKey(name);

    public IReadOnlyList<string> Values(string name) =>
        _options.TryGetValue(name, out var values) && values.Count > 0
            ? values
            : throw new ArgumentException($"--{name} needs a value.");

    public string? Optional(string name)
    {
        if (!_options.TryGetValue(name, out var values))
        {
            return null;
        }

        return values.Count == 1 ? values[0] : throw new ArgumentException($"--{name} takes one value.");
    }

    public string Required(string name) => Optional(name) ?? throw new ArgumentException($"--{name} is required.");

    public DateTimeOffset Time(string name, DateTimeOffset fallback) =>
        Optional(name) is { } text
            ? DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime()
            : fallback;

    public double Double(string name, double fallback) =>
        Optional(name) is { } text ? double.Parse(text, CultureInfo.InvariantCulture) : fallback;

    public int Int(string name, int fallback) =>
        Optional(name) is { } text ? int.Parse(text, CultureInfo.InvariantCulture) : fallback;
}
