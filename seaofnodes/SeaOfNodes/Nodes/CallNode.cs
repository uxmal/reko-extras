using System.Diagnostics;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class CallNode : CfNode
{
    public CallNode(int number, params Node?[] inputs)
        : base(number, inputs)
    {
    }

    public override string Label => "Call";

    public override void Render(TextWriter sw)
    {
        sw.Write($"call ");
        this.Inputs[1]!.RenderReference(sw);
        sw.WriteLine();
        sw.Write("        uses:");
        foreach (var use in this.Inputs.Skip(2))
        {
            sw.Write(" ");
            if (use is null)
                throw new InvalidOperationException();
            if (use is UseNode useNode)
            {
                sw.Write(useNode.Storage);
                sw.Write(':');
                var input = useNode.Inputs[1];
                if (input is null)
                    throw new InvalidOperationException();
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
            if (def is null)
                throw new InvalidOperationException();
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