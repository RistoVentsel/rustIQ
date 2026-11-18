namespace RustIQ.Simulator.Engine;

/// <summary>
/// One server's snapshots as the ranking sees them: production's snapshots before the period, then the
/// simulated and replayed ones, kept in time order.
/// </summary>
public sealed class ServerHistory
{
    private long[] _times = [];
    private short[] _players = [];
    private short[] _maxPlayers = [];
    private bool[] _answered = [];

    public int Count { get; private set; }

    public void Add(long atMs, int players, int maxPlayers, bool answered)
    {
        if (Count == _times.Length)
        {
            var capacity = Math.Max(16, Count * 2);
            Array.Resize(ref _times, capacity);
            Array.Resize(ref _players, capacity);
            Array.Resize(ref _maxPlayers, capacity);
            Array.Resize(ref _answered, capacity);
        }

        // Nearly always appended; a replayed poll can land between the polls of a long tick.
        var position = Count;
        while (position > 0 && _times[position - 1] > atMs)
        {
            _times[position] = _times[position - 1];
            _players[position] = _players[position - 1];
            _maxPlayers[position] = _maxPlayers[position - 1];
            _answered[position] = _answered[position - 1];
            position--;
        }

        _times[position] = atMs;
        _players[position] = (short)Math.Clamp(players, short.MinValue, short.MaxValue);
        _maxPlayers[position] = (short)Math.Clamp(maxPlayers, short.MinValue, short.MaxValue);
        _answered[position] = answered;
        Count++;
    }

    /// <summary>Snapshot count, average and peak players over [fromMs, toMs].</summary>
    public (int Count, double Average, int Peak) Window(long fromMs, long toMs)
    {
        var start = FirstAtOrAfter(fromMs);
        long sum = 0;
        var peak = 0;
        var count = 0;
        for (var i = start; i < Count && _times[i] <= toMs; i++)
        {
            sum += _players[i];
            peak = Math.Max(peak, _players[i]);
            count++;
        }

        return (count, count == 0 ? 0 : (double)sum / count, peak);
    }

    /// <summary>Time of the last answered snapshot at or before <paramref name="atMs"/>.</summary>
    public long? LastAnsweredAtOrBefore(long atMs)
    {
        for (var i = LastAtOrBefore(atMs); i >= 0; i--)
        {
            if (_answered[i])
            {
                return _times[i];
            }
        }

        return null;
    }

    /// <summary>MaxPlayers recorded by the last snapshot at or before <paramref name="atMs"/>.</summary>
    public int? MaxPlayersAtOrBefore(long atMs)
    {
        var i = LastAtOrBefore(atMs);
        return i >= 0 ? _maxPlayers[i] : null;
    }

    private int FirstAtOrAfter(long atMs)
    {
        int low = 0, high = Count;
        while (low < high)
        {
            var mid = (low + high) >>> 1;
            if (_times[mid] < atMs) low = mid + 1;
            else high = mid;
        }

        return low;
    }

    private int LastAtOrBefore(long atMs) => FirstAtOrAfter(atMs + 1) - 1;
}
