using System.Diagnostics;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class StoreNode : MemoryNode
{
    public StoreNode(
        int number,
        Node ctrlNode,
        Node memNode,
        DataType dt,
         Node ea,
         Node value) : base(number,  ctrlNode, memNode, ea, value)
    {
        this.DataType = dt;
    }

    public DataType DataType { get; }

    public override void Render(TextWriter sw)
    {
        sw.Write($"Mem{base.Number}[");
        var mem = Inputs[1];
        Debug.Assert(mem is not null);
        var ea = Inputs[2];
        Debug.Assert(ea is not null);
        ea.RenderReference(sw);
           sw.Write($":{DataType}] = ");
           var value = Inputs[3];
           Debug.Assert(value is not null);
           value.RenderReference(sw);
    }
}