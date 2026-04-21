using System.Diagnostics;
using Reko.Core;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class ApplicationNode : ExpressionNode
{
    public ApplicationNode(int number, DataType dt, Node? cfNode, Node fn, params Node?[] inputs) : base(number, dt, cfNode, fn, inputs)
    {
    }

    public ApplicationNode(int number, DataType dt, params Node?[] inputs) : base(number, dt, inputs)
    {
    }

    public override string Label => "Apply";

    public override void Render(TextWriter sw)
    {
        Debug.Assert(Inputs.Count >= 2);
        var callee = Inputs[1];
        if (Outputs.Count != 0 &&
            (Outputs.Count != 1 || Outputs[0] is not SideEffectNode))
        {
            this.RenderReference(sw);
            sw.Write(" = ");
        }
        Debug.Assert(callee is not null);
        callee.RenderReference(sw);
        sw.Write('(');
        string sep = "";
        for (int i = 2; i < Inputs.Count; i++)
        {
            var arg = Inputs[i];
            Debug.Assert(arg is not null);
            sw.Write(sep);
            arg.RenderReference(sw);
            sep = ", ";
        }
        sw.Write(')');
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitApplicationNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitApplicationNode(this, context);
}