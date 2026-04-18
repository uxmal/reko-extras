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

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitStringNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitStringNode(this, context);
}