using Reko.Core;
using Reko.Core.Graphs;
using Reko.Core.Machine;
using Reko.Core.Memory;
using Reko.Core.Rtl;
using Reko.Scanning;
using System.Diagnostics.CodeAnalysis;

namespace Reko.Extras.blocksoup;

public abstract class Adapter<T>
    where T : IAddressable
{
    public abstract IEnumerator<T> CreateNewEnumerator(EndianImageReader rdr);

    public abstract (InstrClass, Address) TryGetFallthroughAddress(T item);

    public abstract IEnumerable<SoupEdge> GetEdges(T item);

    public abstract void WriteGraph(DirectedGraph<SoupBlock<T>> graph, TextWriter w);
}

public class InstrAdapter : Adapter<MachineInstructionEx>
{
    private readonly IProcessorArchitecture arch;

    public InstrAdapter(IProcessorArchitecture arch)
    {
        this.arch = arch;
    }

    public override IEnumerator<MachineInstructionEx> CreateNewEnumerator(EndianImageReader rdr)
    {
        return arch.CreateDisassembler(rdr)
            .Select(i => new MachineInstructionEx(i))
            .GetEnumerator();
    }

    public override IEnumerable<SoupEdge> GetEdges(MachineInstructionEx item)
    {
        var instr = item.Instruction;
        if (instr.Operands.Length == 0 ||
            !instr.InstructionClass.HasFlag(InstrClass.Transfer))
            yield break;

        if (instr.Operands[^1] is not Address addrTo)
        {
            yield break;
        }
        var addrFrom = instr.Address;
        if (instr.InstructionClass.HasFlag(InstrClass.Conditional))
        {
            //$TODO: delay slots complicate this.
            yield return new(EdgeType.Fallthrough, addrFrom, addrFrom + instr.Length);
        }
        if (instr.InstructionClass.HasFlag(InstrClass.Call))
        {
            yield return new(EdgeType.Call, addrFrom, addrTo);
        }
        else if (instr.InstructionClass.HasFlag(InstrClass.Call))
        {
            yield return new(EdgeType.Jump, addrFrom, addrTo);
        }
    }

    public override (InstrClass, Address) TryGetFallthroughAddress(MachineInstructionEx item)
    {
        var address = item.Address + item.Instruction.Length;
        return (InstrClass.Transfer, address);
    }

    public override void WriteGraph(DirectedGraph<SoupBlock<MachineInstructionEx>> graph, TextWriter w)
    {
        foreach (var block in graph.Nodes.OrderBy(b => b.Begin))
        {
            w.WriteLine($"l{block.Begin}:");
            w.WriteLine($"    // Pred: {string.Join(", ", graph.Predecessors(block).Select(b => $"l{b.Begin}"))}");
            foreach (var instr in block.Instrs)
            {
                w.WriteLine($"    {instr}");
            }
            w.WriteLine($"    // Succ: {string.Join(", ", graph.Successors(block).Select(b => $"l{b.Begin}"))}");
        }
    }
}


public class ClusterAdapter : Adapter<RtlClusterEx>
{
    private IProcessorArchitecture arch;
    private IRewriterHost host;
    private StorageBinder binder;
    private ProcessorState state;

    public ClusterAdapter(IProcessorArchitecture arch, IRewriterHost host)
    {
        this.arch = arch;
        this.host = host;
        this.binder = new StorageBinder();
        this.state = arch.CreateProcessorState();
    }

    public override IEnumerator<RtlClusterEx> CreateNewEnumerator(EndianImageReader rdr)
    {
        return arch.CreateRewriter(rdr, state, binder, host)
            .Select(c => new RtlClusterEx(c)).GetEnumerator();
    }

    public override IEnumerable<SoupEdge> GetEdges(RtlClusterEx item)
    {
        foreach (var rtl in item.Cluster.Instructions)
        {
            //$TODO: handle delay slots and instructions that aren't
            // the last RTL in the cluster.
            switch (rtl)
            {
            case RtlBranch branch when branch.Target is Address addrTo:
                yield return new(EdgeType.Jump, item.Address, item.Address + item.Cluster.Length);
                yield return new(EdgeType.Jump, item.Address, addrTo);
                break;
            case RtlGoto g when g.Target is Address addrGoto:
                yield return new(EdgeType.Jump, item.Address, addrGoto);
                break;
            case RtlCall call when call.Target is Address addrCall:
                yield return new(EdgeType.Call, item.Address, addrCall);
                break;
            }
        }
    }

    public override (InstrClass, Address) TryGetFallthroughAddress(RtlClusterEx item)
    {
        var iclass = item.Cluster.Instructions[^1].Class;
        var address = item.Address + item.Cluster.Length;
        return (iclass, address);
    }

    public override void WriteGraph(DirectedGraph<SoupBlock<RtlClusterEx>> graph, TextWriter w)
    {
        foreach (var block in graph.Nodes.OrderBy(b => b.Begin))
        {
            w.WriteLine($"l{block.Begin}:");
            w.WriteLine($"    // Pred: {string.Join(", ", graph.Predecessors(block).Select(b => $"l{b.Begin}"))}");
            foreach (var cluster in block.Instrs)
            {
                w.WriteLine($"    {cluster.Address} {cluster.Cluster.Class}");
                foreach (var rtl in cluster.Cluster.Instructions)
                {
                    w.WriteLine($"      {rtl}");
                }
            }
            w.WriteLine($"    // Succ: {string.Join(", ", graph.Successors(block).Select(b => $"l{b.Begin}"))}");
        }
    }

}