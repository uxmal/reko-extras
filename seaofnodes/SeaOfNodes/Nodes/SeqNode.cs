using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class SeqNode : Node
{
    public SeqNode(int number, DataType dt, params Node?[] inputs)
        : base(number, dt, inputs)
    {
    }

    public SeqNode(int number, DataType dt, Node? cfNode, params Node?[] inputs) 
        : base(number, dt, cfNode, inputs)
    {
        foreach (var input in inputs)
        {
            if (input is null)
                throw new InvalidOperationException();
        }
    }

    public override string Label => "Seq";

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
        sw.Write(" = SEQ(");
        string sep = "";
        for (int i = 0; i < this.Inputs.Count; ++i)
        {
            var input = this.Inputs[i];
            if (input is null)
            {
                if (i == 0)
                    continue;
                throw new InvalidOperationException();
            }
            sw.Write(sep);
            sep = ", ";
            input.RenderReference(sw);
        }
        sw.Write(')');
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitSeqNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitSeqNode(this, context);

    public string InputsAsString
        => string.Join(',', Inputs.Skip(1));
}
