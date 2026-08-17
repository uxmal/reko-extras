using Reko.Analysis;
using Reko.Core;
using Reko.Core.Code;
using Reko.Core.Collections;
using Reko.Core.Diagnostics;
using Reko.Core.Expressions;
using Reko.Core.Graphs;
using Reko.Core.Lib;
using Reko.Core.Operators;
using Reko.Core.Types;
using System.Diagnostics;

namespace Reko.Extras.SeaOfNodes.Nodes;

/// <summary>
/// Transforms the basic blocks of a <see cref="Procedure"/> into a graph of <see cref="Node"/>s.
/// The transformation is identical to the classical SSA transformation, except that the
/// result is a graph which more easily manipulated and transformed than a control flow graph. 
/// The graph is a directed acyclic graph (DAG) of nodes, where each node represents an operation or a value.
/// The nodes are connected by edges that represent data dependencies between the operations.
/// </summary>
public partial class NodeGraphBuilder
    : InstructionVisitor<Node>
    , ExpressionVisitor<Node>
{
    private static readonly TraceSwitch trace = new(nameof(NodeGraphBuilder), "");

    private readonly NodeFactory factory;
    private readonly NodeApplicationBuilder applicationBuilder;
    private readonly ProgramDataFlow programFlow;
    private readonly IProcessorArchitecture arch;
    private readonly Dictionary<Block, BlockState> blocks;
    private readonly HashSet<Procedure> sccProcs;
    private readonly Dictionary<Node, Node> replacements;
    private readonly List<PhiNode> incompletePhis;
    private readonly Dictionary<Node, List<(BitRange, SliceNode)>> availableSlices;
    private Node? cfNode;
    private Block? currentBlock;
    private Block? entryBlock;

    public NodeGraphBuilder(NodeFactory factory, ProgramDataFlow programFlow, IProcessorArchitecture arch)
    {
        this.programFlow = programFlow;
        this.factory = factory;
        this.arch = arch;
        this.applicationBuilder = new NodeApplicationBuilder(this.factory);
        this.blocks = [];
        this.sccProcs = [];
        this.replacements = [];
        this.incompletePhis = [];
        this.availableSlices = [];
    }

    /// <summary>
    /// Tracks the reaching definitions of each storage in a block.
    /// </summary>
    /// <param name="Node"><see cref="BlockNode"/> corresponding to a block.</param>
    /// <param name="RegisterDefs">Reaching definitions for registers.</param>
    /// <param name="FlagGroupDefs">Reaching definitions for flag groups.</param>
    /// <param name="TemporaryDefs">Reaching definitions for temporaries.</param>
    /// 
    private class BlockState { 

        public BlockState(BlockNode node)
        {
            this.Block = node;
            this.RegisterDefs = [];
            this.FlagGroupDefs = [];
            this.SequenceDefs = [];
            this.TemporaryDefs = [];
            this.StackDefs = [];
            this.MemoryNode = null;
        }

        public BlockNode Block { get; }

        public bool IsVisited { get; set; }

        /// <summary>
        /// For each <see cref="StorageDomain"/>, stores a list of reaching
        /// definitions in thie current block. The items in the list are ordered
        /// from "widest" to "narrowest". E.g on x86-64 you might see: 
        /// <code>
        /// rcx, Mem[rax:32]
        /// ecx, ebx + esi
        /// cl,  SLICE(cx, 0)
        /// ch,  SLICE(cx, 1)
        /// </code>
        /// </summary>
        public Dictionary<StorageDomain, List<(RegisterStorage, Node)>> RegisterDefs { get; }

        /// <summary>
        /// For each flag or status register, stores a list of reaching definitions in the current block,
        /// orderd from oldest to most recent.
        /// </summary>
        public Dictionary<RegisterStorage, List<(FlagGroupStorage, Node)>> FlagGroupDefs { get; }

        /// <summary>
        /// Reaching definitions of value node sequences.
        /// </summary>
        public Dictionary<SequenceStorage, Node> SequenceDefs { get; }

        /// <summary>
        /// Reaching definitions of temporary variables.
        /// </summary>
        public Dictionary<TemporaryStorage, Node> TemporaryDefs { get; }

        /// <summary>
        /// Reaching definitions of stack values, indexed by stack offset..
        /// </summary>
        public IntervalTree<int, Node> StackDefs { get; }

        /// <summary>
        /// The reaching definition of the memory node in this block.
        /// </summary>
        public Node? MemoryNode { get; set; }
    }

    private enum ReadStoragePhase
    {
        Resolve,
        AfterSinglePredecessor,
        AfterPhiPredecessor,
    }

    /// <summary>
    /// Transforms the IR in a <see cref="Procedure"/> to a
    /// sea-of-nodes representation, returning the
    /// <see cref="StartNode"/> of the procedure.
    /// </summary>
    /// <param name="proc">Procedure to transform.</param>
    /// <returns>The <see cref="StartNode"/> of the transformed procedure.
    /// </returns>
    public StartNode Transform(Procedure proc)
    {
        StartNode start = factory.Start(proc);
        EndNode end = factory.End(start);
        start.EndNode = end;
        entryBlock = proc.EntryBlock;
        CreateEmptyBlocks(proc);
        LinkBlocks(proc);
        var entryNode = blocks[proc.EntryBlock];
        Node.AddEdge(start, entryNode.Block);
        Node.AddEdge(blocks[proc.ExitBlock].Block, end);

        var rpo = new DfsIterator<Block>(proc.ControlGraph);
        foreach (var block in rpo.ReversePostOrder())
        {
            var state = blocks[block];
            state = TranslateBlock(block, state);
            state.IsVisited = true;
        }
        ProcessIncompletePhis();

        PopulateExitUses(proc.ExitBlock, proc.Architecture);
        return start;
    }

    private void ProcessIncompletePhis()
    {
        while (incompletePhis.Count > 0)
        {
            var work = incompletePhis.ToArray();
            incompletePhis.Clear();
            foreach (var phi in work)
            {
                AddPhiOperands(phi);
            }
        }
    }

    private void AddPhiOperands(PhiNode phi)
    {
        Debug.Assert(phi.Storage is not null, "Incomplete PHI has no storage.");
        Debug.Assert(phi.Inputs.Count != 0 && phi.Inputs[0] is BlockNode,
            "Incomplete PHI is not anchored to a block.");
        if (phi.Inputs.Count > 1)
            return;

        var block = ((BlockNode)phi.Inputs[0]!).Block;
        foreach (var pred in block.Pred)
        {
            var value = ResolveCanonical(ReadStorage(pred, phi.Storage, phi.DataType));
            Node.AddEdge(value, phi);
        }

        var sameNode = GetTrivialPhiReplacement(phi);
        if (sameNode is null)
            return;

        WriteStorage(blocks[block], phi.Storage, sameNode);
        ReplaceNode(phi, sameNode);
    }

    /// <summary>
    /// Creates <see cref="UseNode"/>s in the exit block for the union of all
    /// register storages that reach the exit. When multiple definitions reach
    /// the exit for a storage, <see cref="ReadStorage"/> will create a phi in
    /// the exit block and return that phi as the input value.
    /// </summary>
    private void PopulateExitUses(Block exitBlock, IProcessorArchitecture arch)
    {
        var exitState = blocks[exitBlock];

        var emittedStorages = new HashSet<Storage>();

        var reachingSequences = blocks.Values
            .SelectMany(state => state.SequenceDefs.Keys)
            .Distinct()
            .OrderBy(seq => seq.Name)
            .ToArray();

        foreach (var sequence in reachingSequences)
        {
            foreach (var storage in EnumerateSequenceLeafStorages(sequence))
            {
                if (!emittedStorages.Add(storage))
                    continue;

                var value = ReadStorage(exitBlock, storage, storage.DataType);
                if (!ShouldEmitExitUse(storage, value))
                    continue;
                var use = factory.Use(exitState.Block, storage, default);
                Node.AddEdge(value, use);
            }
        }

        var reachingRegisterLeaves = exitBlock.Pred
            .SelectMany(pred => blocks[pred].RegisterDefs.Values)
            .SelectMany(defs => defs.Select(entry => entry.Item1))
            .Distinct()
            .ToArray();

        var allSeenRegisters = blocks.Values
            .SelectMany(state => state.RegisterDefs.Values)
            .SelectMany(defs => defs.Select(entry => entry.Item1))
            .Distinct()
            .ToArray();

        var reachingRegisters = PruneCoveredRegisters(
            reachingRegisterLeaves
                .Concat(reachingRegisterLeaves.SelectMany(leaf =>
                    allSeenRegisters.Where(reg => reg.Covers(leaf))))
                .Distinct()
            .OrderBy(reg => reg.Name)
            .ThenBy(reg => reg.Number)
            .ToArray());

        foreach (var reg in reachingRegisters)
        {
            if (!emittedStorages.Add(reg))
                continue;
            var value = ReadStorage(exitBlock, reg, reg.DataType);
            if (!ShouldEmitExitUse(reg, value))
                continue;
            var use = factory.Use(exitState.Block, reg, default);
            Node.AddEdge(value, use);
        }

        var reachingFlagGroups = exitBlock.Pred
            .SelectMany(pred => blocks[pred].FlagGroupDefs.Values)
            .SelectMany(defs => defs.Select(entry => entry.Item1))
            .GroupBy(flag => flag.FlagRegister)
            .Select(g => arch.GetFlagGroup(
                g.Key, 
                g.Select(f => f.FlagGroupBits)
                 .Aggregate((a, b) => a | b))!)
            .OrderBy(flag => flag.FlagRegister.Number)
            .ToArray();

        foreach (var flagGroup in reachingFlagGroups)
        {
            Debug.Assert(flagGroup is not null);
            var value = ReadStorage(exitBlock, flagGroup, flagGroup.DataType);
            if (!ShouldEmitExitUse(flagGroup, value))
                continue;
            var use = factory.Use(exitState.Block, flagGroup, default);
            Node.AddEdge(value, use);
        }

        var memAccessed = blocks.Values.Any(b => b.MemoryNode is not null);
        var x = string.Join("; ", blocks.Values
            .Where(b => b.MemoryNode is not null)
            .Select(b => $"{b.Block.Block.Id}: {b.MemoryNode}"));
        if (memAccessed)
        {
            var memStg = MemoryStorage.Instance;
            var dtMem = MemoryStorage.Instance.DataType;
            var m = ReadStorage(exitBlock, memStg, dtMem);
            var useMem = factory.Use(exitState.Block, memStg, default);
            Node.AddEdge(m, useMem);
        }
    }

    private static bool ShouldEmitExitUse(Storage stg, Node value)
    {
        if (value is DefNode defNode && stg == defNode.Storage &&
            (defNode.Inputs.Count != 2 ||
             defNode.Inputs[1] is not CallNode))
        {
            return false;
        }
        return true;
    }

    private static RegisterStorage[] PruneCoveredRegisters(IEnumerable<RegisterStorage> registers)
    {
        var ordered = registers
            .OrderBy(reg => reg.Name)
            .ThenBy(reg => reg.Number)
            .ToArray();

        var remaining = new List<RegisterStorage>(ordered.Length);
        for (int i = 0; i < ordered.Length; ++i)
        {
            var reg = ordered[i];
            var covered = false;
            for (int j = 0; j < ordered.Length; ++j)
            {
                if (i == j)
                    continue;
                if (ordered[j].Covers(reg))
                {
                    covered = true;
                    break;
                }
            }
            if (!covered)
            {
                remaining.Add(reg);
            }
        }
        return remaining.ToArray();
    }

    private void LinkBlocks(Procedure proc)
    {
        foreach (var block in proc.ControlGraph.Blocks)
        {
            var from = blocks[block].Block;
            foreach (var succ in block.Succ)
            {
                Node.AddEdge(from, blocks[succ].Block);
            }
        }
    }

    private BlockState TranslateBlock(Block block, BlockState state)
    {
        this.currentBlock = block;
        this.cfNode = state.Block;
        foreach (var stmt in block.Statements)
        {
            stmt.Instruction.Accept(this);
        }
        return state;
    }

    private Dictionary<Block, BlockState> CreateEmptyBlocks(Procedure proc)
    {
        foreach (var block in proc.ControlGraph.Blocks)
        {
            var node = factory.Block(block);
            blocks[block] = new BlockState(node);
        }
        return blocks;
    }

    public Node VisitAssignment(Assignment ass)
    {
        Debug.Assert(currentBlock is not null);
        var idDst = ass.Dst;

        var value = ass.Src.Accept(this);
        if (value.Storage is null)
            value.Storage = idDst.Storage;
        WriteStorage(blocks[currentBlock], idDst.Storage, value);
        return value;
    }

    public Node VisitBranch(Branch branch)
    {
        var predicate = branch.Condition.Accept(this);
        IfNode ifNode = factory.If(this.cfNode, predicate);
        Debug.Assert(this.currentBlock is not null);
        var falseBranch = this.blocks[currentBlock].Block;
        var trueBranch = this.blocks[branch.Target].Block;
        Node.AddEdge(ifNode, falseBranch);
        Node.AddEdge(ifNode, trueBranch);
        this.cfNode = ifNode;
        return ifNode;
    }

    public Node VisitComment(CodeComment code)
    {
        Console.Out.WriteLine("NYI: {0}", code.GetType());
        throw new NotImplementedException();
    }

    public Node VisitDefInstruction(DefInstruction def)
    {
        Console.Out.WriteLine("NYI: {0}", def.GetType());
        throw new NotImplementedException();
    }

    public Node VisitGotoInstruction(GotoInstruction gotoInstruction)
    {
        Console.Out.WriteLine("NYI: {0}", gotoInstruction.GetType());
        throw new NotImplementedException();
    }

    public Node VisitPhiAssignment(PhiAssignment phi)
    {
        Console.Out.WriteLine("NYI: {0}", phi.GetType());
        throw new NotImplementedException();
    }

    public Node VisitReturnInstruction(ReturnInstruction ret)
    {
        if (cfNode is null)
            throw new InvalidOperationException();
        if (ret.Expression is null)
            return factory.Return(cfNode);

        var value = ret.Expression.Accept(this);
        return factory.Return(cfNode, value);
    }

    public Node VisitSideEffect(SideEffect side)
    {
        var expNode = side.Expression.Accept(this);
        if (cfNode is null)
            throw new InvalidOperationException();
        return factory.SideEffect(cfNode, expNode);
    }

    public Node VisitStore(Store store)
    {
        if (cfNode is null)
            throw new InvalidOperationException();
        if (store.Dst is not MemoryAccess access)
            throw new NotImplementedException();
        var memNode = ReadStorage(currentBlock!, access.MemoryId.Storage, access.MemoryId.DataType);
        Debug.Assert(memNode is not null);
        var ea = access.EffectiveAddress.Accept(this);
        var value = store.Src.Accept(this);
        var storeNode = factory.Store(cfNode, memNode, access.DataType, ea, value);
        WriteStorage(blocks[currentBlock!], access.MemoryId.Storage, storeNode);
        return storeNode;
    }

    public Node VisitSwitchInstruction(SwitchInstruction si)
    {
        var selector = si.Expression.Accept(this);
        var targets = si.Targets.Select(t => t.DisplayName).ToArray();
        var switchNode = factory.Switch(cfNode!, selector, targets);
        Debug.Assert(currentBlock is not null);
        // Link switch node to target blocks
        foreach (var target in si.Targets)
        {
            if (blocks.TryGetValue(target, out var targetState))
            {
                Node.AddEdge(switchNode, targetState.Block);
            }
        }
        cfNode = switchNode;
        return switchNode;
    }

    public Node VisitUseInstruction(UseInstruction use)
    {
        Console.Out.WriteLine("NYI: {0}", use.GetType());
        throw new NotImplementedException();
    }

    public Node VisitAddress(Address addr)
    {
        return factory.Address(addr);
    }

    public Node VisitApplication(Application appl)
    {
        return applicationBuilder.Build(appl, cfNode, expr => expr.Accept(this));
    }

    public Node VisitArrayAccess(ArrayAccess acc)
    {
        Console.Out.WriteLine("NYI: {0}", acc.GetType());
        throw new NotImplementedException();
    }

    public Node VisitBinaryExpression(BinaryExpression binExp)
    {
        if (binExp.Operator.Type == OperatorType.ISub || binExp.Operator.Type == OperatorType.Xor)
        {
            if (binExp.Left is Identifier id && binExp.Right == id)
            {
                var c = factory.Const(Constant.Zero(id.DataType));
                return c;
            }
        }
        var left = binExp.Left.Accept(this);
        var right = binExp.Right.Accept(this);
        return factory.Bin(binExp.DataType, binExp.Operator, null, left, right);
    }

    public Node VisitCast(Cast cast)
    {
        Console.Out.WriteLine("NYI: {0}", cast.GetType());
        throw new NotImplementedException();
    }

    public Node VisitConditionalExpression(ConditionalExpression cond)
    {
        Console.Out.WriteLine("NYI: {0}", cond.GetType());
        throw new NotImplementedException();
    }

    public Node VisitConditionOf(ConditionOf cof)
    {
        var input = cof.Expression.Accept(this);
        return factory.Cond(cof.DataType, null, input);
    }

    public Node VisitConstant(Constant c)
    {
        return factory.Const(c);
    }

    public Node VisitConversion(Conversion conversion)
    {
        var input = conversion.Expression.Accept(this);
        return factory.Convert(null, conversion.DataType, conversion.SourceDataType, input);
    }

    public Node VisitDereference(Dereference deref)
    {
        Console.Out.WriteLine("NYI: {0}", deref.GetType());
        throw new NotImplementedException();
    }

    public Node VisitFieldAccess(FieldAccess acc)
    {
        Console.Out.WriteLine("NYI: {0}", acc.GetType());
        throw new NotImplementedException();
    }

    public Node VisitIdentifier(Identifier id)
    {
        Debug.Assert(currentBlock is not null);
        return ReadStorage(currentBlock, id.Storage, id.DataType);
    }

    private Node ResolveCanonical(Node node)
    {
        Node canonical = node;
        while (replacements.TryGetValue(canonical, out var replacement))
        {
            Debug.Assert(replacement is not null);
            canonical = replacement;
        }

        if (!ReferenceEquals(canonical, node))
        {
            replacements[node] = canonical;
        }
        return canonical;
    }

    private void ReplaceNode(Node original, Node substitute)
    {
        var canonicalSubstitute = ResolveCanonical(substitute);
        replacements[original] = canonicalSubstitute;
        Node.Replace(original, canonicalSubstitute);
    }


    /// <summary>
    /// To avoid recursion and exhausting the return stack, we reify the
    /// stack of read-storage operations into a work queue.
    /// </summary>
    /// <param name="Phase"></param>
    /// <param name="Block">Block being processed.</param>
    /// <param name="Phi"></param>
    /// <param name="PredecessorIndex"></param>
    private record struct ReadStorageFrame(
        ReadStoragePhase Phase,
        Block Block,
        PhiNode? Phi,
        int PredecessorIndex);


    /// <summary>
    /// Searches "backwards" to locate the most recent definition of the given
    /// <paramref name="storage"/>, starting at the given <paramref name="block"/>.
    /// If no definition was found, we search the predecessor(s) of the block.
    /// If there are multiple predecessors, we create a <see cref="PhiNode"/>
    /// to merge the definitions of the various predecessors. If we reach the entry
    /// block without finding a definition, we create a new <see cref="DefNode"/>.
    /// </summary>
    /// <param name="block"></param>
    /// <param name="storage"></param>
    /// <param name="dt"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private Node ReadStorage(Block block, Storage storage, DataType dt)
    {
        var work = new Stack<ReadStorageFrame>();
        work.Push(new ReadStorageFrame(ReadStoragePhase.Resolve, block, null, 0));

        Node? lastResult = null;
        while (work.TryPop(out var frame))
        {
            switch (frame.Phase)
            {
            case ReadStoragePhase.Resolve:
            {
                var state = blocks[frame.Block];
                // If it's defined in the current block, return the latest definition.
                lastResult = ReadLocalStorage(storage, state, frame);
                if (lastResult is not null)
                {
                    WriteStorage(state, storage, lastResult);
                    break;
                }

                if (frame.Block.Pred.Any(b => !blocks[b].IsVisited))
                {
                    // Incomplete CFG.
                    var incompletePhi = factory.Phi(dt, state.Block);
                    incompletePhi.Storage = storage;
                    WriteStorage(state, storage, incompletePhi);
                    this.incompletePhis.Add(incompletePhi);
                    lastResult = incompletePhi;
                    break;
                }
                // If it's not defined and this is the entry block,
                // create a new def node.
                if (frame.Block == entryBlock)
                {
                    lastResult = CreateDefNode(state, storage, dt);
                    break;
                }

                if (frame.Block.Pred.Count == 0)
                    throw new InvalidOperationException("Unable to resolve storage definition due to missing predecessors.");

                if (frame.Block.Pred.Count == 1)
                {
                    work.Push(new(ReadStoragePhase.AfterSinglePredecessor, frame.Block, null, 0));
                    work.Push(new(ReadStoragePhase.Resolve, frame.Block.Pred[0], null, 0));
                    break;
                }

                var phi = factory.Phi(dt, state.Block);
                phi.Storage = storage;
                WriteStorage(state, storage, phi);

                work.Push(new(ReadStoragePhase.AfterPhiPredecessor, frame.Block, phi, 0));
                work.Push(new(ReadStoragePhase.Resolve, frame.Block.Pred[0], null, 0));
                break;
            }

            case ReadStoragePhase.AfterSinglePredecessor:
                if (lastResult is null)
                    throw new InvalidOperationException();
                WriteStorage(blocks[frame.Block], storage, lastResult);
                break;

            case ReadStoragePhase.AfterPhiPredecessor:
            {
                if (frame.Phi is null)
                    throw new InvalidOperationException();
                if (lastResult is null)
                    throw new InvalidOperationException();
                lastResult = ResolveCanonical(lastResult);
                Node.AddEdge(lastResult, frame.Phi);

                var nextPredIndex = frame.PredecessorIndex + 1;
                if (nextPredIndex < frame.Block.Pred.Count)
                {
                    work.Push(new ReadStorageFrame(ReadStoragePhase.AfterPhiPredecessor, frame.Block, frame.Phi, nextPredIndex));
                    work.Push(new ReadStorageFrame(ReadStoragePhase.Resolve, frame.Block.Pred[nextPredIndex], null, 0));
                    break;
                }

                var sameNode = GetTrivialPhiReplacement(frame.Phi);
                if (sameNode is not null)
                {
                    WriteStorage(blocks[frame.Block], storage, sameNode);
                    ReplaceNode(frame.Phi, sameNode);
                    lastResult = ResolveCanonical(sameNode);
                }
                else
                {
                    lastResult = frame.Phi;
                }
                break;
            }

            default:
                throw new InvalidOperationException($"Unexpected read-storage phase: {frame.Phase}.");
            }
        }

        if (lastResult is null)
            throw new InvalidOperationException();
        return lastResult;
    }

    private DefNode CreateDefNode(BlockState state, Storage storage, DataType dt)
    {
        var defNode = factory.Def(state.Block, storage, dt);
        WriteStorage(state, storage, defNode);
        return defNode;
    }

    private static IEnumerable<Storage> EnumerateSequenceLeafStorages(SequenceStorage sequence)
    {
        foreach (var element in sequence.Elements)
        {
            if (element is SequenceStorage nested)
            {
                foreach (var nestedElement in EnumerateSequenceLeafStorages(nested))
                {
                    yield return nestedElement;
                }
            }
            else
            {
                yield return element;
            }
        }
    }

    private void ReplaceCoveredDefsWithSlices(BlockState state, SequenceStorage sequence, Node value)
    {
        foreach (var element in sequence.Elements)
        {
            var offset = sequence.OffsetOf(element);
            Debug.Assert(offset >= 0);

            if (element is RegisterStorage reg)
            {
                var slice = MakeSlice(reg.DataType, value, offset);
                slice.Storage = reg;
                if (state.RegisterDefs.TryGetValue(reg.Domain, out var existingRegDefs))
                {
                    foreach (var (_, existingRegDef) in existingRegDefs)
                    {
                        ReplaceNode(existingRegDef, slice);
                    }
                }
                state.RegisterDefs[reg.Domain] = [(reg, slice)];
            }
            else if (element is SequenceStorage)
            {
                Debug.Fail("Can't have a nestedSequenceStorage.");
            }
        }
    }

    /// <summary>
    /// Writes storages for each of the elements of the <see cref="SequenceStorage"/>. 
    /// </summary>
    /// <remarks>
    /// The slices are created eagerly, which may cause a lot of garbage if the slices are not used.
    /// However, this is necessary to ensure that the slices are available for use in the current block.
    /// A more complex solution would be to create the slices lazily, but that would require a more
    /// complex data structure to track the slices and their offsets.
    /// </remarks>
    /// <param name="state"></param>
    /// <param name="sequence"></param>
    /// <param name="value"></param>
    private void WriteSubelementStorages(BlockState state, SequenceStorage sequence, Node value)
    {
        var bitMax = (int) sequence.BitSize;
        foreach (var reg in sequence.Elements)
        {
            var bitMin = bitMax - (int)reg.BitSize;
            //$FUTURE: consider deferring slice creation until actually needed.
            WriteStorage(state, reg, MakeSlice(reg.DataType, value, bitMin));
            bitMax = bitMin;
        }
    }

    private Node? ReadLocalStorage(Storage storage, BlockState state, in ReadStorageFrame frame)
    {
        switch (storage)
        {
        case FlagGroupStorage flagGroup:
            var flagValue = TryReadFlagGroupStorage(frame.Block, flagGroup);
            if (flagValue is not null)
            {
                return flagValue;
            }
            break;
        case RegisterStorage regUse:
            var regValue = TryReadRegisterStorage(frame, state, regUse);
            if (regValue is not null)
            {
                return regValue;
            }
            break;
        case SequenceStorage seq:
            if (state.SequenceDefs.TryGetValue(seq, out var seqNode))
            {
                return ResolveCanonical(seqNode);
            }
            break;
        case TemporaryStorage temp:
            if (state.TemporaryDefs.TryGetValue(temp, out var tempNode))
            {
                return ResolveCanonical(tempNode);
            }
            break;
        case StackStorage stk:
            var interval = CreateBitInterval(stk.StackOffset, storage.DataType);
            if (state.StackDefs.TryGetInterval(interval, out var stackNode))
            {
                return ResolveCanonical(stackNode);
            }
            return null;
        case MemoryStorage mem:
            return state.MemoryNode;
        default: throw new NotImplementedException(storage.GetType().Name);
        }
        return null;

    }

    private Node? TryReadRegisterStorage(ReadStorageFrame frame, BlockState state, RegisterStorage regUse)
    {
        trace.Verbose("  TryReadRegisterStorage: ({0}, {1}, ({2})", frame.Block.DisplayName, regUse);
        if (!state.RegisterDefs.TryGetValue(regUse.Domain, out var reachingDefs))
            return null;

        // At least some of the bits of 'regUse' are available locally in this 
        // block. Walk across the bits of 'regUse', collecting all parts
        // defined into a sequence.
        int offsetLo = (int)regUse.BitAddress;
        int offsetHi = (int)(regUse.BitAddress + regUse.BitSize);
        var subNodes = new List<Node>();
        while (offsetLo < offsetHi)
        {
            var useRange = new BitRange(offsetLo, offsetHi);
            var (sidElem, usedRange, defRange) = FindIntersectingRegister(reachingDefs, useRange);
            if (sidElem is null || offsetLo < usedRange.Lsb)
            {
                // Found a gap in the register that wasn't defined in
                // this basic block. Seek backwards into predecessor
                // blocks.
                var bitrangeR = sidElem is null
                    ? useRange
                    : new BitRange(offsetLo, usedRange.Lsb);

                var predecessorValue = ReadStorageFromPredecessors(frame.Block, regUse, regUse.DataType);
                if (predecessorValue is null)
                    return null;
                predecessorValue = MaybeSlice(predecessorValue, bitrangeR);
                subNodes.Add(predecessorValue);
                offsetLo = bitrangeR.Msb;
            }
            if (sidElem is not null)
            {
                sidElem = MaybeSlice(sidElem, usedRange);
                subNodes.Add(sidElem);
                offsetLo = usedRange.Msb;
            }
        }
        if (subNodes.Count == 1)
        {
            return subNodes[0];
        }
        else
        {
            subNodes.Reverse(); // Order sids in big-endian order
            var seq = factory.Seq(regUse.DataType, subNodes.ToArray());
            return seq;
        }
    }

    private Node MaybeSlice(Node value, BitRange bitRange)
    {
        if (bitRange.Extent < value.DataType.BitSize)
        {
            value = MakeSlice(
                PrimitiveType.CreateWord(bitRange.Extent),
                value,
                bitRange.Lsb);
        }
        return value;
    }

    private (Node? sidElem, BitRange usedRange, BitRange defRange) FindIntersectingRegister(
        List<(RegisterStorage, Node)> definitions,
        BitRange useRange)
    {
        var result = ((Node?)null, useRange, default(BitRange));
        for (int i = definitions.Count - 1; i >= 0; --i)
        {
            var (regDef, sid) = definitions[i];
            var defRange = regDef.GetBitRange();
            var intersection = defRange.Intersect(useRange);
            if (!intersection.IsEmpty && (result.Item1 is null || result.useRange.Lsb > intersection.Lsb))
            {
                defRange = new BitRange(intersection.Lsb, intersection.Msb);
                result = (sid, intersection, defRange);
                useRange = new BitRange(useRange.Lsb, defRange.Lsb);
            }
        }
        return result;
    }

    private SliceNode MakeSlice(DataType dt, Node slicedValue, int offset)
    {
        BitRange range = new(offset, offset + dt.BitSize);
        if (!this.availableSlices.TryGetValue(slicedValue, out var slices))
        {
            slices = [];
            this.availableSlices.Add(slicedValue, slices);
        }
        foreach (var slice in slices)
        {
            if (slice.Item1 == range)
            {
                return slice.Item2;
            }
        }
        var newSlice = factory.Slice(dt, slicedValue, offset);
        slices.Add((range, newSlice));
        return newSlice;
    }

    private Node? ReadStorageFromPredecessors(Block block, Storage storage, DataType dt)
    {
        if (block == entryBlock)
            return null;

        if (block.Pred.Count == 0)
            return null;

        if (block.Pred.Count == 1)
            return ReadStorage(block.Pred[0], storage, dt);

        var state = blocks[block];
        var phi = factory.Phi(dt, state.Block);
        phi.Storage = storage;
        foreach (var pred in block.Pred)
        {
            var predValue = ReadStorage(pred, storage, dt);
            Node.AddEdge(ResolveCanonical(predValue), phi);
        }

        var sameNode = GetTrivialPhiReplacement(phi);
        if (sameNode is not null)
        {
            ReplaceNode(phi, sameNode);
            return ResolveCanonical(sameNode);
        }
        return phi;
    }

    private static Node? GetTrivialPhiReplacement(PhiNode phi)
    {
        Node? candidate = null;
        foreach (var input in phi.Inputs.Skip(1).Cast<Node>())
        {
            if (input is null || ReferenceEquals(input, phi))
                continue;

            if (candidate is null)
            {
                candidate = input;
                continue;
            }

            if (!ReferenceEquals(candidate, input))
                return null;
        }
        return candidate;
    }

    private Node? TryReadFlagGroupStorage(Block block, FlagGroupStorage storage)
    {
        var state = blocks[block];
        if (!state.FlagGroupDefs.TryGetValue(storage.FlagRegister, out var defs) || defs.Count == 0)
            return null;

        var requestedMask = storage.FlagGroupBits;
        List<Node> fragments = [];
        for (int i = defs.Count - 1; requestedMask != 0 && i >= 0; --i)
        {
            var (candidateStorage, candidateNode) = defs[i];

            if (!candidateStorage.OverlapsWith(storage))
                continue;

            candidateNode = ResolveCanonical(candidateNode);
            ulong interSection = candidateStorage.FlagGroupBits & requestedMask;
            if (interSection != requestedMask)
            {
                candidateNode = factory.And(candidateNode, requestedMask);
                candidateNode.Storage = candidateStorage;
            }
            fragments.Add(candidateNode);
            requestedMask &= ~interSection;
        }
        if (fragments.Count == 0)
            return null;
        if (requestedMask != 0)
        {
            // Some bits left to read, but we don't have a definition
            // in this block. Seek backwards into predecessors.
            var newStg = arch.GetFlagGroup(storage.FlagRegister, requestedMask);
            Debug.Assert(newStg is not null);
            var predNode = this.ReadStorageFromPredecessors(block, newStg, storage.DataType);
            if (predNode is not null)
                fragments.Add(predNode);
        }
        Node result;
        result = fragments[0];
        for (int i = 1; i < fragments.Count; ++i)
        {
            result = factory.Or(result, fragments[i]);
        }
        WriteStorage(state, storage, result);
        return result;
    }

    private void WriteStorage(BlockState state, Storage stgDst, Node value)
    {
        value = ResolveCanonical(value);
        if (value.Storage is null)
            value.Storage = stgDst;
        switch (stgDst)
        {
        case RegisterStorage regDst:
            if (!state.RegisterDefs.TryGetValue(regDst.Domain, out var defs))
            {
                defs = [];
                state.RegisterDefs[regDst.Domain] = defs;
            }
            for (int i = 0; i < defs.Count; ++i)
            {
                var (stg, valuePrev) = defs[i];
                if (stgDst.Covers(stg))
                {
                    defs.RemoveAt(i);
                    --i;
                }
            }
            defs.Add((regDst, value));
            state.RegisterDefs[regDst.Domain] = defs;
            break;
        case FlagGroupStorage flagGroup:
            if (!state.FlagGroupDefs.TryGetValue(flagGroup.FlagRegister, out var flagDefs))
            {
                flagDefs = [];
                state.FlagGroupDefs[flagGroup.FlagRegister] = flagDefs;
            }

            flagDefs.RemoveAll(entry => flagGroup.Covers(entry.Item1));
            flagDefs.Add((flagGroup, value));
            break;
        case SequenceStorage seq:
            state.SequenceDefs[seq] = value;
            if (value is DefNode)
            {
                ReplaceCoveredDefsWithSlices(state, seq, value);
            }
            else
            {
                WriteSubelementStorages(state, seq, value);
            }
            break;
        case TemporaryStorage tmp:
            state.TemporaryDefs[tmp] = value;
            break;
        case StackStorage stk:
            state.StackDefs.Add(CreateBitInterval(stk.StackOffset, value.DataType), value);
            break;
        case MemoryStorage mem:
            state.MemoryNode = value;
            break;
        default:  
            throw new NotImplementedException(stgDst.GetType().Name);
        }
    }

    internal Interval<int> CreateBitInterval(int unitStackOffset, DataType dt)
    {
        var bitsPerUnit = arch.MemoryGranularity;
        var bitOffset = unitStackOffset * bitsPerUnit;
        return Interval.Create(
            bitOffset,
            bitOffset +
                dt.MeasureBitSize(arch.MemoryGranularity));
    }

    public Node VisitMemberPointerSelector(MemberPointerSelector mps)
    {
        Console.Out.WriteLine("NYI: {0}", mps.GetType());
        throw new NotImplementedException();
    }

    public Node VisitMemoryAccess(MemoryAccess access)
    {
        if (cfNode is null)
            throw new InvalidOperationException();

        var memNode = ReadStorage(currentBlock!, access.MemoryId.Storage, access.MemoryId.DataType);
        var ea = access.EffectiveAddress.Accept(this);
        return factory.Load(cfNode, memNode, access.DataType, ea);
    }

    public Node VisitMkSequence(MkSequence seq)
    {
        var inputs = seq.Expressions
            .Select(expr => expr.Accept(this))
            .ToArray();
        return factory.Seq(seq.DataType, inputs);
    }

    public Node VisitOutArgument(OutArgument outArgument)
    {
        Console.Out.WriteLine("NYI: {0}", outArgument.GetType());
        throw new NotImplementedException();
    }

    public Node VisitPhiFunction(PhiFunction phi)
    {
        Console.Out.WriteLine("NYI: {0}", phi.GetType());
        throw new NotImplementedException();
    }

    public Node VisitPointerAddition(PointerAddition pa)
    {
        Console.Out.WriteLine("NYI: {0}", pa.GetType());
        throw new NotImplementedException();
    }

    public Node VisitProcedureConstant(ProcedureConstant pc)
    {
        return factory.ProcedureConstant(pc.DataType, pc.Procedure);
    }

    public Node VisitScopeResolution(ScopeResolution scopeResolution)
    {
        Console.Out.WriteLine("NYI: {0}", scopeResolution.GetType());
        throw new NotImplementedException();
    }

    public Node VisitSegmentedAddress(SegmentedPointer address)
    {
        var seg = address.BasePointer.Accept(this);
        var off = address.Offset.Accept(this);
        return factory.SegPtr(address.DataType, seg, off);
    }

    public Node VisitSlice(Slice slice)
    {
        var input = slice.Expression.Accept(this);
        return MakeSlice(slice.DataType, input, slice.Offset);
    }

    public Node VisitStringConstant(StringConstant str)
    {
        Console.Out.WriteLine("NYI: {0}", str.GetType());
        throw new NotImplementedException();
    }

    public Node VisitTestCondition(TestCondition tc)
    {
        var input = tc.Expression.Accept(this);
        return factory.Test(tc.DataType, tc.ConditionCode, null, input);
    }

    public Node VisitUnaryExpression(UnaryExpression unary)
    {
        var operand = unary.Expression.Accept(this);
        return factory.Unary(unary.DataType, unary.Operator, null, operand);
    }
}
