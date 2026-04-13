using System.Diagnostics;
using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Lib;
using Reko.Core.Operators;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class NodeFactory
{
    private int number;

    public NodeFactory()
    {
        this.number = 0;
    }

    private int NextId() => ++number;

    public OperationNode Bin(DataType dt, Operator op, Node? cfNode, Node left, Node right)
    {
        return new OperationNode(
            NextId(),
            dt,
            op,
            cfNode, left, right);
    }

    public OperationNode CreateUnary(DataType dt, Operator op, Node? cfNode, Node operand)
    {
        return new OperationNode(NextId(), dt, op, cfNode, operand);
    }

    public LoadNode CreateLoad(Node cfNode, Node memNode, DataType dt, Node ea)
    {
        return new LoadNode(NextId(), cfNode, memNode, dt, ea);
    }

    public ConstantNode Word32(uint value) => new ConstantNode(
        NextId(),
        Constant.Word32(value));

    public ConstantNode Const(Constant value) => new ConstantNode(
        NextId(),
        value);

    public AddressNode CreateAddress(Address addr) => new AddressNode(
        NextId(),
        addr);

    public StartNode CreateStartNode(Procedure proc)
    {
        var node = new StartNode(NextId());
        return node;
    }

    public BlockNode CreateBlockNode(Block block)
    {
        var node = new BlockNode(NextId(), block, []);
        return node;
    }

    public EndNode CreateEndNode(StartNode start)
    {
        var node = new EndNode(NextId());
        return node;
    }

    public Node CreateReturnNode(Node cfNode)
    {
        var node = new ReturnNode(NextId(), cfNode);
        return node;
    }

    public Node CreateReturnNode(Node cfNode, Node value)
    {
        var node = new ReturnNode(NextId(), cfNode, value);
        return node;
    }


    public DefNode CreateDefNode(Node cfNode, Storage storage, string? name, DataType dt)
    {
        var node = new DefNode(NextId(), storage, dt, name, cfNode);
        return node;
    }


    public DefNode CreateDefNode(Node cfNode, Storage storage, DataType dt)
    {
        var node = new DefNode(NextId(), storage, dt, null, cfNode);
        return node;
    }

    public MemoryNode CreateMemoryNode(Node cfNode)
    {
        return new MemoryNode(NextId(), cfNode);
    }

    public StoreNode CreateStore(Node cfNode, MemoryNode memNode, DataType dt, Node ea, Node value)
    {
        return new StoreNode(NextId(), cfNode, memNode, dt, ea, value);
    }

    public PhiNode CreatePhi(Node cfNode)
    {
        return new PhiNode(NextId(), cfNode);
    }

    public IfNode If(Node? cfNode, Node predicate)
    {
        return new IfNode(NextId(), cfNode, predicate);
    }

    public Node CreateUse(Node? cfNode, Storage stg, BitRange bitRange)
    {
        return new UseNode(NextId(), stg, bitRange, cfNode);
    }

    public ProcedureConstantNode ProcedureConstant(ProcedureBase procedure)
    {
        return new ProcedureConstantNode(NextId(), procedure);
    }

    public CallNode Call(Node? cfNode, Node callee)
    {
        return new CallNode(NextId(), cfNode, callee);
    }
}
