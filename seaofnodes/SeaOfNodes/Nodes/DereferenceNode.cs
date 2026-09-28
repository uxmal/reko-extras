using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class DereferenceNode : Node
{
    public DereferenceNode(int number, DataType dataType, CfNode? cfNode, Node pointer)
        : base(number, dataType, cfNode, pointer)
    {
    }

    public override string Label => throw new NotImplementedException();

    public override void Accept(INodeVisitor visitor)
    {
        visitor.VisitDereferenceNode(this);
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
    {
        return visitor.VisitDereferenceNode(this);
    }

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
    {
        return visitor.VisitDereferenceNode(this, context);
    }

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
        sw.Write(" = *");
        Inputs[1]!.RenderReference(sw);
    }
}
