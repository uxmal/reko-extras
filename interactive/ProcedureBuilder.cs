using Avalonia.Vulkan;
using Reko.Core;
using Reko.Core.Services;
using Reko.Scanning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.Interactive;

public class ProcedureBuilder
{
    private ScanResults scanResults;
    private Core.Program program;
    private IEventListener listener;

    public ProcedureBuilder(ScanResults scanResults, Core.Program program, IEventListener listener)
    {
        this.scanResults = scanResults;
        this.program = program;
        this.listener = listener;
    }

    public void BuildProcedures()
    {
        listener.Progress.ShowProgress("Building procedures...", 0, scanResults.CFG.Nodes.Count);
        var sw = Stopwatch.StartNew();
        int cBlocks = scanResults.CFG.Nodes.Count;
        var blocksRemaining = new HashSet<Core.Address>(scanResults.CFG.Nodes);
        List<BlockCluster> clusters = [];
        foreach (var addrCalled in scanResults.CalledAddresses)
        {
            var cluster = FindWcc(addrCalled, blocksRemaining);
            clusters.Add(cluster);
            listener.Progress.ShowProgress(cBlocks - blocksRemaining.Count, cBlocks);
        }
        while (blocksRemaining.Count > 0)
        {
            var addr = blocksRemaining.First();
            var cluster = FindWcc(addr, blocksRemaining);
            clusters.Add(cluster);
            listener.Progress.ShowProgress(cBlocks - blocksRemaining.Count, cBlocks);
        }
        listener.Info($"Found {clusters.Count} block clusters in {sw.ElapsedMilliseconds}ms");
        ClassifyClusters(clusters);
        listener.Progress.Finish();
    }

    private void ClassifyClusters(List<BlockCluster> clusters)
    {
        var blocks = new Histogram();
        var entries = new Histogram();
        foreach (BlockCluster cluster in clusters)
        {
            blocks.Add(cluster.Blocks.Count, 1);
            entries.Add(cluster.Entries.Count, 1);
        }
        Debug.WriteLine("Blocks per cluster");
        blocks.Dump(30);
        Debug.WriteLine("Entries per cluster");
        entries.Dump(30);
        foreach (var c in clusters.Where(c => c.Entries.Count > 1))
        {
            foreach (Address b in c.Blocks.OrderBy(a => a))
            {
                var block = this.scanResults.Blocks[b];
                if (c.Entries.Contains(b))
                    Debug.Write("=> ");
                Debug.Write(b.ToString());
                Debug.WriteLine("");
                foreach (var instr in block.Instructions.SelectMany(m => m.Instructions))
                {
                    Debug.WriteLine($"    {instr}");
                }
                Debug.WriteLine("");
            }
        }
    }

    private BlockCluster FindWcc(Address addr, HashSet<Address> blocksRemaining)
    {
        HashSet<Address> clusterBlocks = [];
        var wl = new Queue<Address>();
        wl.Enqueue(addr);
        List<Address> possibleEntries = [];
        while (wl.TryDequeue(out var addrBlock))
        {
            if (!blocksRemaining.Remove(addrBlock))
                continue;
            clusterBlocks.Add(addrBlock);
            foreach (var succ in scanResults.CFG.Successors(addrBlock))
            {
                if (blocksRemaining.Contains(succ))
                    wl.Enqueue(succ);
            }
            int cPred = 0;
            foreach (var pred in scanResults.CFG.Predecessors(addrBlock))
            {
                ++cPred;
                if (blocksRemaining.Contains(pred))
                    wl.Enqueue(pred);
            }
            if (cPred == 0)
            {
                possibleEntries.Add(addrBlock);
            }
        }
        return new BlockCluster(possibleEntries, clusterBlocks);
    }
}
