using System.ComponentModel.DataAnnotations;
using Reko.Core.Expressions;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class ConstantNode : ExpressionNode
{
    public ConstantNode(int number, Constant value) : base(number, value.DataType, [null])
    {
        this.Value = value;
    }

    public Constant Value { get; }

    public override void RenderReference(TextWriter sw)
    {
        sw.Write(Value.ToString());
    }

    public override void Render(TextWriter sw)
    {
        sw.Write(Value.ToString());
    }
}