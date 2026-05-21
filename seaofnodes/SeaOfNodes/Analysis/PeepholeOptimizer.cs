using Reko.Core.Expressions;
using Reko.Core.Operators;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Nodes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.SeaOfNodes.Analysis;

public partial class PeepholeOptimizer
{
    private readonly NodeFactory m;

    public PeepholeOptimizer(NodeFactory factory)
    {
        this.m = factory;
    }

    public ExpressionNode Bin(DataType dt, Operator op, Node? cfNode, Node left, Node right)
    {
        var cLeft = left as ConstantNode;
        var cRight = right as ConstantNode;
        if (cLeft is not null && cRight is not null)
        {
            var c = op.ApplyConstants(dt, cLeft.Value, cRight.Value);
            return m.Const(c);
        }
        if (IsSymmetric(op) && cLeft is not null)
        {
            var t = cLeft;
            left = right;
            cRight = cLeft;
            right = cLeft;
            cLeft = null;
        }
        switch (op.Type)
        {
        case OperatorType.IAdd:
            if (cRight is not null)
            {
                if (left is OperationNode inner)
                {
                    if (inner.Operator.Type.IsAddOrSub())
                    {
                        if (inner.Inputs[2] is ConstantNode cInnerRight)
                        {
                            cRight = m.Const(inner.Operator.ApplyConstants(dt, cRight.Value, cInnerRight.Value));
                            right = cRight;
                            left = inner.Inputs[1]!;
                        }
                    }
                }
                if (cRight.Value.IsZero)
                    return (ExpressionNode)left;
                if (cRight.Value.IsNegative)
                    cRight = m.Const(cRight.Value);
                right = cRight;
            }
            break;
        case OperatorType.ISub:
            if (left == right)
                return m.Const(Constant.Zero(dt));
            if (cRight is not null)
            {
                if (left is OperationNode inner)
                {
                    if (inner.Operator.Type.IsAddOrSub())
                    {
                        if (inner.Inputs[2] is ConstantNode cInnerRight)
                        {
                            var opInv = inner.Operator.Type == OperatorType.IAdd
                                ? Operator.ISub
                                : Operator.IAdd;
                            cRight = m.Const(opInv.ApplyConstants(dt, cRight.Value, cInnerRight.Value));
                            right = cRight;
                            left = inner.Inputs[1]!;
                        }
                    }
                }
                if (cRight.Value.IsZero)
                    return (ExpressionNode)left;
            }
            break;
        case OperatorType.Xor:
            if (left == right)
                return m.Const(Constant.Zero(dt));
            break;
        }
        return m.Bin(dt, op, cfNode, left, right);
    }

    public ExpressionNode ISub(ExpressionNode left, ExpressionNode right)
    {
        return Bin(left.DataType, Operator.ISub, null, left, right);
    }

    internal CondNode Cond(DataType dataType, Node? value, Node exp)
    {
        return m.Cond(dataType, value, exp);
    }

    internal ConstantNode Const(Constant constant)
    {
        return m.Const(constant);
    }

    internal Node Load(Node cfNode, Node memNode, DataType dt, Node ea)
    {
        return m.Load(cfNode, memNode, dt, ea);
    }

    private bool IsSymmetric(Operator op)
    {
        return op.Type switch
        {
            OperatorType.IAdd => true,
            OperatorType.IMul => true,
            OperatorType.SMul => true,
            OperatorType.UMul => true,
            OperatorType.And => true,
            OperatorType.Or => true,
            OperatorType.Xor => true,
            _ => false
        };
    }
}
