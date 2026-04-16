using Reko.Analysis;
using Reko.Core;
using Reko.Core.Code;
using Reko.Core.Expressions;
using Reko.Core.Graphs;
using Reko.Core.Lib;
using Reko.Core.Operators;
using Reko.Core.Types;
using System.Diagnostics;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class NodeRepresentationBuilder
    : InstructionVisitor<Node>
    , ExpressionVisitor<Node>
{
    private readonly NodeFactory factory;
    private readonly NodeApplicationBuilder applicationBuilder;
    private readonly ProgramDataFlow programFlow;
    private readonly Dictionary<Block, BlockState> blocks;
    private readonly HashSet<Procedure> sccProcs;
    private bool procedureHadTranslationError;
    private Node? cfNode;
    private Block? currentBlock;
    private Block? entryBlock;
    private MemoryNode? memNode;


    public NodeRepresentationBuilder(ProgramDataFlow programFlow)
    {
        this.programFlow = programFlow;
        this.factory = new NodeFactory();
        this.applicationBuilder = new NodeApplicationBuilder(this.factory);
        this.blocks = [];
        this.sccProcs = [];
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
            this.TemporaryDefs = [];
        }

        public BlockNode Node { get; }

        public Dictionary<RegisterStorage, List<(BitRange, Node)>> RegisterDefs { get; }
        public Dictionary<RegisterStorage, List<(FlagGroupStorage, Node)>> FlagGroupDefs { get; }
        public Dictionary<TemporaryStorage, Node> TemporaryDefs { get; }
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
        procedureHadTranslationError = false;
        StartNode start = factory.Start(proc);
        EndNode end = factory.End(start);
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
        return start;
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
        value.Name = GenerateName(idDst.Storage, value);
        WriteStorage(blocks[currentBlock], idDst.Storage, value);
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

    public Node VisitCallInstruction(CallInstruction call)
    {
        var callee = call.Callee.Accept(this);
        var pc = call.Callee as ProcedureConstant;
        if (pc is not null && pc.Signature.ParametersValid)
        {
            return GenerateApplicationFromCall(pc, callee);
        }
        if (pc?.Procedure is Procedure proc &&
            programFlow.ProcedureFlows.TryGetValue(proc, out var calleeFlow) &&
            !sccProcs.Contains(proc))
        {
            // If the callee is a procedure constant and it's not part of the
            // current recursion group, we should know what storages are live
            // in and trashed.
            return GenerateUseDefsForKnownCallee(call, callee, proc, calleeFlow);
        }
        else
        {
            return GenerateUseDefsForUnknownCallee(call);
        }
    }

    private Node GenerateApplicationFromCall(ProcedureConstant callee, Node calleeNode)
    {
        Node a = factory.Apply(VoidType.Instance, this.cfNode, calleeNode);
        Node s = factory.SideEffect(this.cfNode!, a);
        cfNode = s;
        return s;
    }

    private CallNode GenerateUseDefsForKnownCallee(CallInstruction call, Node callee, Procedure proc, ProcedureFlow calleeFlow)
    {
        var callNode = factory.Call(this.cfNode, callee);
        foreach (var (stgUse, bitRange) in calleeFlow.BitsUsed)
        {
            var value = ReadStorage(this.currentBlock!, stgUse, stgUse.DataType);
            if (stgUse is RegisterStorage reg)
            {
                Debug.Assert(this.cfNode is not null);
                var useNode = factory.Use(this.cfNode, reg, bitRange);
                Node.AddEdge(value, useNode);
                Node.AddEdge(useNode, callNode);
            }
            else 
                throw new NotImplementedException();
        }
        foreach (var stgDef in calleeFlow.Trashed)
        {
            if (stgDef is RegisterStorage reg)
            {
                Debug.Assert(this.cfNode is not null);
                var defNode = factory.Def(this.cfNode, reg, reg.DataType);
                Node.AddEdge(callNode, defNode);
                WriteStorage(blocks[this.currentBlock!], stgDef, defNode);
            }
            else
                throw new NotImplementedException();
        }
        return callNode;
    }

    private Node GenerateUseDefsForUnknownCallee(CallInstruction call)
    {
        throw new NotImplementedException();
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
        Debug.Assert(cfNode is not null);
        if (ret.Expression is null)
            return factory.Return(cfNode);

        var value = ret.Expression.Accept(this);
        return factory.Return(cfNode, value);
    }

    public Node VisitSideEffect(SideEffect side)
    {
        var expNode = side.Expression.Accept(this);
        Debug.Assert(cfNode is not null);
        return factory.SideEffect(cfNode, expNode);
    }

    public Node VisitStore(Store store)
    {
        Debug.Assert(cfNode is not null);
        Debug.Assert(memNode is not null);
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

                var phi = factory.Phi(state.Node);
                phi.Name = GenerateName(storage, phi);
                WriteStorage(state, storage, phi);

                work.Push(new(ReadStoragePhase.AfterPhiPredecessor, frame.Block, phi, 0));
                work.Push(new(ReadStoragePhase.Resolve, frame.Block.Pred[0], null, 0));
                break;
            }

            case ReadStoragePhase.AfterSinglePredecessor:
                Debug.Assert(lastResult is not null);
                WriteStorage(blocks[frame.Block], storage, lastResult);
                break;

            case ReadStoragePhase.AfterPhiPredecessor:
            {
                Debug.Assert(frame.Phi is not null);
                Debug.Assert(lastResult is not null);
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
                    Node.Replace(frame.Phi, sameNode);
                    lastResult = sameNode;
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

        Debug.Assert(lastResult is not null);
        return lastResult;
    }

    private Node? CreateDefNode(BlockState state, Storage storage, DataType dt)
    {
        var defNode = factory.Def(state.Node, storage, dt);
        defNode.Name = storage.Name;
        WriteStorage(state, storage, defNode);
        return defNode;
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
            if (state.RegisterDefs.TryGetValue(regUse, out var defs) && defs.Count > 0)
            {
                return defs[^1].Item2;
            }
            break;
        case TemporaryStorage temp:
            if (state.TemporaryDefs.TryGetValue(temp, out var tempNode))
            {
                return tempNode;
            }
            break;
        default: throw new NotImplementedException(storage.GetType().Name);
        }
        return null;

    }

    private static Node? GetTrivialPhiReplacement(PhiNode phi)
    {
        Node? candidate = null;
        foreach (var input in phi.Inputs.Skip(1))
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
        for (int i = defs.Count - 1; i >= 0; --i)
        {
            var (candidateStorage, candidateNode) = defs[i];
            if (candidateStorage.FlagGroupBits == requestedMask)
                return candidateNode;

            if (!candidateStorage.Covers(storage))
                continue;

            var andNode = factory.Bin(storage.DataType, Operator.And, null, candidateNode, factory.Word32((uint) requestedMask));
            andNode.Name = GenerateName(storage, andNode);
            WriteStorage(state, storage, andNode);
            return andNode;
        }
        return null;
    }

    private void WriteStorage(BlockState state, Storage stgDst, Node value)
    {
        switch (stgDst)
        {
        case RegisterStorage regDst:
            if (!state.RegisterDefs.TryGetValue(regDst, out var defs))
            {
                defs = [];
                state.RegisterDefs[regDst] = defs;
            }
            defs.Add((default, value));
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
        case TemporaryStorage tmp:
            state.TemporaryDefs[tmp] = value;
            break;
        default:  
            throw new NotImplementedException(stgDst.GetType().Name);
        }
    }


    public Node VisitMemberPointerSelector(MemberPointerSelector mps)
    {
        Console.Out.WriteLine("NYI: {0}", mps.GetType());
        throw new NotImplementedException();
    }

    public Node VisitMemoryAccess(MemoryAccess access)
    {
        Debug.Assert(cfNode is not null);
        Debug.Assert(memNode is not null);
        var ea = access.EffectiveAddress.Accept(this);
        return factory.Load(cfNode, memNode, access.DataType, ea);
    }

    public Node VisitMkSequence(MkSequence seq)
    {
        Console.Out.WriteLine("NYI: {0}", seq.GetType());
        throw new NotImplementedException();
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
        return factory.Slice(null, slice.DataType, input, slice.Offset);
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

    private string? GenerateName(Storage storage, Node value)
    {
        return $"{storage.Name}_{value.Number}";
    }
}
