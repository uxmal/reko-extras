using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class OutArgumentNode : Node
{
    public OutArgumentNode(int nodeId, DataType dt) 
        : base(nodeId, dt, [null])
    { 
    }

    public override string Label => "Out";

    public override T Accept<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitOutArgumentNode(this);
    }

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
    {
        return visitor.VisitOutArgumentNode(this, context);
    }

    public override void Render(TextWriter w)
    {
        w.Write("out ");
        base.RenderReference(w);
    }
}
