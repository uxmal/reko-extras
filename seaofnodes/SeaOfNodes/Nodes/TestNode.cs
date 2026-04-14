using Reko.Core.Expressions;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class TestNode : ExpressionNode
{
    public TestNode(int number, DataType dt, ConditionCode conditionCode, Node? cfNode, Node input)
        : base(number, dt, cfNode, input)
    {
        this.ConditionCode = conditionCode;
    }

    public ConditionCode ConditionCode { get; }

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
        sw.Write(" = TEST(");
        sw.Write(this.ConditionCode);
        sw.Write(", ");
        this.Inputs[1]!.RenderReference(sw);
        sw.Write(')');
    }
}
