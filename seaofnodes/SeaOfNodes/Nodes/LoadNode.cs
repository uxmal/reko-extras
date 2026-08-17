using System.Diagnostics;
using Reko.Core.Expressions;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

/// <summary>
/// Models a memory load operation. A load node has an <see cref="EffectiveAddress"/> from
/// which data is loaded, and a <see cref="DataType"/> that indicates the size of the data to load.
/// </summary>
public sealed class LoadNode : Node
{
    public LoadNode(
        int number,
        Node ctrlNode,
        Node memNode,
        DataType dt,
        Node ea) : base(number, dt, ctrlNode, memNode, ea)
    {
    }

    public override string Label => "Load";

    public Node MemoryId => Inputs[1]!;

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
        sw.Write(" = ");
        Debug.Assert(Inputs.Count == 3);
        sw.Write($"Mem{MemoryId.Number}[");
        var ea = EffectiveAddress;
        ea.RenderReference(sw);
        sw.Write($":{DataType}]");
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitLoadNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitLoadNode(this, context);

    public Node EffectiveAddress
    {
        get
        {
            var ea = Inputs[2];
            Debug.Assert(ea is not null);
            return ea;
        }
    }
}