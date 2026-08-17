using Reko.Core.Expressions;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class TestNode : Node
{
    public TestNode(int number, DataType dt, ConditionCode conditionCode, Node? cfNode, Node input)
        : base(number, dt, cfNode, input)
    {
        this.ConditionCode = conditionCode;
    }

    public ConditionCode ConditionCode { get; }

    public override string Label => "Test";

    public Node Expression => Inputs[1]!;

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
        sw.Write(" = TEST(");
        sw.Write(this.ConditionCode);
        sw.Write(", ");
        this.Inputs[1]!.RenderReference(sw);
        sw.Write(')');
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitTestNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitTestNode(this, context);
}
