using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ParallelScan
{
    /// <summary>
    /// Implementation of the priority queue ADT.
    /// </summary>
    /// <typeparam name="TItem"></typeparam>
    public class PriorityQueue<TItem, TPriority> : IEnumerable<TItem>
    {
        HeapItem[] heap;
        private Comparer<TPriority> cmp;
        int count;

        public struct HeapItem
        {
            public TItem Value;
            public TPriority Priority;
        }

        public PriorityQueue()
        {
            heap = new HeapItem[4];
            cmp = Comparer<TPriority>.Default;
        }

        private void GrowHeap()
        {
            HeapItem [] newHeap = new HeapItem[heap.Length * 2 + 1];
            heap.CopyTo(newHeap, 0);
            heap = newHeap;
        }

        private void BubbleUp(int idx, HeapItem item)
        {
            int iParent = (idx - 1) / 2;
            while (idx > 0 && cmp.Compare(heap[iParent].Priority, item.Priority) < 0)
            {
                heap[idx] = heap[iParent];
                idx = iParent;
                iParent = (idx - 1) / 2;
            }
            heap[idx] = item;
        }

        private void TrickleDown(int idx, HeapItem item)
        {
            int iChild = idx * 2 + 1;
            while (iChild < count)
            {
                if (iChild+1 < count && 
                    cmp.Compare(heap[iChild].Priority, heap[iChild+1].Priority) < 0)
                {
                    ++iChild;
                }
                heap[idx] = heap[iChild];
                idx = iChild;
                iChild  =idx * 2 + 1;
            }
            BubbleUp(idx, item);
        }

        #region ICollection<T> Members

        public void Enqueue(TItem value, TPriority priority)
        {
            ++count;
            if (count >= heap.Length)
                GrowHeap();
            BubbleUp(count - 1, new HeapItem { Priority = priority, Value = value });
        }

        public bool TryDequeue([MaybeNullWhen(false)] out TItem item)
        {
            if (count <= 0)
            {
                item = default;
                return false;
            }
            item = heap[0].Value;
            --count;
            TrickleDown(0, heap[count]);
            return true;
        }

        public void Clear()
        {
            count = 0;
            heap = new HeapItem[4];
        }

        public bool Contains(TItem item)
        {
            for (int i = 0; i < count; ++i)
            {
                if (heap[i].Value!.Equals(item))
                    return true;
            }
            return false;
        }

        public void CopyTo(TItem[] array, int arrayIndex)
        {
            for (int i = 0; i < count; ++i, ++arrayIndex)
            {
                array[arrayIndex] = heap[i].Value;
            }
        }

        public int Count
        {
            get { return count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }


        #endregion

        #region IEnumerable<T> Members

        public IEnumerator<TItem> GetEnumerator()
        {
            for (int i = 0; i < count; ++i)
            {
                yield return heap[i].Value;
            }
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

    }
}
