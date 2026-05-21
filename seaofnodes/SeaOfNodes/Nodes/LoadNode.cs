using System.Diagnostics;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class LoadNode : ExpressionNode
{
    public LoadNode(
        int number,
        Node ctrlNode,
        Node memNode,
        DataType dt,
        Node ea) : base(number, dt, ctrlNode, memNode, ea)
    {
    }

    public override string Label => "Load";

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
        sw.Write(" = ");
        Debug.Assert(Inputs.Count == 3);
        sw.Write($"Mem{Inputs[1]!.Number}[");
        var ea = Inputs[2];
        if (ea is null)
            throw new InvalidOperationException();
        ea.RenderReference(sw);
        sw.Write($":{DataType}]");
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitLoadNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitLoadNode(this, context);
}