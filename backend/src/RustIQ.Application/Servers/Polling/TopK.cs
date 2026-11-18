namespace RustIQ.Application.Servers.Polling;

/// <summary>
/// Picks the first <c>k</c> items in an order without sorting everything: a bounded max-heap that keeps
/// the k best seen so far. Ties must be broken inside the comparer for a deterministic result.
/// </summary>
public sealed class TopK<T>
{
    private readonly IComparer<T> _comparer;
    private readonly List<T> _heap = [];
    private int _k;

    /// <param name="comparer">Sorts the wanted items first (negative = earlier).</param>
    public TopK(IComparer<T> comparer)
    {
        _comparer = comparer;
    }

    public void Reset(int k)
    {
        _k = Math.Max(0, k);
        _heap.Clear();
    }

    public void Offer(T item)
    {
        if (_k == 0)
        {
            return;
        }

        if (_heap.Count < _k)
        {
            _heap.Add(item);
            SiftUp(_heap.Count - 1);
            return;
        }

        // The root is the worst kept item; replace it only with something earlier in the order.
        if (_comparer.Compare(item, _heap[0]) < 0)
        {
            _heap[0] = item;
            SiftDown(0);
        }
    }

    /// <summary>The kept items in order.</summary>
    public List<T> ToSortedList()
    {
        var result = new List<T>(_heap);
        result.Sort(_comparer);
        return result;
    }

    private void SiftUp(int index)
    {
        while (index > 0)
        {
            var parent = (index - 1) / 2;
            if (_comparer.Compare(_heap[index], _heap[parent]) <= 0)
            {
                return;
            }

            (_heap[index], _heap[parent]) = (_heap[parent], _heap[index]);
            index = parent;
        }
    }

    private void SiftDown(int index)
    {
        while (true)
        {
            var left = index * 2 + 1;
            var right = left + 1;
            var largest = index;
            if (left < _heap.Count && _comparer.Compare(_heap[left], _heap[largest]) > 0) largest = left;
            if (right < _heap.Count && _comparer.Compare(_heap[right], _heap[largest]) > 0) largest = right;
            if (largest == index)
            {
                return;
            }

            (_heap[index], _heap[largest]) = (_heap[largest], _heap[index]);
            index = largest;
        }
    }
}
