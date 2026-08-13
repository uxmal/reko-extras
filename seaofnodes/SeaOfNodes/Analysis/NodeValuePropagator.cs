using Reko.Core.Collections;
using Reko.Core.Operators;
using Reko.Extras.SeaOfNodes.Nodes;

namespace Reko.Extras.SeaOfNodes.Analysis;

public partial class NodeValuePropagator : INodeVisitor<Node?>
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
                && oLeft.Inputs[1] is Node nLeftLeft
                && oLeft.Inputs[2] is ConstantNode cLeftRight)
            if (n.Operator.Type == OperatorType.IAdd)
            {
                var cNew = n.Operator.ApplyConstants(n.DataType, cLeftRight.Value, cRight.Value);
                return m.IAdd(nLeftLeft, m.Const(cNew));
            }
        }
        return null;
    }


    public Node? VisitOutArgumentNode(OutArgumentNode outArgumentNode)
    {
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

    private HashSet<T> FindReachingDefinitions<T>(
        Node start,
        Func<Node, Node?> testBypass,
        Func<Node, T?> test)
    {
        var result = new HashSet<T>();
        var visited = new HashSet<Node>();
        var wl = new WorkList<Node>();
        wl.Add(start);
        while (wl.TryGetWorkItem(out var node))
        {
            if (!visited.Add(node))
                continue;
            var bypass = testBypass(node);
            if (bypass is not null)
            {
                wl.Add(bypass);
                continue;
            }
            var val = test(node);
            if (val is not null)
                result.Add(val);
            foreach (var input in node.Inputs)
            {
                if (input is not null)
                    wl.Add(input);
            }
        }
        return result;
    }

    public Node? VisitUseNode(UseNode n) => null;

    private static OperationNode? AsISub(Node? node)
    {
        if (node is OperationNode op && op.Operator.Type == OperatorType.ISub)
            return op;
        return null;
    }
}
