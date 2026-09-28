using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class CastNode : Node
{
    public CastNode(int number, DataType dt, CfNode? cfNode, Node expression)
        : base(number, dt, cfNode, expression)
    { 
    }

    public override string Label => "cast";

    public Node Expression => Inputs[1]!;

    public override void Accept(INodeVisitor visitor)
    {
        visitor.VisitCastNode(this);
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitCastNode(this);
    }

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
    {
        return visitor.VisitCastNode(this, context);
    }

    public override void Render(TextWriter sw)
    {
        throw new NotImplementedException();
    }
}
