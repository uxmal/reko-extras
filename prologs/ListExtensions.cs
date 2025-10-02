using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace prologs;

public static class ListExtensions
{
    public static T pop<T>(this List<T> list)
    {
        if (list.Count == 0)
        {
            throw new InvalidOperationException("Cannot pop from an empty list.");
        }
        var ilast = list.Count - 1;
        T item = list[ilast];
        list.RemoveAt(ilast);
        return item;
    }

    public static T pop<T>(this HashSet<T> set)
    {
        if (set.Count == 0)
        {
            throw new InvalidOperationException("Cannot pop from an empty set.");
        }
        var item = set.First();
        set.Remove(item);
        return item;
    }
}
