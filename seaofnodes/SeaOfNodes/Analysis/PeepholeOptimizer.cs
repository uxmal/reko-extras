using Reko.Core.Expressions;
using Reko.Core.Operators;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.SeaOfNodes.Analysis;

public class PeepholeOptimizer
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
        if (cLeft != null && cRight != null)
        {
            var c = op.ApplyConstants(dt, cLeft.Value, cRight.Value);
            return m.Const(c);
        }
        if (IsSymmetric(op) && cLeft is not null)
        {
            return m.Bin(dt, op, cfNode, right, cLeft);
        }
        return m.Bin(dt, op, cfNode, left, right);
    }

    public SeqNode Seq(DataType dt, params Node[] inputs)
    {
        List<Node?> nodes = [ ];
        foreach (var n in inputs)
        {
            if (n is SeqNode seq)
            {
                nodes.AddRange(seq.Inputs.Skip(1)!);
            }
            else
            {
                nodes.Add(n);
            }
        }
        return m.Seq(dt, inputs.ToArray());
    }

    public SliceNode Slice(DataType dt, Node input, int offset)
    {
        return m.Slice(dt, input, offset);
    }

    internal Node? Cond(DataType dataType, Node? value, Node exp)
    {
        return m.Cond(dataType, value, exp);
    }

    internal Node? Const(Constant constant)
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
