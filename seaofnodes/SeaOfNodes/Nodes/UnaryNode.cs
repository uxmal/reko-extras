using Reko.Core.Operators;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class UnaryNode : Node
{
    private static readonly Dictionary<OperatorType, string> operatorName = new()
    {
        { OperatorType.Not, "!" },
        { OperatorType.Neg, "-" },
        { OperatorType.Comp, "~" },
        { OperatorType.AddrOf, "&" },
    };
    public UnaryNode(int number, DataType dt, UnaryOperator op, Node? cfNode, Node expr)
        : base(number, dt, cfNode, expr)
    {
        this.Operator = op;
    }

    public UnaryOperator Operator { get; }

    public Node Expression => Inputs[1]!;

    public override string Label => operatorName[this.Operator.Type].Trim();

    public override T Accept<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitUnaryNode(this);
    }

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
    {
        return visitor.VisitUnaryNode(this, context);
    }

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
        sw.Write(" = ");
        string opName = operatorName[this.Operator.Type];
        sw.Write(opName);
        Expression.RenderReference(sw);
    }
}
