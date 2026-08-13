
using Reko.Core.Expressions;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class SliceNode : Node
{
    public SliceNode(int number, DataType dt, Node? cfNode, Node input, int offset)
        : base(number, dt, cfNode, input)
    {
        this.Offset = offset;
    }

    public override string Label => "Slice";

    public Node Expression => Inputs[1]!;
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

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitSliceNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitSliceNode(this, context);
}