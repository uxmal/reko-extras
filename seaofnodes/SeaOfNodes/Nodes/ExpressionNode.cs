using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public abstract class ExpressionNode : Node
{
    protected ExpressionNode(int number, DataType dt, params Node?[] inputs) : base(number, inputs)
    {
        DataType = dt;
    }

    public DataType DataType { get; }
}