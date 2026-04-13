
using Reko.Core.Expressions;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class SliceNode : ExpressionNode
{
    public SliceNode(int number, DataType dt, Node? cfNode, Node input, int offset)
        : base(number, dt, cfNode, input)
    {
        this.Offset = offset;
    }

    public int Offset { get; }

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
        sw.Write(" = SLICE(");
        this.Inputs[1]!.RenderReference(sw);
        sw.Write(", ");
        sw.Write(this.DataType);
        sw.Write(", ");
        sw.Write(this.Offset);
        sw.Write(')');
    }
}