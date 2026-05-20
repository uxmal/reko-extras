using Reko.Core;
using Reko.Core.Types;
using System.Text;

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

    protected ExpressionNode(int number, DataType dt, Node? cfNode, params Node?[] inputs) 
        : base(number, cfNode, inputs)
    {
        DataType = dt;
    }

    public DataType DataType { get; }

    public override string ToString()
    {
        var sw = new StringWriter();
        this.Render(sw);
        return sw.ToString();
    }
}
