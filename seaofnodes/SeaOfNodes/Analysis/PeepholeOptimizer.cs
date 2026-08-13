using Reko.Core;
using Reko.Core.Analysis;
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

    public AddressNode Address(Address address)
    {
        return m.Address(address);
    }

    public Node And(DefNode left, ulong right)
    {
        return Bin(left.DataType, Operator.And, null, left, m.Const(left.DataType, right));
    }

    public Node ISub(Node left, Node right)
    {
        return Bin(left.DataType, Operator.ISub, null, left, right);
    }

    public Node ISub(Node left, long right)
    {
        return Bin(left.DataType, Operator.ISub, null, left, m.Const(left.DataType, right));
    }

    public CondNode Cond(DataType dataType, Node? value, Node exp)
    {
        return m.Cond(dataType, value, exp);
    }

    public ConstantNode Const(Constant constant)
    {
        return m.Const(constant);
    }

    public ConstantNode Const(DataType dt, long value)
    {
        return m.Const(dt, value);
    }

    public ConstantNode Const(DataType dt, ulong value)
    {
        return m.Const(dt, value);
    }

    public ApplicationNode Fn(DataType dt, Node? cfNode, Node fn, params Node[] args)
    {
        return m.Apply(dt, cfNode, fn, args);
    }

    public ApplicationNode Fn(DataType dt, Node? cfNode, IntrinsicProcedure fn, params Node[] args)
    {
        return m.Apply(dt, cfNode, fn, args);
    }


    public Node Load(Node cfNode, Node memNode, DataType dt, Node ea)
    {
        return m.Load(cfNode, memNode, dt, ea);
    }

    public Node Neg(DataType dt, Node node)
    {
        if (node is OperationNode op)
        {
            if (op.Operator.Type == OperatorType.Neg)
                return node.Inputs[1]!;
        }
        return m.Neg(dt, node);
    }

    public Node Not(Node node)
    {
        return m.Not(node);
    }

    public Node Phi(DataType dt, Node cfNode, params Node[] args)
    {
        return m.Phi(dt, cfNode, args);
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
