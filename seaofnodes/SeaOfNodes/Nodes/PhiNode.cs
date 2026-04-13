using System.Diagnostics;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class PhiNode : Node
{
    public PhiNode(int number, params Node?[] inputs) : base(number, inputs)
    {
    }

    public override void Render(TextWriter sw)
    {
        Debug.Assert(Inputs.Count >= 2);
        this.RenderReference(sw);
        sw.Write(" = PHI(");
        string sep = "";
        for (int i = 1; i < Inputs.Count; i++)
        {
            var input = Inputs[i];
            Debug.Assert(input is not null);
            sw.Write(sep);
            input.RenderReference(sw);
            sep = ", ";
        }
        sw.Write(")");
    }
}