using Reko.Core;
using Reko.Core.Collections;
using Reko.Core.Diagnostics;
using Reko.Core.Expressions;
using Reko.Core.Graphs;
using Reko.Core.Loading;
using Reko.Core.Machine;
using Reko.Core.Output;
using Reko.Core.Rtl;
using Reko.Extras.Interactive.Views;
using Reko.ImageLoaders.Nro;
using Reko.Scanning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Reko.Extras.Interactive.ViewModels;

public class HybridCodeCollection :
    ObservableObject,
    IList,
    INotifyCollectionChanged,
    INotifyPropertyChanged
{
    private static TraceSwitch trace = new(nameof(HybridCodeCollection), "")
    {
        Level = TraceLevel.Verbose
    };

    private ScanResults? sr;
    private ImageSegment? segment;
    private SortedList<int, RtlBlock> map;

    public HybridCodeCollection(ImageSegment? segment = null)
    {
        this.segment = segment;
        this.map = [];
    }

    public object? this[int index] {
        get => ItemAtIndex(index);
        set => throw new NotImplementedException();
    }

    public ScanResults? ScanResults
    {
        get => sr;
        set
        {
            if (this.RaiseAndSetIfChanged(ref sr, value))
            {
                OnSetScanResults(value);
                CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
            }
        }
    }

    private void OnSetScanResults(ScanResults? value)
    {
        if (value is null || segment is null)
        {
            map = [];
            return;
        }
        map = value.Blocks.Values
            .Where(b => this.segment.IsInRange(b.Address))
            .ToSortedList(b => (int)(b.Address - segment.Address));
    }

    public bool IsFixedSize => false;

    public bool IsReadOnly => false;

    public int Count => EstimateCount();

    public bool IsSynchronized => throw new NotImplementedException();

    public object SyncRoot => throw new NotImplementedException();

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int Add(object? value)
    {
        throw new NotImplementedException();
    }

    public void Clear()
    {
        throw new NotImplementedException();
    }

    public bool Contains(object? value)
    {
        throw new NotImplementedException();
    }

    public void CopyTo(Array array, int index)
    {
        throw new NotImplementedException();
    }

    public IEnumerator GetEnumerator()
    {
        if (segment is null)
            yield break;
        int offset = 0;
        var output = new FormatterOutput();
        var rdr = segment.MemoryArea.CreateLeReader(segment.Address + offset);
        bool moreData = true;
        while (moreData)
        {
            if (map.TryGetLowerBound(offset, out var block))
            {
                throw new NotImplementedException();
            }
            else
            {
                var a = rdr.Address;
                moreData = segment.MemoryArea.Formatter.RenderLine(rdr, Encoding.UTF8, output);
                trace.Verbose($"Enum:{a} {moreData}");
                yield return output.Item;
            }
            offset += 4;
        }
    }

    public int IndexOf(object? value)
    {
        throw new NotImplementedException();
    }

    public void Insert(int index, object? value)
    {
        throw new NotImplementedException();
    }

    public void Remove(object? value)
    {
        throw new NotImplementedException();
    }

    public void RemoveAt(int index)
    {
        throw new NotImplementedException();
    }

    private int EstimateCount()
    {
        if (sr is null)
            return (int)((segment?.Size + 16 -1) / 16 ?? 0);
        return map.Count;
    }

    private HybridItem ItemAtIndex(int index)
    {
        if (segment is null)
            throw new IndexOutOfRangeException();
        if (map.Count == 0)
        {
            var addr = segment.Address + index * 16;
            var rdr = segment.MemoryArea.CreateLeReader(addr);
            var mem = new FormatterOutput();
            segment.MemoryArea.Formatter.RenderLine(rdr, Encoding.UTF8, mem);
            return mem.Item;
        }
        throw new NotImplementedException();
    }

    private class FormatterOutput : IMemoryFormatterOutput
    {
        private List<HybridElement> elems;
        private HybridItem item;

        public FormatterOutput()
        {
            elems = [];
            item = default!;
        }

        public HybridItem Item => item!;

        public void BeginLine()
        {
            elems.Clear();
        }

        /// <inheritdoc/>
        public void EndLine(Constant[] units)
        {
            item = new HybridItem
            {
                Elements = elems.ToArray()
            };
        }

        /// <inheritdoc/>
        public void RenderAddress(Address addr)
        {
            elems.Add(new HybridElement { Text = addr.ToString() });
        }

        /// <inheritdoc/>
        public void RenderFillerSpan(int nChunks, int nCharsPerChunk)
        {
            elems.Add(new HybridElement { Text = new string(' ', nChunks * nCharsPerChunk) });
        }

        public void RenderTextFillerSpan(int padding)
        {
            elems.Add(new HybridElement { Text = new string(' ', padding) });
        }

        /// <inheritdoc/>
        public void RenderUnit(Address addr, string sUnit)
        {
            elems.Add(new HybridElement { Text = sUnit });
        }

        /// <inheritdoc/>
        public void RenderUnitAsText(Address addr, string sUnit)
        {
            elems.Add(new HybridElement { Text = sUnit });
        }
    }
}
