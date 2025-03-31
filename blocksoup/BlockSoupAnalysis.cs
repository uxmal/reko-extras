namespace Reko.Extras.blocksoup;

using Reko.Core;
using Reko.Core.Graphs;
using Reko.Core.Loading;
using Reko.Environments.PalmOS;
using Reko.Scanning;
using System.Diagnostics;
using System.Runtime.CompilerServices;

public class BlockSoupAnalysis
{
    private readonly Program program;

    public BlockSoupAnalysis(Program program)
    {
        this.program = program;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Extract()
    {
        IRewriterHost host = new RewriterHost();
        ExtractOnlyUsingAsm(host);

        ExtractUsingRtl(host);
    }

    private void ExtractUsingRtl(IRewriterHost host)
    {
        var cadapter = new ClusterAdapter(program.Architecture, host);
        var (clusters, cedges) = CollectInstructions(cadapter, host);
        var symbols = program.ImageSymbols;
        Console.WriteLine($"{clusters.Count,9} clusters");
        Console.WriteLine($"{cedges.Count,9} edges");
        var (cblocks, cblockEdges, callTallies) = BuildBlocks(clusters, cedges, cadapter);
        Console.WriteLine($"Blocks:            {cblocks.Count,9}");
        Console.WriteLine($"Edges:             {cedges.Count,9}");
        Console.WriteLine($"Called:            {callTallies.Count,9}");
        Console.WriteLine($"Symbols (fns):     {symbols.Count(de => de.Value.Type == SymbolType.Procedure),9}");
        var cgraph = BuildGraph(cblocks, cblockEdges);
        Console.WriteLine($"Graph: {cgraph.Nodes.Count,9} nodes");

        //cadapter.WriteGraph(cgraph, Console.Out);
    }

    private void ExtractOnlyUsingAsm(IRewriterHost host)
    {
        var iadapter = new InstrAdapter(program.Architecture);
        var (instrs, iedges) = CollectInstructions(iadapter, host);
        Console.WriteLine($"{instrs.Count,9} instructions");
        Console.WriteLine($"{iedges.Count,9} edges");
        var (iblocks, iblockEdges, callTallies) = BuildBlocks(instrs, iedges, iadapter);
        DumpBlocks(iblocks, iblockEdges);
        Console.WriteLine($"{iedges.Count,9} edges");
        var graph = BuildGraph(iblocks, iblockEdges);
        //iadapter.WriteGraph(graph, Console.Out);
    }

    private DirectedGraph<SoupBlock<T>> BuildGraph<T>(Dictionary<Address, SoupBlock<T>> blocks, List<SoupEdge> edges)
        where T : IAddressable
    {
        var sw = Stopwatch.StartNew();
        var graph = new DiGraph<SoupBlock<T>>();
        foreach (var block in blocks.Values)
        {
            graph.AddNode(block);
        }
        foreach (var edge in edges)
        {
            graph.AddEdge(blocks[edge.From], blocks[edge.To]);
        }
        return graph;
    }

    private void DumpBlocks<T>(Dictionary<Address, SoupBlock<T>> blocks, List<SoupEdge> edges)
        where T : IAddressable
    {
        Console.WriteLine($"Blocks:            {blocks.Count,9}");
        Console.WriteLine($"Edges:             {edges.Count,9}");
    }

    private BlockSoupResults<T> BuildBlocks<T>(List<T> instrs, List<SoupEdge> edges, Adapter<T> adapter)
        where T : IAddressable
    {
        HashSet<Address> destAddress = [];
        Dictionary<Address, int> calledAddresses = [];
        foreach (var edge in edges)
        {
            if (edge.EdgeType != EdgeType.Call)
            {
                destAddress.Add(edge.To);
            }
            else
            {
                if (!calledAddresses.TryGetValue(edge.To, out var count))
                {
                    count = 0;
                }
                calledAddresses[edge.To] = count + 1;
            }
        }
        Dictionary<Address, SoupBlock<T>> result = [];
        List<SoupEdge> blockEdges = [];
        Dictionary<Address, SoupBlock<T>> active = [];
        int cInstrs = 0;
        var sw = Stopwatch.StartNew();
        var instrBlocks = new Dictionary<Address, SoupBlock<T>>();
        foreach (var instr in instrs)
        {
            var addr = instr.Address;
            bool isBlockStart = destAddress.Contains(addr);
            if (!active.TryGetValue(addr, out var block))
            {
                block = new SoupBlock<T>(addr);
                result.Add(addr, block);
            }
            else
            {
                active.Remove(addr);
                if (isBlockStart)
                {
                    block.End = addr;
                    //blockEdges.Add(new(EdgeType.Fallthrough, block.Begin, addr));
                    block = new SoupBlock<T>(addr);
                    result.Add(addr, block);
                }
            }
            block.Instrs.Add(instr);
            instrBlocks.Add(addr, block);
            var (iclass, addrNext) = adapter.TryGetFallthroughAddress(instr);
            if (MayFallThrough(iclass))
            {
                active.TryAdd(addrNext, block);
            }
            if (++cInstrs % 100_000 == 0)
            {
                BlockStatus(instrs.Count, result.Count, sw, cInstrs);
            }
        }
        BlockStatus(instrs.Count, result.Count, sw);
        Console.WriteLine();
        foreach (var edge in edges)
        {
            var e = new SoupEdge(edge.EdgeType, instrBlocks[edge.From].Begin, instrBlocks[edge.To].Begin);
            blockEdges.Add(e);
        }
        return new(result, blockEdges, calledAddresses);
    }

    private static bool MayFallThrough(InstrClass iclass)
    {
        if ((iclass & (InstrClass.Return | InstrClass.Invalid)) != 0)
            return false;
        if ((iclass & InstrClass.ConditionalTransfer) == InstrClass.Transfer)
            return false;
        return true;
    }

    private (List<T>, List<SoupEdge>) CollectInstructions<T>(Adapter<T> adapter, IRewriterHost host)
        where T : IAddressable
    {
        var instrs = new List<T>();
        var edges = new List<SoupEdge>();
        foreach (var segment in program.SegmentMap.Segments.Values)
        {
            if (!segment.IsExecutable)
                continue;
            var offsetStart = segment.Address - segment.MemoryArea.BaseAddress;
            var offsetEnd = offsetStart + segment.Size;
            var arch = program.Architecture;
            var step = arch.InstructionBitSize / arch.CodeMemoryGranularity;
            var sw = Stopwatch.StartNew();
            var active = new Dictionary<Address, IEnumerator<T>>();
            for (int offset = 0; offset < segment.Size; offset += step)
            {
                var addr = segment.Address + offset;
                if (!active.TryGetValue(addr, out var e))
                {
                    var rdr = arch.CreateImageReader(segment.MemoryArea, addr);
                    e = adapter.CreateNewEnumerator(rdr);
                }
                else
                {
                    active.Remove(addr);
                }
                try
                {
                    if (e.MoveNext())
                    {
                        var item = e.Current;
                        instrs.Add(item);
                        edges.AddRange(adapter.GetEdges(item)
                            .Where(e => IsEdgeValid(e)));
                        var (_, addrNext) = adapter.TryGetFallthroughAddress(item);
                        active.TryAdd(addrNext, e);
                    }
                }
                catch (Exception ex)
                {
                    host.Error(addr, $"*** {ex.Message}");
                }

                if (offset > 0 && offset % 100_000 == 0)
                {
                    InstrStatus(instrs, segment, sw, offset);
                }
            }
            InstrStatus(instrs, segment, sw);
            sw.Restart();
            Console.WriteLine();
        }
        return (instrs, edges);
    }

    private bool IsEdgeValid(SoupEdge e)
    {
        //$TODO: need an "IProcessorArchitecture.IsValidinstructionAddress"
        if (!program.SegmentMap.IsExecutableAddress(e.To))
            return false;
        return true;
    }

    private static void InstrStatus<T>(
        List<T> instrs,
        ImageSegment segment,
        Stopwatch sw,
        int offset = -1)
    {
        var percentage = offset >= 0
            ? $"[{100.0 * offset / segment.Size:###.0}%]"
            : $"[Done] {sw.Elapsed.TotalSeconds,10:######.0}s";
        Console.Write($"{segment.Name,-12}: 0x{segment.Size:X8} {instrs.Count / sw.Elapsed.TotalMilliseconds,10:######.0} instr/ms {percentage}     \r");
    }

    private static void BlockStatus(
        int instrCount,
        int blockCount,
        Stopwatch sw,
        int offset = -1)
    {
        var percentage = offset >= 0
            ? $"[{100.0 * offset / instrCount:###.0}%]"
            : $"[Done] {sw.Elapsed.TotalSeconds,10:######.0}s";
        Console.Write($"{blockCount,9} blocks: {blockCount / sw.Elapsed.TotalMilliseconds,10:######.0} block/ms {percentage}     \r");
    }


    public void Scan(Project project, IServiceProvider services)
    {
        var decompiler = new Decompiler(project, services);
        var sw = Stopwatch.StartNew();
        decompiler.ScanPrograms();
        Console.WriteLine($"Scanning took {sw.Elapsed.TotalSeconds,10:#####0.0}s");
        Console.WriteLine($"Found {project.Programs[0].Procedures.Count} procedures");
    }
}
