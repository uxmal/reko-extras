
using Reko.Core.Expressions;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class SliceNode : ExpressionNode
{
    public SliceNode(int number, DataType dt, Node input, Node offset)
        : base(number, dt, input, offset)
    {
    }

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
        sw.Write(" = Slice(");
        this.Inputs[1]!.RenderReference(sw);
        sw.Write(", ");
        sw.Write(this.DataType);
        this.Inputs[2]!.RenderReference(sw);
        sw.Write(')');
    }
}