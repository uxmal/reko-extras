namespace Reko.Extras.SeaOfNodes.Nodes;

public interface INodeVisitor<T>
{
    T VisitAddressNode(AddressNode node);
    T VisitApplicationNode(ApplicationNode node);
    T VisitBinaryNode(BinaryNode node);
    T VisitBlockNode(BlockNode node);
    T VisitCallNode(CallNode node);
    T VisitCondNode(CondNode node);
    T VisitConstantNode(ConstantNode node);
    T VisitConversionNode(ConversionNode node);
    T VisitDefNode(DefNode node);
    T VisitEndNode(EndNode node);
    T VisitIfNode(IfNode node);
    T VisitLoadNode(LoadNode node);
    T VisitMemoryNode(MemoryNode node);
    T VisitOutArgumentNode(OutArgumentNode outArgumentNode);
    T VisitPhiNode(PhiNode node);
    T VisitProcedureConstantNode(ProcedureConstantNode node);
    T VisitReturnNode(ReturnNode node);
    T VisitSeqNode(SeqNode node);
    T VisitSideEffectNode(SideEffectNode node);
    T VisitSliceNode(SliceNode node);
    T VisitStartNode(StartNode node);
    T VisitStoreNode(StoreNode node);
    T VisitStringNode(StringNode node);
    T VisitSwitchNode(SwitchNode node);
    T VisitTestNode(TestNode node);
    T VisitUnaryNode(UnaryNode node);
    T VisitUseNode(UseNode node);
}

public interface INodeVisitor<T, C>
{
    T VisitAddressNode(AddressNode node, C context);
    T VisitApplicationNode(ApplicationNode node, C context);
    T VisitBinaryNode(BinaryNode node, C context);
    T VisitBlockNode(BlockNode node, C context);
    T VisitCallNode(CallNode node, C context);
    T VisitCondNode(CondNode node, C context);
    T VisitConstantNode(ConstantNode node, C context);
    T VisitConversionNode(ConversionNode node, C context);
    T VisitDefNode(DefNode node, C context);
    T VisitEndNode(EndNode node, C context);
    T VisitIfNode(IfNode node, C context);
    T VisitLoadNode(LoadNode node, C context);
    T VisitMemoryNode(MemoryNode node, C context);
    T VisitOutArgumentNode(OutArgumentNode outArgumentNode, C? context);
    T VisitPhiNode(PhiNode node, C context);
    T VisitProcedureConstantNode(ProcedureConstantNode node, C context);
    T VisitReturnNode(ReturnNode node, C context);
    T VisitSeqNode(SeqNode node, C context);
    T VisitSideEffectNode(SideEffectNode node, C context);
    T VisitSliceNode(SliceNode node, C context);
    T VisitStartNode(StartNode node, C context);
    T VisitStoreNode(StoreNode node, C context);
    T VisitStringNode(StringNode node, C context);
    T VisitSwitchNode(SwitchNode node, C context);
    T VisitTestNode(TestNode node, C context);
    T VisitUnaryNode(UnaryNode node, C context);
    T VisitUseNode(UseNode node, C context);
}