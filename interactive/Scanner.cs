using Reko.Core;
using Reko.Core.Rtl;
using Reko.Core.Services;
using Reko.Scanning;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reko.Extras.Interactive;

internal class Scanner
{
    private readonly Program program;
    private readonly IEventListener listener;
    private readonly IDecompilerHost host;
    private readonly IRewriterHost rwhost;
    private ScanResults sr;
    private readonly StorageBinder binder;
    private readonly Dictionary<Address, RtlBlock> blockStarts;
    private readonly Dictionary<Address, Address> blockEnds;

    public Scanner(
        Program program,
        IEventListener listener,
        IDecompilerHost host,
        IRewriterHost rwhost)
    {
        this.program = program;
        this.listener = listener;
        this.host = host;
        this.rwhost = rwhost;
        this.sr = new ScanResults();

        this.binder = new StorageBinder();
        this.blockStarts = [];
        this.blockEnds = [];
        this.sr = new();
    }

    public ScanResults ScanImage()
    {
        var items = CollectWorkitems();
        var wis = MakeQueue(items);
        listener.Progress.ShowProgress("Scanning...", 0, wis.Count);
        while (wis.TryDequeue(out var wi))
        {
            var nodes = sr.CFG.Nodes.Count;
            listener.Progress.ShowProgress(blockEnds.Count, blockEnds.Count + wis.Count);
            var block = blockStarts[wi.Address];
            block = this.Process(wi, block);
            if (block is not null)
            {
                blockEnds.TryAdd(block.Instructions[^1].Address, block.Address);
                AddEdges(wi, block, wis);
            }
        }
        listener.Progress.Finish();
        host.OnCompleted();
        return sr;
    }

    private IEnumerable<Workitem> CollectWorkitems()
    {
        var items = program.EntryPoints.Values.Select(ep => new Workitem(
            ep.Address,
            ep.ProcessorState ?? ep.Architecture.CreateProcessorState()));
        items = items.Concat(program.ImageSymbols.Values.Select(sym =>
            new Workitem(
                sym.Address,
                sym.ProcessorState ?? sym.Architecture.CreateProcessorState())));
        return items;
    }

    private Queue<Workitem> MakeQueue(IEnumerable<Workitem> items)
    {
        var wis = new Queue<Workitem>();
        foreach (var item in items)
        {
            sr.CFG.AddNode(item.Address);
            blockStarts[item.Address] = EmptyBlock(item.State.Architecture, item.Address);
            wis.Enqueue(item);
        }
        return wis;
    }

    private RtlBlock EmptyBlock(IProcessorArchitecture arch, Address addr)
    {
        var id = NamingPolicy.Instance.BlockName(addr);
        var block = RtlBlock.CreateEmpty(arch, addr, id);
        block.Provenance = ProvenanceType.Scanning;
        return block;
    }

    private void AddEdges(Workitem wi, RtlBlock block, Queue<Workitem> wis)
    {
        var lastRtl = block.Instructions[^1].Instructions[^1];
        switch (lastRtl)
        {
        case RtlBranch branch:
            EnqueueEdge(block.Address, block.FallThrough, wi.State, wis);
            if (branch.Target is Address addrTarget)
            {
                EnqueueEdge(block.Address, addrTarget, wi.State.Clone(), wis);
            }
            break;
        case RtlGoto g:
            if (g.Target is Address addrTarget2)
            {
                EnqueueEdge(block.Address, addrTarget2, wi.State, wis);
            }
            else
            {
                // throw new NotImplementedException("Computed goto");
            }
            break;
        case RtlCall call:
            //$non-returning calls.
            EnqueueEdge(block.Address, block.FallThrough, wi.State.Clone(), wis);
            break;
        case RtlAssignment _:
        case RtlReturn _:
        case RtlInvalid _:
        case RtlSideEffect _:
            break;
        default:
            throw new NotImplementedException($"Unimplemented {lastRtl}");
        }
    }

    private void EnqueueEdge(Address addrFrom, Address addrTo, ProcessorState state, Queue<Workitem> wis)
    {
        if (!blockStarts.TryGetValue(addrTo, out var block))
        {
            if (blockStarts.TryAdd(addrTo, EmptyBlock(state.Architecture, addrTo)))
            {
                wis.Enqueue(new Workitem(addrTo, state));
                sr.CFG.Nodes.Add(addrTo);
            }
        }
        sr.CFG.AddEdge(addrFrom, addrTo);
    }

