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

    public ExpressionNode And(DefNode left, ulong right)
    {
        return Bin(left.DataType, Operator.And, null, left, m.Const(Constant.Create(left.DataType, right)));
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
