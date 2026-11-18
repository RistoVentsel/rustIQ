namespace RustIQ.Application.Servers.Polling;

/// <summary>
/// Spends a fixed number of queries per minute on the servers that have waited longest
/// (<c>LastCheckedAtUtc ?? CreatedAtUtc</c>), with no special treatment of any server.
/// </summary>
public sealed class UniformPollingStrategy : IPollingStrategy
{
    public const string Lane = "Uniform";

    private readonly PollBudget _budget;
    private readonly TopK<PolledServer> _topK = new(OldestFirst.Instance);

    public UniformPollingStrategy(double queriesPerMinute, double tickSeconds = 15)
    {
        _budget = new PollBudget(queriesPerMinute, tickSeconds);
    }

    public string Name => "uniform";

    public void Tick(long nowMs, IReadOnlyList<PolledServer> servers, IPollExecutor poll)
    {
        _topK.Reset(_budget.Next(nowMs));
        foreach (var server in servers)
        {
            if (server.Exists)
            {
                _topK.Offer(server);
            }
        }

        var selected = _topK.ToSortedList();
        if (selected.Count > 0)
        {
            poll.Poll(selected, Lane);
        }
    }

    /// <summary>Oldest first; ties by id (ordinal on the text form, as PostgreSQL orders uuids).</summary>
    private sealed class OldestFirst : IComparer<PolledServer>
    {
        public static readonly OldestFirst Instance = new();

        public int Compare(PolledServer? x, PolledServer? y)
        {
            var byDue = x!.DueKeyMs.CompareTo(y!.DueKeyMs);
            return byDue != 0 ? byDue : GuidOrder.Compare(x.Id, y.Id);
        }
    }
}

/// <summary>
/// Turns a per-minute budget into whole polls per tick, carrying the fraction. The allowance follows the
/// time since the previous tick, so a tick that runs late (the worker was busy) does not lose budget.
/// </summary>
public sealed class PollBudget
{
    private readonly double _perMs;
    private readonly long _firstTickMs;
    private long? _lastTickMs;
    private double _carry;

    public PollBudget(double queriesPerMinute, double tickSeconds)
    {
        _perMs = queriesPerMinute / 60_000.0;
        _firstTickMs = (long)(tickSeconds * 1000);
    }

    public int Next(long nowMs)
    {
        var elapsed = _lastTickMs is { } last ? Math.Max(0, nowMs - last) : _firstTickMs;
        _lastTickMs = nowMs;
        _carry += elapsed * _perMs;
        var whole = (int)Math.Floor(_carry + 1e-9);
        _carry -= whole;
        return whole;
    }
}

public static class GuidOrder
{
    /// <summary>PostgreSQL's uuid order: byte order of the canonical text form.</summary>
    public static int Compare(Guid x, Guid y) => string.CompareOrdinal(x.ToString(), y.ToString());
}
