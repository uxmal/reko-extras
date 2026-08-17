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
        Node value) : base(number, dt, ctrlNode, memNode, ea, value)
    {
    }

    public override void Render(TextWriter sw)
    {
        sw.Write($"Mem{base.Number}[");
        var mem = Inputs[1];
        if (mem is null)
            throw new InvalidOperationException();
        var ea = Inputs[2];
        if (ea is null)
            throw new InvalidOperationException();
        ea.RenderReference(sw);
           sw.Write($":{DataType}] = ");
           var value = Inputs[3];
           if (value is null)
               throw new InvalidOperationException();
           value.RenderReference(sw);
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitStoreNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitStoreNode(this, context);
}