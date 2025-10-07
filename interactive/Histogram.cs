using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.Interactive;

internal class Histogram
{
    private Dictionary<int, int> buckets;
    public Histogram()
    {
        this.buckets = [];
    }

    public void Add(int bucket, int count)
    {
        if (buckets.ContainsKey(bucket))
            buckets[bucket] += count;
        else
            buckets[bucket] = count;
    }

    public void Dump(int width=60)
    {
        int maxCount = buckets.Values.Max();
        foreach (var kvp in buckets.OrderBy(kvp => kvp.Key))
        {
            int barLength = (int)((kvp.Value / (double)maxCount) * width);
            string bar = new string('#', barLength);
            Console.WriteLine($"{kvp.Key,5}: {bar} ({kvp.Value})");
            Debug.WriteLine($"{kvp.Key,5}: {bar} ({kvp.Value})");
        }
    }
}
