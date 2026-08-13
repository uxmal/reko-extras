using Reko.Core.Types;
using System.Diagnostics;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class PhiNode : Node
{
    public PhiNode(int number, DataType dt, Node cfNode, params Node[] inputs)
        : base(number, dt, cfNode, inputs)
    {
    }

    public override string Label => "Phi";

    public override void Render(TextWriter sw)
    {
        Debug.Assert(Inputs.Count >= 2);
        this.RenderReference(sw);
        sw.Write(" = PHI(");
        string sep = "";
        for (int i = 1; i < Inputs.Count; i++)
        {
            var input = Inputs[i];
            if (input is null)
                throw new InvalidOperationException();
            sw.Write(sep);
            input.RenderReference(sw);
            sep = ", ";
        }
        sw.Write(")");
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitPhiNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitPhiNode(this, context);
}
