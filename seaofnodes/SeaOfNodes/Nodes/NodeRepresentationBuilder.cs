using System.Diagnostics;
using System.Linq;
using Reko.Analysis;
using Reko.Core;
using Reko.Core.Code;
using Reko.Core.Expressions;
using Reko.Core.Lib;
using Reko.Core.Types;
using Reko.ImageLoaders.OdbgScript;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class NodeRepresentationBuilder
    : InstructionVisitor<Node>
    , ExpressionVisitor<Node>
{
    private readonly NodeFactory factory;
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
        this.blocks = [];
        this.sccProcs = [];
    }

    private record struct BlockState(
        BlockNode Node,
        Dictionary<Storage, List<(BitRange, Node)>> StorageDefs)
    {
    }

    public StartNode Select(Procedure proc)
    {
        procedureHadTranslationError = false;
        StartNode start = factory.CreateStartNode(proc);
        EndNode end = factory.CreateEndNode(start);
        entryBlock = proc.EntryBlock;
        CreateEmptyBlocks(proc);
        LinkBlocks(proc);
        this.memNode = factory.CreateMemoryNode(start);
        Node.AddEdge(start, blocks[proc.EntryBlock].Node);
        Node.AddEdge(blocks[proc.ExitBlock].Node, end);

        var rpo = new Reko.Core.Graphs.DfsIterator<Block>(proc.ControlGraph);
        foreach (var block in rpo.ReversePostOrder())
        {
            var state = blocks[block];
            state = TranslateBlock(block, state);
        }
        return start;
    }

    public bool ProcedureHadTranslationError => procedureHadTranslationError;

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
            try
            {
                stmt.Instruction.Accept(this);
            }
            catch
            {
                Console.Out.WriteLine($"Error: {stmt.Instruction} in block {block}");
                procedureHadTranslationError = true;
            }
        }
        return state;
    }

    private Dictionary<Block, BlockState> CreateEmptyBlocks(Procedure proc)
    {
        foreach (var block in proc.ControlGraph.Blocks)
        {
            var node = factory.CreateBlockNode(block);
            blocks[block] = new BlockState(node, []);
        }
        return blocks;
    }

    public Node VisitAssignment(Assignment ass)
    {
        Debug.Assert(currentBlock is not null);
        if (ass.Dst is not Identifier idDst)
            throw new NotImplementedException();

        var value = ass.Src.Accept(this);
        WriteIdentifier(currentBlock, idDst.Storage, value);
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
        if (call.Callee is ProcedureConstant pc &&
            pc.Procedure is Procedure proc &&
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

    private CallNode GenerateUseDefsForKnownCallee(CallInstruction call, Node callee, Procedure proc, ProcedureFlow calleeFlow)
    {
        var callNode = factory.Call(this.cfNode, callee);
        foreach (var (stgUse, bitRange) in calleeFlow.BitsUsed)
        {
            var value = ReadIdentifier(this.currentBlock!, stgUse, stgUse.Name, stgUse.DataType);
            if (stgUse is RegisterStorage reg)
            {
                Debug.Assert(this.cfNode is not null);
                var useNode = factory.CreateUse(this.cfNode, reg, bitRange);
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
                var defNode = factory.CreateDefNode(this.cfNode, reg, null, reg.DataType);
                Node.AddEdge(callNode, defNode);
                WriteIdentifier(this.currentBlock!, stgDef, defNode);
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
        throw new NotImplementedException();
    }

    public Node VisitDefInstruction(DefInstruction def)
    {
        throw new NotImplementedException();
    }

    public Node VisitGotoInstruction(GotoInstruction gotoInstruction)
    {
        throw new NotImplementedException();
    }

    public Node VisitPhiAssignment(PhiAssignment phi)
    {
        throw new NotImplementedException();
    }

    public Node VisitReturnInstruction(ReturnInstruction ret)
    {
        Debug.Assert(cfNode is not null);
        if (ret.Expression is null)
            return factory.CreateReturnNode(cfNode);

        var value = ret.Expression.Accept(this);
        return factory.CreateReturnNode(cfNode, value);
    }

    public Node VisitSideEffect(SideEffect side)
    {
        throw new NotImplementedException();
    }

    public Node VisitStore(Store store)
    {
        Debug.Assert(cfNode is not null);
        Debug.Assert(memNode is not null);
        if (store.Dst is not MemoryAccess access)
            throw new NotImplementedException();
        var ea = access.EffectiveAddress.Accept(this);
        var value = store.Src.Accept(this);
        var storeNode = factory.CreateStore(cfNode, memNode, access.DataType, ea, value);
        memNode = storeNode;
        return storeNode;
    }

    public Node VisitSwitchInstruction(SwitchInstruction si)
    {
        throw new NotImplementedException();
    }

    public Node VisitUseInstruction(UseInstruction use)
    {
        throw new NotImplementedException();
    }

    public Node VisitAddress(Address addr)
    {
        throw new NotImplementedException();
    }

    public Node VisitApplication(Application appl)
    {
        throw new NotImplementedException();
    }

    public Node VisitArrayAccess(ArrayAccess acc)
    {
        throw new NotImplementedException();
    }

    public Node VisitBinaryExpression(BinaryExpression binExp)
    {
        var left = binExp.Left.Accept(this);
        var right = binExp.Right.Accept(this);
        return factory.Bin(binExp.DataType, binExp.Operator, cfNode, left, right);
    }

    public Node VisitCast(Cast cast)
    {
        throw new NotImplementedException();
    }

    public Node VisitConditionalExpression(ConditionalExpression cond)
    {
        throw new NotImplementedException();
    }

    public Node VisitConditionOf(ConditionOf cof)
    {
        throw new NotImplementedException();
    }

    public Node VisitConstant(Constant c)
    {
        return factory.Const(c);
    }

    public Node VisitConversion(Conversion conversion)
    {
        throw new NotImplementedException();
    }

    public Node VisitDereference(Dereference deref)
    {
        throw new NotImplementedException();
    }

    public Node VisitFieldAccess(FieldAccess acc)
    {
        throw new NotImplementedException();
    }

    public Node VisitIdentifier(Identifier id)
    {
        Debug.Assert(currentBlock is not null);
        return ReadIdentifier(currentBlock, id.Storage, id.Name, id.DataType);
    }

    private Node ReadIdentifier(Block block, Storage storage, string name, DataType dt)
    {
        var state = blocks[block];
        if (state.StorageDefs.TryGetValue(storage, out var defs) && defs.Count > 0)
        {
            return defs[^1].Item2;
        }

        if (block == entryBlock)
        {
            //$TODO: nameFromStorage.
            var defNode = factory.CreateDefNode(state.Node, storage, name, dt);
            state.StorageDefs[storage] =
            [
                (default, defNode)
            ];
            return defNode;
        }

        if (block.Pred.Count == 0)
            throw new InvalidOperationException("Unable to resolve storage definition due to missing predecessors.");

        if (block.Pred.Count == 1)
            return ReadIdentifier(block.Pred[0], storage, name, dt);

        var phi = factory.CreatePhi(state.Node);
        state.StorageDefs[storage] =
        [
            (default, phi)
        ];

        foreach (var pred in block.Pred)
        {
            var arg = ReadIdentifier(pred, storage, name, dt);
            Node.AddEdge(arg, phi);
        }

        var sameNode = GetTrivialPhiReplacement(phi);
        if (sameNode is not null)
        {
            state.StorageDefs[storage] = [(default, sameNode)];
            Node.Replace(phi, sameNode);
            return sameNode;
        }
        return phi;
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

    private void WriteIdentifier(Block currentBlock, Storage stgDst, Node value)
    {
        var state = blocks[currentBlock];
        if (!state.StorageDefs.TryGetValue(stgDst, out var defs))
        {
            defs = [];
            state.StorageDefs[stgDst] = defs;
        }
        defs.Add((default, value));
    }


    public Node VisitMemberPointerSelector(MemberPointerSelector mps)
    {
        throw new NotImplementedException();
    }

    public Node VisitMemoryAccess(MemoryAccess access)
    {
        Debug.Assert(cfNode is not null);
        Debug.Assert(memNode is not null);
        var ea = access.EffectiveAddress.Accept(this);
        return factory.CreateLoad(cfNode, memNode, access.DataType, ea);
    }

    public Node VisitMkSequence(MkSequence seq)
    {
        throw new NotImplementedException();
    }

    public Node VisitOutArgument(OutArgument outArgument)
    {
        throw new NotImplementedException();
    }

    public Node VisitPhiFunction(PhiFunction phi)
    {
        throw new NotImplementedException();
    }

    public Node VisitPointerAddition(PointerAddition pa)
    {
        throw new NotImplementedException();
    }

    public Node VisitProcedureConstant(ProcedureConstant pc)
    {
        return factory.ProcedureConstant(pc.Procedure);
    }

    public Node VisitScopeResolution(ScopeResolution scopeResolution)
    {
        throw new NotImplementedException();
    }

    public Node VisitSegmentedAddress(SegmentedPointer address)
    {
        throw new NotImplementedException();
    }

    public Node VisitSlice(Slice slice)
    {
        throw new NotImplementedException();
    }

    public Node VisitStringConstant(StringConstant str)
    {
        throw new NotImplementedException();
    }

    public Node VisitTestCondition(TestCondition tc)
    {
        throw new NotImplementedException();
    }

    public Node VisitUnaryExpression(UnaryExpression unary)
    {
        var operand = unary.Expression.Accept(this);
        return factory.CreateUnary(unary.DataType, unary.Operator, cfNode, operand);
    }
}