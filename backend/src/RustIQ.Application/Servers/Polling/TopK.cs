namespace RustIQ.Application.Servers.Polling;

public sealed class TopK<T>
{
    private readonly IComparer<T> _comparer;
    private readonly List<T> _heap = new List<T>();
    private int _k;

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

        if (_comparer.Compare(item, _heap[0]) < 0)
        {
            _heap[0] = item;
            SiftDown(0);
        }
    }

    public List<T> ToSortedList()
    {
        List<T> result = new List<T>(_heap);
        result.Sort(_comparer);
        return result;
    }

    private void SiftUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (_comparer.Compare(_heap[index], _heap[parent]) <= 0)
            {
                return;
            }

            Swap(index, parent);
            index = parent;
        }
    }

    private void SiftDown(int index)
    {
        while (true)
        {
            int left = index * 2 + 1;
            int right = left + 1;
            int largest = index;
            if (left < _heap.Count && _comparer.Compare(_heap[left], _heap[largest]) > 0)
            {
                largest = left;
            }

            if (right < _heap.Count && _comparer.Compare(_heap[right], _heap[largest]) > 0)
            {
                largest = right;
            }

            if (largest == index)
            {
                return;
            }

            Swap(index, largest);
            index = largest;
        }
    }

    private void Swap(int a, int b)
    {
        T temp = _heap[a];
        _heap[a] = _heap[b];
        _heap[b] = temp;
    }
}
