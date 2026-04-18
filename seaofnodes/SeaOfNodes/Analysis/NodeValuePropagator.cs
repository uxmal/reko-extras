using Reko.Core.Collections;
using Reko.Core.Operators;
using Reko.Extras.SeaOfNodes.Nodes;

namespace Reko.Extras.SeaOfNodes.Analysis;

public class NodeValuePropagator : INodeVisitor<Node?>
{
    private readonly NodeFactory m;

    public NodeValuePropagator(NodeFactory factory)
    {
        this.m = factory;
    }
    
    public StartNode Transform(StartNode start)
    {
        var workList = new WorkList<Node>();

        var reachable = new HashSet<Node>();
        var stack = new Stack<Node>();
        stack.Push(start);
        while (stack.TryPop(out var node))
        {
            if (!reachable.Add(node))
                continue;
            foreach (var output in node.Outputs)
            {
                stack.Push(output);
            }
        }

        foreach (var node in reachable)
        {
            workList.Add(node);
        }

        while (workList.TryGetWorkItem(out var oldNode))
        {
            var newNode = oldNode.Accept(this);
            if (newNode is null)
                continue;
            newNode.Storage = oldNode.Storage;
            Node.Replace(oldNode, newNode);
            foreach (var output in newNode.Outputs)
            {
                workList.Add(output);
            }
        }

        return start;
    }

    public Node? VisitAddressNode(AddressNode n) => null;

    public Node? VisitApplicationNode(ApplicationNode n) => null;

    public Node? VisitBlockNode(BlockNode n) => null;

    public Node? VisitCallNode(CallNode n) => null;

    public Node? VisitCondNode(CondNode n) => null;

    public Node? VisitConstantNode(ConstantNode n) => null;

    public Node? VisitConversionNode(ConversionNode n) => null;

    public Node? VisitDefNode(DefNode n) => null;

    public Node? VisitEndNode(EndNode n) => null;

    public Node? VisitIfNode(IfNode n) => null;

    public Node? VisitLoadNode(LoadNode n) => null;

    public Node? VisitMemoryNode(MemoryNode n) => null;

    public Node? VisitOperationNode(OperationNode n)
    {
        if (n.Inputs.Count == 3
            && n.Inputs[1] is OperationNode oLeft
            && n.Inputs[2] is ConstantNode cRight)
        {
            if (oLeft.Operator.Type == OperatorType.IAdd
                && oLeft.Inputs.Count == 3
                && oLeft.Inputs[1] is ExpressionNode nLeftLeft
                && oLeft.Inputs[2] is ConstantNode cLeftRight)
            if (n.Operator.Type == OperatorType.IAdd)
            {
                var cNew = n.Operator.ApplyConstants(n.DataType, cLeftRight.Value, cRight.Value);
                return m.IAdd(nLeftLeft, m.Const(cNew));
            }
        }
        return null;
    }

    public Node? VisitPhiNode(PhiNode n) => null;

    public Node? VisitProcedureConstantNode(ProcedureConstantNode n) => null;

    public Node? VisitReturnNode(ReturnNode n) => null;

    public Node? VisitSeqNode(SeqNode n) => null;

    public Node? VisitSideEffectNode(SideEffectNode n) => null;

    public Node? VisitSliceNode(SliceNode n) => null;

    public Node? VisitStartNode(StartNode n) => null;

    public Node? VisitStoreNode(StoreNode n) => null;

    public Node? VisitStringNode(StringNode n) => null;

    public Node? VisitSwitchNode(SwitchNode n) => null;

    public Node? VisitTestNode(TestNode n) => null;

    public Node? VisitUseNode(UseNode n) => null;
}
