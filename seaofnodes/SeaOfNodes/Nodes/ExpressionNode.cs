using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

/// <summary>
/// Models an expression with a data type. 
/// </summary>
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

    /// <inheritdoc />
    public override bool IsFloating => Inputs.Count < 1 || Inputs[0] is null;
}