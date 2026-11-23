namespace RustIQ.Application.Servers.Polling;

public sealed class UniformPollingStrategy : IPollingStrategy
{
    public const string Lane = "Uniform";

    private readonly PollBudget _budget;
    private readonly TopK<PolledServer> _topK = new TopK<PolledServer>(OldestFirst.Instance);

    public UniformPollingStrategy(double queriesPerMinute, double tickSeconds = 15)
    {
        _budget = new PollBudget(queriesPerMinute, tickSeconds);
    }

    public string Name
    {
        get { return "uniform"; }
    }

    public void Tick(long nowMs, IReadOnlyList<PolledServer> servers, IPollExecutor poll)
    {
        _topK.Reset(_budget.Next(nowMs));
        foreach (PolledServer server in servers)
        {
            if (server.Exists)
            {
                _topK.Offer(server);
            }
        }

        List<PolledServer> selected = _topK.ToSortedList();
        if (selected.Count > 0)
        {
            poll.Poll(selected, Lane);
        }
    }

    private sealed class OldestFirst : IComparer<PolledServer>
    {
        public static readonly OldestFirst Instance = new OldestFirst();

        public int Compare(PolledServer? x, PolledServer? y)
        {
            int byDue = x!.DueKeyMs.CompareTo(y!.DueKeyMs);
            if (byDue != 0)
            {
                return byDue;
            }

            return GuidOrder.Compare(x.Id, y.Id);
        }
    }
}

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
        long elapsed;
        if (_lastTickMs.HasValue)
        {
            elapsed = Math.Max(0, nowMs - _lastTickMs.Value);
        }
        else
        {
            elapsed = _firstTickMs;
        }

        _lastTickMs = nowMs;
        _carry += elapsed * _perMs;
        int whole = (int)Math.Floor(_carry + 1e-9);
        _carry -= whole;
        return whole;
    }
}

public static class GuidOrder
{
    public static int Compare(Guid x, Guid y)
    {
        return string.CompareOrdinal(x.ToString(), y.ToString());
    }
}