    private RtlBlock? Process(Workitem wi, RtlBlock block)
    {
        if (!program.TryCreateImageReader(wi.Address, out var rdr))
            return null;
        var rw = program.Architecture.CreateRewriter(rdr, wi.State, binder, rwhost);
        var clusters = block.Instructions;
        var iclassPrev = InstrClass.None;
        var e = rw.GetEnumerator();
        Address addrLast = wi.Address;
        while (e.MoveNext())
        {
            var cluster = e.Current;
            host.OnBeforeInstruction(sr.CFG, block, cluster.Address);
            var iclass = cluster.Class;
            if (HasDelaySlot(iclass))
            {
                var nextCluster = StealNextInstruction(cluster, e, clusters);
                if (nextCluster is null)
                {
                    clusters.Add(new RtlInstructionCluster(
                        cluster.Address,
                        cluster.Length,
                        [new RtlInvalid()])
                    {
                        Class = InstrClass.Invalid
                    });

                    var invalidBlock = RtlBlock.Create(
                        wi.State.Architecture,
                        wi.Address,
                        NamingPolicy.Instance.BlockName(wi.Address),
                        (int)(clusters[^1].Address - wi.Address + clusters[^1].Length),
                        addrLast,
                        ProvenanceType.Scanning,
                        clusters);
                    return invalidBlock;
                }
            }
            clusters.Add(cluster);
            addrLast = e.Current.Address + e.Current.Length;
            if (ReachedEndOfBlock(iclass, iclassPrev))
                break;
        }
        block.Length = (int)(clusters[^1].Address - wi.Address + clusters[^1].Length);
        block.FallThrough = addrLast;
        return block;
    }

    private bool HasDelaySlot(InstrClass iclass)
    {
        var DT = InstrClass.Delay | InstrClass.Transfer;
        return ((iclass & DT) == DT);
    }

    private RtlInstructionCluster? StealNextInstruction(
        RtlInstructionCluster transferCluster,
        IEnumerator<RtlInstructionCluster> e,
        List<RtlInstructionCluster> rtls)
    {
        if (!e.MoveNext())
            return null;

        var stolen = e.Current;

        List<RtlInstruction> newRtl = [];
        var lastRtl = transferCluster.Instructions[^1];
        switch (lastRtl)
        {
        case RtlBranch branch:
        {
            var tmp = binder.CreateTemporary(branch.Condition.DataType);
            var ass = new RtlAssignment(tmp, branch.Condition);
            newRtl.Add(ass);
            lastRtl = new RtlBranch(tmp, branch.Target, branch.Class);
            break;
        }
        case RtlGoto g:
        {
            if (g.Target is not Address)
            {
                var tmp = binder.CreateTemporary(g.Target.DataType);
                var ass = new RtlAssignment(tmp, g.Target);
                newRtl.Add(ass);
                lastRtl = new RtlGoto(tmp, g.Class);
            }
            break;
        }
        case RtlCall call:
            if (call.Target is not Address)
            {
                var tmp = binder.CreateTemporary(call.Target.DataType);
                var ass = new RtlAssignment(tmp, call.Target);
                newRtl.Add(ass);
                lastRtl = new RtlGoto(tmp, call.Class);
            }
            break;
        case RtlReturn:
            break;
        default:
            throw new NotImplementedException();
        }
        newRtl.AddRange(stolen.Instructions);
        var hybrid = new RtlInstructionCluster(
            transferCluster.Address,
            transferCluster.Length,
            newRtl.ToArray())
        {
            Class = InstrClass.Linear
        };
        rtls.Add(hybrid);
        transferCluster.Instructions[^1] = lastRtl;
        return transferCluster;
    }

    private bool ReachedEndOfBlock(InstrClass iclass, InstrClass iclassPrev)
    {
        if ((iclass & (InstrClass.Transfer | InstrClass.Invalid)) != 0)
            return true;
        if (((iclassPrev ^ iclass) & (InstrClass.Padding | InstrClass.Zero)) != 0)
            return true;
        return false;
    }


}
