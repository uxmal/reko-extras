using Reko.Core.Expressions;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class StringNode : ExpressionNode
{
    public StringNode(int number, StringConstant c) : base(number, c.DataType)
    {
        this.Value = c;
    }

    public StringConstant Value { get; }

    public override void RenderReference(TextWriter sw)
    {
        sw.Write(this.Value);
    }

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
    }
}