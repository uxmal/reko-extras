using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class CondNode : ExpressionNode
{
    public CondNode(int number, DataType dt, Node? cfNode, Node input)
        : base(number, dt, cfNode, input)
    {
    }

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
        sw.Write(" = cond(");
        this.Inputs[1]!.RenderReference(sw);
        sw.Write(')');
    }
}