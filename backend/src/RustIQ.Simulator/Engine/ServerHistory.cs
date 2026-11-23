namespace RustIQ.Simulator.Engine;

public sealed class ServerHistory
{
    private long[] _times = new long[0];
    private short[] _players = new short[0];
    private short[] _maxPlayers = new short[0];
    private bool[] _answered = new bool[0];

    public int Count { get; private set; }

    public void Add(long atMs, int players, int maxPlayers, bool answered)
    {
        if (Count == _times.Length)
        {
            int capacity = Math.Max(16, Count * 2);
            Array.Resize(ref _times, capacity);
            Array.Resize(ref _players, capacity);
            Array.Resize(ref _maxPlayers, capacity);
            Array.Resize(ref _answered, capacity);
        }

        int position = Count;
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

    public (int Count, double Average, int Peak) Window(long fromMs, long toMs)
    {
        int start = FirstAtOrAfter(fromMs);
        long sum = 0;
        int peak = 0;
        int count = 0;
        for (int i = start; i < Count && _times[i] <= toMs; i++)
        {
            sum += _players[i];
            if (_players[i] > peak)
            {
                peak = _players[i];
            }

            count++;
        }

        double average = 0;
        if (count != 0)
        {
            average = (double)sum / count;
        }

        return (count, average, peak);
    }

    public long? LastAnsweredAtOrBefore(long atMs)
    {
        for (int i = LastAtOrBefore(atMs); i >= 0; i--)
        {
            if (_answered[i])
            {
                return _times[i];
            }
        }

        return null;
    }

    public int? MaxPlayersAtOrBefore(long atMs)
    {
        int i = LastAtOrBefore(atMs);
        if (i >= 0)
        {
            return _maxPlayers[i];
        }

        return null;
    }

    private int FirstAtOrAfter(long atMs)
    {
        int low = 0;
        int high = Count;
        while (low < high)
        {
            int mid = low + (high - low) / 2;
            if (_times[mid] < atMs)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    private int LastAtOrBefore(long atMs)
    {
        return FirstAtOrAfter(atMs + 1) - 1;
    }
}
