using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public abstract class ExpressionNode : Node
{
    protected ExpressionNode(int number, DataType dt, params Node?[] inputs) : base(number, inputs)
    {
        DataType = dt;
    }

    protected ExpressionNode(int number, DataType dt, Node? cfNode, Node n, params Node?[] inputs) : base(number, cfNode, n, inputs)
    {
        DataType = dt;
    }

    public DataType DataType { get; }
}