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

    public AddressNode Address(Address addr) => new AddressNode(
        NextId(),
        addr);

    public ApplicationNode Apply(DataType dataType, Node? cfNode, Node fn, params Node[] args)
    {
        return new ApplicationNode(NextId(), dataType, cfNode, fn, args);
    }

    public BlockNode Block(Block block)
    {
        var node = new BlockNode(NextId(), block, []);
        return node;
    }

    public CallNode Call(Node? cfNode, Node callee)
    {
        return new CallNode(NextId(), cfNode, callee);
    }

    public ConstantNode Const(Constant value) => new ConstantNode(
        NextId(),
        value);

    public ConversionNode Convert(Node? cfNode, DataType dstType, DataType srcType, Node input)
        => new ConversionNode(NextId(), dstType, srcType, cfNode, input);

    public DefNode Def(Node cfNode, Storage storage, DataType dt)
    {
        var node = new DefNode(NextId(), storage, dt, cfNode);
        return node;
    }

    public EndNode End(StartNode start)
    {
        var node = new EndNode(NextId());
        return node;
    }

    public OperationNode? Eq(Node left, Node right)
    {
        return Bin(PrimitiveType.Bool, Operator.Eq, null, left, right);
    }

    public Node? IAdd(ExpressionNode left, ExpressionNode right)
    {
        return Bin(left.DataType, Operator.IAdd, null, left, right);
    }


    public MemoryNode Mem(Node cfNode)
    {
        return new MemoryNode(NextId(), cfNode);
    }

    public PhiNode Phi(DataType dt, Node? cfNode)
    {
        return new PhiNode(NextId(), dt, cfNode);
    }

    public IfNode If(Node? cfNode, Node predicate)
    {
        return new IfNode(NextId(), cfNode, predicate);
    }

    public LoadNode Load(Node cfNode, Node memNode, DataType dt, Node ea)
    {
        return new LoadNode(NextId(), cfNode, memNode, dt, ea);
    }

    public ExpressionNode OutArg(DataType dt)
    {
        return new OutArgumentNode(NextId(), dt);
    }

    public ProcedureConstantNode ProcedureConstant(ProcedureBase procedure)
    {
        return new ProcedureConstantNode(NextId(), procedure);
    }

    public CondNode Cond(DataType dt, Node? cfNode, Node input)
    {
        return new CondNode(NextId(), dt, cfNode, input);
    }

    public Node Return(Node cfNode)
    {
        var node = new ReturnNode(NextId(), cfNode);
        return node;
    }

    public Node Return(Node cfNode, Node value)
    {
        var node = new ReturnNode(NextId(), cfNode, value);
        return node;
    }

    public Node SideEffect(Node cfNode, Node expNode)
    {
        return new SideEffectNode(NextId(), cfNode, expNode);
    }

    public SeqNode Seq(DataType dt, params Node[] inputs)
    {
        return new SeqNode(NextId(), dt, inputs);
    }

    public SliceNode Slice(DataType dt, Node input, int offset)
        => new SliceNode(NextId(), dt, null, input, offset);

    public StartNode Start(Procedure proc)
    {
        var node = new StartNode(NextId());
        return node;
    }

    public StoreNode Store(Node cfNode, MemoryNode memNode, DataType dt, Node ea, Node value)
    {
        return new StoreNode(NextId(), cfNode, memNode, dt, ea, value);
    }

    public SwitchNode Switch(Node cfNode, Node selector, string[] targets)
    {
        return new SwitchNode(NextId(), cfNode, selector, targets);
    }

    public TestNode Test(DataType dt, ConditionCode conditionCode, Node? cfNode, Node input)
    {
        return new TestNode(NextId(), dt, conditionCode, cfNode, input);
    }

    public OperationNode Unary(DataType dt, Operator op, Node? cfNode, Node operand)
    {
        return new OperationNode(NextId(), dt, op, cfNode, operand);
    }

    public Node Use(Node? cfNode, Storage stg, BitRange bitRange)
    {
        return new UseNode(NextId(), stg, bitRange, cfNode);
    }

    public ConstantNode Word32(uint value) => new ConstantNode(
        NextId(),
        Constant.Word32(value));
}
