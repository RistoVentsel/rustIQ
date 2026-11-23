using System.Globalization;

namespace RustIQ.Simulator;

public sealed class CommandLine
{
    private readonly Dictionary<string, List<string>> _options = new Dictionary<string, List<string>>(StringComparer.Ordinal);

    private CommandLine(string command)
    {
        Command = command;
    }

    public string Command { get; }

    public IReadOnlyDictionary<string, List<string>> Options
    {
        get { return _options; }
    }

    public static CommandLine Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || args[0].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("The first argument must be a command.");
        }

        CommandLine commandLine = new CommandLine(args[0]);
        List<string>? current = null;
        for (int i = 1; i < args.Count; i++)
        {
            string arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                string name = arg.Substring(2);
                if (commandLine._options.ContainsKey(name))
                {
                    throw new ArgumentException($"--{name} is given twice.");
                }

                current = new List<string>();
                commandLine._options[name] = current;
            }
            else if (current == null)
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

    public bool Has(string name)
    {
        return _options.ContainsKey(name);
    }

    public IReadOnlyList<string> Values(string name)
    {
        List<string>? values;
        if (_options.TryGetValue(name, out values) && values.Count > 0)
        {
            return values;
        }

        throw new ArgumentException($"--{name} needs a value.");
    }

    public string? Optional(string name)
    {
        List<string>? values;
        if (!_options.TryGetValue(name, out values))
        {
            return null;
        }

        if (values.Count != 1)
        {
            throw new ArgumentException($"--{name} takes one value.");
        }

        return values[0];
    }

    public string Required(string name)
    {
        string? value = Optional(name);
        if (value == null)
        {
            throw new ArgumentException($"--{name} is required.");
        }

        return value;
    }

    public DateTimeOffset Time(string name, DateTimeOffset fallback)
    {
        string? text = Optional(name);
        if (text == null)
        {
            return fallback;
        }

        DateTimeOffset parsed = DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        return parsed.ToUniversalTime();
    }

    public double Double(string name, double fallback)
    {
        string? text = Optional(name);
        if (text == null)
        {
            return fallback;
        }

        return double.Parse(text, CultureInfo.InvariantCulture);
    }

    public int Int(string name, int fallback)
    {
        string? text = Optional(name);
        if (text == null)
        {
            return fallback;
        }

        return int.Parse(text, CultureInfo.InvariantCulture);
    }
}
