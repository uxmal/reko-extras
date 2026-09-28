using System;
using System.Linq;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class AbstractVisitor2 : INodeVisitor<Node?>
{
    private readonly NodeFactory m;

    public AbstractVisitor2(NodeFactory factory)
    {
        this.m = factory;
    }

    private (bool changed, Node?[] newInputs) VisitAndCollectInputs(Node node)
    {
        var count = node.Inputs.Count;
        var newInputs = new Node?[count];
        bool changed = false;
        for (int i = 0; i < count; ++i)
        {
            var inp = node.Inputs[i];
            if (inp is null)
            {
                newInputs[i] = null;
                continue;
            }
            var r = inp.Accept(this);
            if (r is null)
            {
                newInputs[i] = inp;
            }
            else
            {
                newInputs[i] = r;
                changed = true;
            }
        }
        return (changed, newInputs);
    }

    // Default DoVisit methods - can be overridden by derived classes.
    protected virtual Node? DoVisitAddressNode(AddressNode node) => null;
    protected virtual Node? DoVisitApplicationNode(ApplicationNode node) => null;
    protected virtual Node? DoVisitBinaryNode(BinaryNode node) => null;
    protected virtual Node? DoVisitBlockNode(BlockNode node) => null;
    protected virtual Node? DoVisitCallNode(CallNode node) => null;
    protected virtual Node? DoVisitCastNode(CastNode node) => null;
    protected virtual Node? DoVisitCondNode(CondNode node) => null;
    protected virtual Node? DoVisitConstantNode(ConstantNode node) => null;
    protected virtual Node? DoVisitConversionNode(ConversionNode node) => null;
    protected virtual Node? DoVisitDefNode(DefNode node) => null;
    protected virtual Node? DoVisitDereferenceNode(DereferenceNode node) => null;
    protected virtual Node? DoVisitEndNode(EndNode node) => null;
    protected virtual Node? DoVisitIfNode(IfNode node) => null;
    protected virtual Node? DoVisitLoadNode(LoadNode node) => null;
    protected virtual Node? DoVisitMemberPointerSelectorNode(MemberPointerSelectorNode node) => null;
    protected virtual Node? DoVisitMemoryNode(MemoryNode node) => null;
    protected virtual Node? DoVisitOutArgumentNode(OutArgumentNode node) => null;
    protected virtual Node? DoVisitPhiNode(PhiNode node) => null;
    protected virtual Node? DoVisitProcedureConstantNode(ProcedureConstantNode node) => null;
    protected virtual Node? DoVisitReturnNode(ReturnNode node) => null;
    protected virtual Node? DoVisitSegmentedPointerNode(SegmentedPointerNode node) => null;
    protected virtual Node? DoVisitSeqNode(SeqNode node) => null;
    protected virtual Node? DoVisitSideEffectNode(SideEffectNode node) => null;
    protected virtual Node? DoVisitSliceNode(SliceNode node) => null;
    protected virtual Node? DoVisitStartNode(ProcedureNode node) => null;
    protected virtual Node? DoVisitStoreNode(StoreNode node) => null;
    protected virtual Node? DoVisitStringNode(StringNode node) => null;
    protected virtual Node? DoVisitSwitchNode(SwitchNode node) => null;
    protected virtual Node? DoVisitTestNode(TestNode node) => null;
    protected virtual Node? DoVisitUnaryNode(UnaryNode node) => null;
    protected virtual Node? DoVisitUseNode(UseNode node) => null;

    public virtual Node? VisitAddressNode(AddressNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        var candidate = changed ? new AddressNode(node.Number, node.Value) : node;
        var after = DoVisitAddressNode((AddressNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitApplicationNode(ApplicationNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate;
        if (changed)
        {
            // inputs: [cf?, procedure, args...]
            if (newInputs.Length >= 2 && newInputs[1] is not null)
                candidate = new ApplicationNode(node.Number, node.DataType, newInputs[0], newInputs[1]!, newInputs.Skip(2).ToArray());
            else
                candidate = new ApplicationNode(node.Number, node.DataType, newInputs);
        }
        else
            candidate = node;
        var after = DoVisitApplicationNode((ApplicationNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitBinaryNode(BinaryNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate;
        if (changed)
            candidate = new BinaryNode(node.Number, node.DataType, node.Operator, newInputs[0], newInputs[1]!, newInputs[2]!);
        else
            candidate = node;
        var after = DoVisitBinaryNode((BinaryNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitBlockNode(BlockNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new BlockNode(node.Number, node.Block, newInputs) : node;
        var after = DoVisitBlockNode((BlockNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitCallNode(CallNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new CallNode(node.Number, newInputs) : node;
        var after = DoVisitCallNode((CallNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitCastNode(CastNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new CastNode(node.Number, node.DataType, (CfNode?)newInputs[0], newInputs[1]!) : node;
        var after = DoVisitCastNode((CastNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitCondNode(CondNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new CondNode(node.Number, node.DataType, newInputs[0], newInputs[1]!) : node;
        var after = DoVisitCondNode((CondNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitConstantNode(ConstantNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        var candidate = node; // constant has no replaceable inputs
        var after = DoVisitConstantNode(node);
        if (after is not null) return after;
        return null;
    }

    public virtual Node? VisitConversionNode(ConversionNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new ConversionNode(node.Number, node.DataType, node.SourceDataType, newInputs[0], newInputs[1]!) : node;
        var after = DoVisitConversionNode((ConversionNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitDefNode(DefNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new DefNode(node.Number, node.Storage, node.DataType, newInputs) : node;
        var after = DoVisitDefNode((DefNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitDereferenceNode(DereferenceNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new DereferenceNode(node.Number, node.DataType, (CfNode?)newInputs[0], newInputs[1]!) : node;
        var after = DoVisitDereferenceNode((DereferenceNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitEndNode(EndNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new EndNode(node.Number) : node;
        var after = DoVisitEndNode((EndNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitIfNode(IfNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new IfNode(node.Number, newInputs) : node;
        var after = DoVisitIfNode((IfNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitLoadNode(LoadNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new LoadNode(node.Number, newInputs[0]!, newInputs[1]!, node.DataType, newInputs[2]!) : node;
        var after = DoVisitLoadNode((LoadNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitMemberPointerSelectorNode(MemberPointerSelectorNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new MemberPointerSelectorNode(node.Number, node.DataType, (CfNode?)newInputs[0], newInputs[1]!, newInputs[2]!) : node;
        var after = DoVisitMemberPointerSelectorNode((MemberPointerSelectorNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitMemoryNode(MemoryNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new MemoryNode(node.Number, node.DataType, (CfNode?)newInputs[0]) : node;
        var after = DoVisitMemoryNode((MemoryNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitOutArgumentNode(OutArgumentNode node)
    {
        var after = DoVisitOutArgumentNode(node);
        if (after is not null) return after;
        return null;
    }

    public virtual Node? VisitPhiNode(PhiNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new PhiNode(node.Number, node.DataType, newInputs[0]!, newInputs.Skip(1).Where(n => n is not null).Select(n => n!).ToArray()) : node;
        var after = DoVisitPhiNode((PhiNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitProcedureConstantNode(ProcedureConstantNode node)
    {
        var after = DoVisitProcedureConstantNode(node);
        if (after is not null) return after;
        return null;
    }

    public virtual Node? VisitReturnNode(ReturnNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new ReturnNode(node.Number, newInputs) : node;
        var after = DoVisitReturnNode((ReturnNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitSegmentedPointerNode(SegmentedPointerNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new SegmentedPointerNode(node.Number, node.DataType, (CfNode?)newInputs[0], newInputs[1]!, newInputs[2]!) : node;
        var after = DoVisitSegmentedPointerNode((SegmentedPointerNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitSeqNode(SeqNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new SeqNode(node.Number, node.DataType, newInputs) : node;
        var after = DoVisitSeqNode((SeqNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitSideEffectNode(SideEffectNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new SideEffectNode(node.Number, newInputs[0]!, newInputs[1]!) : node;
        var after = DoVisitSideEffectNode((SideEffectNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitSliceNode(SliceNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new SliceNode(node.Number, node.DataType, newInputs[0], newInputs[1]!, node.Offset) : node;
        var after = DoVisitSliceNode((SliceNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitStartNode(ProcedureNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new ProcedureNode(node.Number, node.Procedure, newInputs) : node;
        var after = DoVisitStartNode((ProcedureNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitStoreNode(StoreNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        // constructor: StoreNode(number, ctrlNode, memNode, dt, ea, value)
        Node candidate = changed ? new StoreNode(node.Number, newInputs[0]!, newInputs[1]!, node.DataType, newInputs[2]!, newInputs[3]!) : node;
        var after = DoVisitStoreNode((StoreNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitStringNode(StringNode node)
    {
        var after = DoVisitStringNode(node);
        if (after is not null) return after;
        return null;
    }

    public virtual Node? VisitSwitchNode(SwitchNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        throw new NotImplementedException();
        //Node candidate = changed ? m.Switch(node.Inputs[0], newInputs[1]!, newInputs.Skip(2).Where(n => n is not null).Select(n => n!).ToArray()) : node;
        //var after = DoVisitSwitchNode((SwitchNode)candidate);
        //if (after is not null) return after;
        //return changed ? candidate : null;
    }

    public virtual Node? VisitTestNode(TestNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? m.Test(node.DataType, node.ConditionCode, newInputs[0], newInputs[1]!) : node;
        var after = DoVisitTestNode((TestNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitUnaryNode(UnaryNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new UnaryNode(node.Number, node.DataType, node.Operator, (CfNode?)newInputs[0], newInputs[1]!) : node;
        var after = DoVisitUnaryNode((UnaryNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }

    public virtual Node? VisitUseNode(UseNode node)
    {
        var (changed, newInputs) = VisitAndCollectInputs(node);
        Node candidate = changed ? new UseNode(node.Number, node.Storage, node.BitRange, newInputs[0]) : node;
        var after = DoVisitUseNode((UseNode)candidate);
        if (after is not null) return after;
        return changed ? candidate : null;
    }
}
