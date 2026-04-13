using System.Diagnostics;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class SeqNode : ExpressionNode
{
    public SeqNode(int number, DataType dt, params Node?[] inputs) : base(number, dt, inputs)
    {
    }

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
        sw.Write(" = SEQ(");
        string sep = "";
        foreach (var input in this.Inputs.Skip(1))
        {
            sw.Write(sep);
            sep = ", ";
            Debug.Assert(input is not null);
            input.RenderReference(sw);
        }
        sw.Write(')');
    }
}