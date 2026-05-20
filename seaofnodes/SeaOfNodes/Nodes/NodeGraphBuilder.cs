using Reko.Analysis;
using Reko.Core;
using Reko.Core.Code;
using Reko.Core.Collections;
using Reko.Core.Expressions;
using Reko.Core.Graphs;
using Reko.Core.Lib;
using Reko.Core.Operators;
using Reko.Core.Types;
using System.Diagnostics;

namespace Reko.Extras.SeaOfNodes.Nodes;

public partial class NodeGraphBuilder
    : InstructionVisitor<Node>
    , ExpressionVisitor<Node>
{
    private readonly NodeFactory factory;
    private readonly NodeApplicationBuilder applicationBuilder;
    private readonly ProgramDataFlow programFlow;
    private readonly IProcessorArchitecture arch;
    private readonly Dictionary<Block, BlockState> blocks;
    private readonly HashSet<Procedure> sccProcs;
    private readonly Dictionary<Node, Node> replacements;
    private Node? cfNode;
    private Block? currentBlock;
    private Block? entryBlock;
    private MemoryNode? memNode;

    public NodeGraphBuilder(NodeFactory factory, ProgramDataFlow programFlow, IProcessorArchitecture arch)
    {
        this.programFlow = programFlow;
        this.factory = factory;
        this.arch = arch;
        this.applicationBuilder = new NodeApplicationBuilder(this.factory);
        this.blocks = [];
        this.sccProcs = [];
        this.replacements = [];
    }

    /// <summary>
    /// Tracks the reaching definitions of each storage in a block.
    /// </summary>
    /// <param name="Node"><see cref="BlockNode"/> corresponding to a block.</param>
    /// <param name="RegisterDefs">Reaching definitions for registers.</param>
    /// <param name="FlagGroupDefs">Reaching definitions for flag groups.</param>
    /// <param name="TemporaryDefs">Reaching definitions for temporaries.</param>
    private class BlockState { 

        public BlockState(BlockNode node)
        {
            this.Node = node;
            this.RegisterDefs = [];
            this.FlagGroupDefs = [];
            this.SequenceDefs = [];
            this.TemporaryDefs = [];
            this.StackDefs = [];
        }

        public BlockNode Node { get; }

        public Dictionary<RegisterStorage, List<(BitRange, ExpressionNode)>> RegisterDefs { get; }
        public Dictionary<RegisterStorage, List<(FlagGroupStorage, ExpressionNode)>> FlagGroupDefs { get; }
        public Dictionary<SequenceStorage, ExpressionNode> SequenceDefs { get; }
        public Dictionary<TemporaryStorage, ExpressionNode> TemporaryDefs { get; }
        public IntervalTree<int, ExpressionNode> StackDefs { get; }
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
        this.memNode = factory.Mem(start);
        Node.AddEdge(start, blocks[proc.EntryBlock].Node);
        Node.AddEdge(blocks[proc.ExitBlock].Node, end);

        var rpo = new DfsIterator<Block>(proc.ControlGraph);
        foreach (var block in rpo.ReversePostOrder())
        {
            var state = blocks[block];
            state = TranslateBlock(block, state);
        }

        PopulateExitUses(proc.ExitBlock, proc.Architecture);
        return start;
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
                if (!ShouldEmitExitUse(value))
                    continue;
                var use = factory.Use(exitState.Node, storage, default);
                Node.AddEdge(value, use);
            }
        }

        var reachingRegisters = exitBlock.Pred
            .SelectMany(pred => blocks[pred].RegisterDefs.Keys)
            .Distinct()
            .OrderBy(reg => reg.Name)
            .ThenBy(reg => reg.Number)
            .ToArray();

        foreach (var reg in reachingRegisters)
        {
            if (!emittedStorages.Add(reg))
                continue;
            var value = ReadStorage(exitBlock, reg, reg.DataType);
            if (!ShouldEmitExitUse(value))
                continue;
            var use = factory.Use(exitState.Node, reg, default);
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
            if (!ShouldEmitExitUse(value))
                continue;
            var use = factory.Use(exitState.Node, flagGroup, default);
            Node.AddEdge(value, use);
        }
    }

    private static bool ShouldEmitExitUse(Node value)
    {
        if (value is DefNode defNode &&
            (defNode.Inputs.Count != 2 ||
             defNode.Inputs[1] is not CallNode))
            return false;
        return true;
    }

    private void LinkBlocks(Procedure proc)
    {
        foreach (var block in proc.ControlGraph.Blocks)
        {
            var from = blocks[block].Node;
            foreach (var succ in block.Succ)
            {
                Node.AddEdge(from, blocks[succ].Node);
            }
        }
    }

    private BlockState TranslateBlock(Block block, BlockState state)
    {
        this.currentBlock = block;
        this.cfNode = state.Node;
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
        WriteStorage(blocks[currentBlock], idDst.Storage, (ExpressionNode) value);
        return value;
    }

    public Node VisitBranch(Branch branch)
    {
        var predicate = branch.Condition.Accept(this);
        IfNode ifNode = factory.If(this.cfNode, predicate);
        Debug.Assert(this.currentBlock is not null);
        var falseBranch = this.blocks[currentBlock].Node;
        var trueBranch = this.blocks[branch.Target].Node;
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
        if (memNode is null)
            throw new InvalidOperationException();
        if (store.Dst is not MemoryAccess access)
            throw new NotImplementedException();
        var ea = access.EffectiveAddress.Accept(this);
        var value = store.Src.Accept(this);
        var storeNode = factory.Store(cfNode, memNode, access.DataType, ea, value);
        memNode = storeNode;
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
                Node.AddEdge(switchNode, targetState.Node);
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

    private ExpressionNode ResolveCanonical(ExpressionNode node)
    {
        ExpressionNode canonical = node;
        while (replacements.TryGetValue(canonical, out var replacement))
        {
            if (replacement is null)
                throw new InvalidOperationException();
            canonical = (ExpressionNode) replacement;
        }

        if (!ReferenceEquals(canonical, node))
        {
            replacements[node] = canonical;
        }
        return canonical;
    }

    private void ReplaceNode(ExpressionNode original, ExpressionNode substitute)
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
    private ExpressionNode ReadStorage(Block block, Storage storage, DataType dt)
    {
        var work = new Stack<ReadStorageFrame>();
        work.Push(new ReadStorageFrame(ReadStoragePhase.Resolve, block, null, 0));

        ExpressionNode? lastResult = null;
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
                    break;

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

                var phi = factory.Phi(dt, state.Node);
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

    private ExpressionNode CreateDefNode(BlockState state, Storage storage, DataType dt)
    {
        var defNode = factory.Def(state.Node, storage, dt);
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

    private void ReplaceCoveredDefsWithSlices(BlockState state, SequenceStorage sequence, ExpressionNode value)
    {
        foreach (var element in sequence.Elements)
        {
            var offset = sequence.OffsetOf(element);
            Debug.Assert(offset >= 0);

            if (element is RegisterStorage reg)
            {
                var slice = factory.Slice(reg.DataType, value, offset);
                slice.Storage = reg;
                if (state.RegisterDefs.TryGetValue(reg, out var existingRegDefs))
                {
                    foreach (var (_, existingRegDef) in existingRegDefs)
                    {
                        ReplaceNode(existingRegDef, slice);
                    }
                }
                state.RegisterDefs[reg] = [(reg.GetBitRange(), slice)];
            }
            else if (element is SequenceStorage)
            {
                Debug.Fail("Can't have a nestedSequenceStorage.");
            }
        }
    }

    private void TrackSequenceCoveredDefs(BlockState state, SequenceStorage sequence, ExpressionNode value)
    {
        var seqBitRange = sequence.GetBitRange();
        foreach (var reg in EnumerateSequenceRegisters(sequence))
        {
            state.RegisterDefs[reg] = [(seqBitRange, value)];
        }
    }

    private static IEnumerable<RegisterStorage> EnumerateSequenceRegisters(SequenceStorage sequence)
    {
        foreach (var element in EnumerateSequenceLeafStorages(sequence))
        {
            if (element is RegisterStorage reg)
                yield return reg;
        }
    }

    private ExpressionNode? ReadLocalStorage(Storage storage, BlockState state, in ReadStorageFrame frame)
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
            if (state.RegisterDefs.TryGetValue(regUse, out var defs) && defs.Count > 0)
            {
                var (bitRange, regValue) = defs[^1];
                regValue = ResolveCanonical(regValue);
                if (!bitRange.IsEmpty && bitRange.Extent > regUse.GetBitRange().Extent)
                {
                    if (regValue.Storage is SequenceStorage seqStorage)
                    {
                        var offset = seqStorage.OffsetOf(regUse);
                        if (offset >= 0)
                        {
                            var slice = factory.Slice(regUse.DataType, regValue, offset);
                            slice.Storage = regUse;
                            state.RegisterDefs[regUse] = [(regUse.GetBitRange(), slice)];
                            return slice;
                        }
                    }
                }
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
        default: throw new NotImplementedException(storage.GetType().Name);
        }
        return null;

    }

    private static ExpressionNode? GetTrivialPhiReplacement(PhiNode phi)
    {
        ExpressionNode? candidate = null;
        foreach (var input in phi.Inputs.Skip(1).Cast<ExpressionNode>())
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

    private ExpressionNode? TryReadFlagGroupStorage(Block block, FlagGroupStorage storage)
    {
        var state = blocks[block];
        if (!state.FlagGroupDefs.TryGetValue(storage.FlagRegister, out var defs) || defs.Count == 0)
            return null;

        var requestedMask = storage.FlagGroupBits;
        for (int i = defs.Count - 1; i >= 0; --i)
        {
            var (candidateStorage, candidateNode) = defs[i];
            if (candidateStorage.FlagGroupBits == requestedMask)
                return ResolveCanonical(candidateNode);

            if (!candidateStorage.Covers(storage))
                continue;

            candidateNode = ResolveCanonical(candidateNode);
            var andNode = factory.Bin(storage.DataType, Operator.And, null, candidateNode, factory.Word32((uint) requestedMask));
            andNode.Storage = storage;
            WriteStorage(state, storage, andNode);
            return andNode;
        }
        return null;
    }

    private void WriteStorage(BlockState state, Storage stgDst, ExpressionNode value)
    {
        value = ResolveCanonical(value);
        switch (stgDst)
        {
        case RegisterStorage regDst:
            if (!state.RegisterDefs.TryGetValue(regDst, out var defs))
            {
                defs = [];
                state.RegisterDefs[regDst] = defs;
            }
            defs.Add((regDst.GetBitRange(), value));
            state.RegisterDefs[regDst] = defs;
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
                TrackSequenceCoveredDefs(state, seq, value);
            }
            break;
        case TemporaryStorage tmp:
            state.TemporaryDefs[tmp] = value;
            break;
        case StackStorage stk:
            state.StackDefs.Add(CreateBitInterval(stk.StackOffset, value.DataType), value);
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
        if (memNode is null)
            throw new InvalidOperationException();
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
        return factory.ProcedureConstant(pc.Procedure);
    }

    public Node VisitScopeResolution(ScopeResolution scopeResolution)
    {
        Console.Out.WriteLine("NYI: {0}", scopeResolution.GetType());
        throw new NotImplementedException();
    }

    public Node VisitSegmentedAddress(SegmentedPointer address)
    {
        Console.Out.WriteLine("NYI: {0}", address.GetType());
        throw new NotImplementedException();
    }

    public Node VisitSlice(Slice slice)
    {
        var input = slice.Expression.Accept(this);
        return factory.Slice(slice.DataType, input, slice.Offset);
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
