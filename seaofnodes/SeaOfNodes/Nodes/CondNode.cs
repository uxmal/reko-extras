using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class CondNode : Node
{
    public CondNode(int number, DataType dt, Node? cfNode, Node input)
        : base(number, dt, cfNode, input)
    {
    }

    public override string Label => "Cond";

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
        sw.Write(" = cond(");
        this.Inputs[1]!.RenderReference(sw);
        sw.Write(')');
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitCondNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitCondNode(this, context);
}