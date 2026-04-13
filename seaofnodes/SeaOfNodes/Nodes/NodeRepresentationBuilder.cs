using System.Diagnostics;
using System.Linq;
using Reko.Core;
using Reko.Core.Code;
using Reko.Core.Expressions;
using Reko.Core.Lib;
using Reko.ImageLoaders.OdbgScript;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class NodeRepresentationBuilder
    : InstructionVisitor<Node>
    , ExpressionVisitor<Node>
{
    private readonly NodeFactory factory;
    private Dictionary<Block, BlockState> blocks;
    private Node? cfNode;

    public NodeRepresentationBuilder()
    {
        this.factory = new NodeFactory();
        this.blocks = [];
    }

    private record struct BlockState(
        BlockNode Node,
        Dictionary<Storage, List<(BitRange, Node)>> StorageDefs)
    {
    }

    public StartNode Select(Procedure proc)
    {
        StartNode start = factory.CreateStartNode(proc);
        EndNode end = factory.CreateEndNode(start);
        CreateEmptyBlocks(proc);
        LinkBlocks(proc);
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
            var node = factory.CreateBlockNode(block);
            blocks[block] = new BlockState(node, []);
        }
        return blocks;
    }

    public Node VisitAssignment(Assignment ass)
    {
        throw new NotImplementedException();
    }

    public Node VisitBranch(Branch branch)
    {
        throw new NotImplementedException();
    }

    public Node VisitCallInstruction(CallInstruction call)
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
        if (ret.Expression is not null)
            throw new NotImplementedException();
            Debug.Assert(cfNode is not null);
            return factory.CreateReturnNode(cfNode);
    }

    public Node VisitSideEffect(SideEffect side)
    {
        throw new NotImplementedException();
    }

    public Node VisitStore(Store store)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }

    public Node VisitMemberPointerSelector(MemberPointerSelector mps)
    {
        throw new NotImplementedException();
    }

    public Node VisitMemoryAccess(MemoryAccess access)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}