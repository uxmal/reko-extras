using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class SegmentedPointerNode : Node
{
    public SegmentedPointerNode(int id, DataType dataType, CfNode? cf, Node segment, Node offset)
        : base(id, dataType, cf, segment, offset)
    {
    }

    public Node Segment => Inputs[1]!;
    public Node Offset => Inputs[2]!;

    public override string Label => "SegPtr";

    public override T Accept<T>(INodeVisitor<T> visitor)
    {
        throw new NotImplementedException();
    }

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
    {
        throw new NotImplementedException();
    }

    public override void Render(TextWriter writer)
    {
        this.RenderReference(writer);
        writer.Write(" = ");
        Segment.RenderReference(writer);
        writer.Write(':');
        Offset.RenderReference(writer);
    }
}
