using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace angr.analyses;

public class defaultdict<K, V> : IDictionary<K, V>
    where K : notnull
{
    private readonly Dictionary<K, V> dict;
    private readonly Func<V> generator;

    public defaultdict(Func<V> generator)
    {
        this.dict = new();
        this.generator = generator;
    }

    public V this[K key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public ICollection<K> Keys => dict.Keys;

    public ICollection<V> Values => dict.Values;

    public int Count => throw new NotImplementedException();

    public bool IsReadOnly => throw new NotImplementedException();

    public void Add(K key, V value)
    {
        throw new NotImplementedException();
    }

    public void Add(KeyValuePair<K, V> item)
    {
        throw new NotImplementedException();
    }

    public void Clear()
    {
        throw new NotImplementedException();
    }

    public bool Contains(KeyValuePair<K, V> item)
    {
        throw new NotImplementedException();
    }

    public bool ContainsKey(K key)
    {
        throw new NotImplementedException();
    }

    public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
    {
        throw new NotImplementedException();
    }

    public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    public bool Remove(K key)
    {
        throw new NotImplementedException();
    }

    public bool Remove(KeyValuePair<K, V> item)
    {
        throw new NotImplementedException();
    }

    public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
    {
        throw new NotImplementedException();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}
