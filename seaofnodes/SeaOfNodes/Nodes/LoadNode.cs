using System.Diagnostics;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class LoadNode : Node
{
    public LoadNode(
        int number,
        Node ctrlNode,
        Node memNode,
        DataType dt,
        Node ea) : base(number, ctrlNode, memNode, ea)
    {
        this.DataType = dt;
    }

    public DataType DataType { get; }

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
        sw.Write(" = ");
        Debug.Assert(Inputs.Count == 3);
        sw.Write($"Mem{base.Number}[");
        var ea = Inputs[2];
        Debug.Assert(ea is not null);
        ea.RenderReference(sw);
        sw.Write($":{DataType}]");
    }
}