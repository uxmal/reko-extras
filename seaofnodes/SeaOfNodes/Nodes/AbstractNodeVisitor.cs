using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class AbstractNodeVisitor : INodeVisitor
{
    private void VisitInputs(Node node)
    {
        foreach (var input in node.Inputs)
        {
            input?.Accept(this);
        }
    }

    public virtual void VisitAddressNode(AddressNode node)
    {
    }

    public virtual void VisitApplicationNode(ApplicationNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitBinaryNode(BinaryNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitBlockNode(BlockNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitCallNode(CallNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitCastNode(CastNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitCondNode(CondNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitConstantNode(ConstantNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitConversionNode(ConversionNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitDefNode(DefNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitDereferenceNode(DereferenceNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitEndNode(EndNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitIfNode(IfNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitLoadNode(LoadNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitMemberPointerSelectorNode(MemberPointerSelectorNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitMemoryNode(MemoryNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitOutArgumentNode(OutArgumentNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitPhiNode(PhiNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitProcedureConstantNode(ProcedureConstantNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitReturnNode(ReturnNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitSegmentedPointerNode(SegmentedPointerNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitSeqNode(SeqNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitSideEffectNode(SideEffectNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitSliceNode(SliceNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitStartNode(ProcedureNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitStoreNode(StoreNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitStringNode(StringNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitSwitchNode(SwitchNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitTestNode(TestNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitUnaryNode(UnaryNode node)
    {
        VisitInputs(node);
    }

    public virtual void VisitUseNode(UseNode node)
    {
        VisitInputs(node);
    }
}
