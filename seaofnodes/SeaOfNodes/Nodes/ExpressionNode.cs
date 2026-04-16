using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public abstract class ExpressionNode : Node
{
    protected ExpressionNode(int number, DataType dt, params Node?[] inputs) : base(number, inputs)
    {
        DataType = dt;
        InputOffset = 0;
    }

    protected ExpressionNode(int number, DataType dt, Node? cfNode, Node n, params Node?[] inputs) : base(number, cfNode, n, inputs)
    {
        DataType = dt;
        InputOffset = cfNode is not null ? 1 : 0;
    }

    public DataType DataType { get; }

    /// <summary>
    /// Index of the first data input. Is 1 when a cfNode is present as Inputs[0], 0 otherwise.
    /// </summary>
    public int InputOffset { get; }

    /// <inheritdoc />
    public override bool IsFloating => InputOffset == 0;
}