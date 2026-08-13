using Reko.Core;
using Reko.Core.Analysis;
using Reko.Core.Expressions;
using Reko.Core.Operators;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Nodes;

namespace Reko.Extras.SeaOfNodes.Analysis;

public partial class PeepholeOptimizer
{
    public Node Bin(DataType dt, Operator op, Node? cfNode, Node left, Node right)
    {
        var cLeft = left as ConstantNode;
        var cRight = right as ConstantNode;
        if (cLeft is not null && cRight is not null)
        {
            var c = op.ApplyConstants(dt, cLeft.Value, cRight.Value);
            return m.Const(c);
        }

        // Prefer constants as the right operand for commutative operations.
        if (IsSymmetric(op) && cLeft is not null)
        {
            left = right;
            cRight = cLeft;
            right = cLeft;
        }
        switch (op.Type)
        {
        case OperatorType.IAdd:
            if (cRight is not null)
            {
                if (left is BinaryNode inner)
                {
                    if (inner.Operator.Type.IsAddOrSub())
                    {
                        if (inner.Right is ConstantNode cInnerRight)
                        {
                            // (+ (+/- x C1) C@) => (+/- x (C1 + C2))
                            cRight = m.Const(inner.Operator.ApplyConstants(dt, cRight.Value, cInnerRight.Value));
                            left = inner.Left;
                        }
                    }
                }
                // (+ x 0) => x
                if (cRight.Value.IsZero)
                    return left;
                // (+ x -C) => (- x C)
                if (cRight.Value.IsNegative)
                {
                    cRight = m.Const(cRight.Value.Negate());
                    op = Operator.ISub;
                }
                right = cRight;
            }
            break;
        case OperatorType.ISub:
            // (- x x) => 0
            if (left == right)
                return m.Const(Constant.Zero(dt));
            if (cRight is not null)
            {
                if (left is BinaryNode inner)
                {
                    if (inner.Operator.Type.IsAddOrSub())
                    {
                        if (inner.Right is ConstantNode cInnerRight)
                        {
                            var opInv = inner.Operator.Type == OperatorType.IAdd
                                ? Operator.ISub
                                : Operator.IAdd;
                            cRight = m.Const(opInv.ApplyConstants(dt, cRight.Value, cInnerRight.Value));
                            right = cRight;
                            left = inner.Left;
                        }
                    }
                }
                if (cRight.Value.IsZero)
                    return left;
                if (cRight.Value.IsNegative)
                {
                    cRight = m.Const(cRight.Value.Negate());
                    op = Operator.IAdd;
                }
                right = cRight;
            }
            break;
        case OperatorType.And:
            // x & x => x
            if (left == right)
                return left;
            if (cRight is not null)
            {
                // X & 0 => 0
                if (cRight.Value.IsZero)
                    return m.Const(Constant.Zero(dt));
                // X & MAX => X
                if (cRight.Value.IsMaxUnsigned)
                    return left;
            }
            break;
        case OperatorType.Or:
            if (left == right)
                return left;
            if (cRight is not null)
            {
                if (cRight.Value.IsZero)
                    return left;
            }
            break;
        case OperatorType.Xor:
            if (left == right)
                return m.Zero(dt);
            if (cRight is not null)
            {
                if (cRight.Value.IsZero)
                    return left;
            }
            break;
        }
        return m.Bin(dt, op, cfNode, left, right);
    }
}
