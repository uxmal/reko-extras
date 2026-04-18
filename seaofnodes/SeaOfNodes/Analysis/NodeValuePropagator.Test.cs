using System.Diagnostics;
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
        var conds = FindReachingDefinitions(ccNode, n => null, n => n as CondNode);
        foreach (CondNode cond in conds)
        {
            if (AsISub(cond.Inputs[1]) is OperationNode sub)
            {
                if (!cCodesToOperators.TryGetValue(n.ConditionCode, out var ccOp))
                    throw new NotImplementedException($"Condition code {n.ConditionCode} is not supported.");
                return m.Bin(PrimitiveType.Bool, ccOp, null, sub.Inputs[1]!, sub.Inputs[2]!);
            }
            throw new NotImplementedException($"Condition node {cond} is not supported.");
        }
        return null;
    }

}