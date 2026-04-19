using System.Diagnostics;
using System.Linq;
using Reko.Core.Collections;
using Reko.Core.Expressions;
using Reko.Core.Operators;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Nodes;

namespace Reko.Extras.SeaOfNodes.Analysis;

public partial class NodeValuePropagator
{
    private static readonly Dictionary<ConditionCode, Operator> cCodesToOperators = new Dictionary<ConditionCode, Operator>
    {
        { ConditionCode.EQ, Operator.Eq },
        { ConditionCode.NE, Operator.Ne },
        { ConditionCode.LT, Operator.Lt },
        { ConditionCode.LE, Operator.Le },
        { ConditionCode.GT, Operator.Gt },
        { ConditionCode.GE, Operator.Ge },
        { ConditionCode.ULT, Operator.Ult },
        { ConditionCode.ULE, Operator.Ule },
        { ConditionCode.UGT, Operator.Ugt },
        { ConditionCode.UGE, Operator.Uge },
    };

    public Node? VisitTestNode(TestNode n)
    {
        var ccNode = n.Inputs[1];
        Debug.Assert(ccNode is not null);
        return RewriteTestFromCcNode(n, ccNode);
    }

    private Node? RewriteTestFromCcNode(TestNode testNode, Node ccNode)
    {
        var active = new HashSet<Node>();
        var stack = new Stack<RewriteFrame>();
        stack.Push(new RewriteFrame(ccNode));

        Node? lastResult = null;
        while (stack.Count > 0)
        {
            var frame = stack.Peek();
            switch (frame.Stage)
            {
            case RewriteStage.Enter:
                if (!active.Add(frame.Node))
                {
                    frame.Result = null;
                    frame.Stage = RewriteStage.Done;
                    break;
                }

                if (frame.Node is PhiNode phi)
                {
                    if (phi.Inputs.Count < 2)
                    {
                        frame.Result = null;
                        frame.Stage = RewriteStage.Done;
                        break;
                    }
                    var cfNode = phi.Inputs[0];
                    if (cfNode is null)
                    {
                        frame.Result = null;
                        frame.Stage = RewriteStage.Done;
                        break;
                    }

                    frame.Phi = phi;
                    frame.PushedPhi = m.Phi(cfNode);
                    frame.ChildIndex = 1;
                    frame.Stage = RewriteStage.ProcessPhi;
                    break;
                }

                if (frame.Node is CondNode directCond)
                {
                    frame.Result = RewriteCondToComparison(testNode, directCond);
                    frame.Stage = RewriteStage.Done;
                    break;
                }

                var reachingConds = FindReachingCondDefinitions(frame.Node);
                frame.Result = reachingConds.Count != 1
                    ? null
                    : RewriteCondToComparison(testNode, reachingConds.Single());
                frame.Stage = RewriteStage.Done;
                break;

            case RewriteStage.ProcessPhi:
                Debug.Assert(frame.Phi is not null);
                if (frame.ChildIndex >= frame.Phi.Inputs.Count)
                {
                    frame.Result = frame.PushedPhi;
                    frame.Stage = RewriteStage.Done;
                    break;
                }

                var phiInput = frame.Phi.Inputs[frame.ChildIndex];
                if (phiInput is null)
                {
                    frame.Result = null;
                    frame.Stage = RewriteStage.Done;
                    break;
                }

                frame.Stage = RewriteStage.AwaitChild;
                stack.Push(new RewriteFrame(phiInput));
                break;

            case RewriteStage.AwaitChild:
                if (lastResult is null)
                {
                    frame.Result = null;
                    frame.Stage = RewriteStage.Done;
                    break;
                }

                Debug.Assert(frame.PushedPhi is not null);
                Node.AddEdge(lastResult, frame.PushedPhi);
                ++frame.ChildIndex;
                frame.Stage = RewriteStage.ProcessPhi;
                break;

            case RewriteStage.Done:
                lastResult = frame.Result;
                active.Remove(frame.Node);
                stack.Pop();
                break;

            default:
                throw new InvalidOperationException($"Unexpected rewrite stage {frame.Stage}.");
            }
        }

        return lastResult;
    }

    private enum RewriteStage
    {
        Enter,
        ProcessPhi,
        AwaitChild,
        Done,
    }

    private sealed class RewriteFrame
    {
        public RewriteFrame(Node node)
        {
            Node = node;
            Stage = RewriteStage.Enter;
        }

        public Node Node { get; }
        public RewriteStage Stage { get; set; }
        public PhiNode? Phi { get; set; }
        public PhiNode? PushedPhi { get; set; }
        public int ChildIndex { get; set; }
        public Node? Result { get; set; }
    }

    private HashSet<CondNode> FindReachingCondDefinitions(Node start)
    {
        var result = new HashSet<CondNode>();
        var visited = new HashSet<Node>();
        var wl = new WorkList<Node>();
        wl.Add(start);
        while (wl.TryGetWorkItem(out var node))
        {
            if (!visited.Add(node))
                continue;

            if (node is CondNode cond)
            {
                result.Add(cond);
                continue;
            }

            var startIndex = GetDataInputStartIndex(node);
            for (int i = startIndex; i < node.Inputs.Count; ++i)
            {
                var input = node.Inputs[i];
                if (input is not null)
                    wl.Add(input);
            }
        }
        return result;
    }

    private static int GetDataInputStartIndex(Node node)
    {
        if (node.Inputs.Count == 0)
            return 0;
        return node.Inputs[0] is CfNode or BlockNode or StartNode ? 1 : 0;
    }

    private Node RewriteCondToComparison(TestNode testNode, CondNode cond)
    {
        if (AsISub(cond.Inputs[1]) is not OperationNode sub)
            throw new NotImplementedException($"Condition node {cond} is not supported.");

        if (!cCodesToOperators.TryGetValue(testNode.ConditionCode, out var ccOp))
            throw new NotImplementedException($"Condition code {testNode.ConditionCode} is not supported.");

        return m.Bin(PrimitiveType.Bool, ccOp, null, sub.Inputs[1]!, sub.Inputs[2]!);
    }

}