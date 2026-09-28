using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class MemberPointerSelectorNode : Node
{
    public MemberPointerSelectorNode(int number, DataType type, CfNode? cfNode, Node basePointer, Node memberPointer)
        : base(number, type, cfNode, basePointer, memberPointer)
    {
    }

    public Node BasePointer => Inputs[1]!;
    public Node MemberPointer => Inputs[2]!;
    public override string Label => "MemberPointerSelector";

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
        sw.Write(" = ");
        BasePointer.RenderReference(sw);
        sw.Write(".*");
        MemberPointer.RenderReference(sw);
    }

    public override void Accept(INodeVisitor visitor)
        => visitor.VisitMemberPointerSelectorNode(this);
    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitMemberPointerSelectorNode(this);
    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitMemberPointerSelectorNode(this, context);
}
