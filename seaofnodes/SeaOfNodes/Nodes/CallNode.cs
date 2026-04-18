using System.Diagnostics;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class CallNode : CfNode
{
    public CallNode(int number, params Node?[] inputs)
        : base(number, inputs)
    {
    }

    public override void Render(TextWriter sw)
    {
        sw.Write($"call ");
        this.Inputs[1]!.RenderReference(sw);
        sw.WriteLine();
        sw.Write("        uses:");
        foreach (var use in this.Inputs.Skip(2))
        {
            sw.Write(" ");
            Debug.Assert(use is not null);
            if (use is UseNode useNode)
            {
                sw.Write(useNode.Storage);
                sw.Write(':');
                var input = useNode.Inputs[1];
                Debug.Assert(input is not null);
                input.RenderReference(sw);
            }
            else
            {
                use.Render(sw);
            }
        }
        sw.WriteLine();
        sw.Write("        defs:");
        foreach (DefNode def in this.Outputs)
        {
            sw.Write(" ");
            Debug.Assert(def is not null);
            sw.Write(def.Storage);
            sw.Write(':');
            def.RenderReference(sw);
        }
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitCallNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitCallNode(this, context);
}