using System.Diagnostics;
using Reko.Core.Operators;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class OperationNode : ExpressionNode
{
    private static readonly Dictionary<OperatorType, string> operatorName = new()
    {
        { OperatorType.IAdd, " +" },
        { OperatorType.ISub, " -" },
        { OperatorType.USub, " -u" },
        { OperatorType.IMul, " *" },
        { OperatorType.SMul, " *s" },
        { OperatorType.UMul, " *u" },
        { OperatorType.SDiv, " /s" },
        { OperatorType.UDiv, " /u" },
        { OperatorType.IMod, " %" },
        { OperatorType.SMod, " %s" },
        { OperatorType.UMod, " %u" },
        { OperatorType.FAdd, " +f" },
        { OperatorType.FSub, " -f" },
        { OperatorType.FMul, " *f" },
        { OperatorType.FDiv, " /f" },
        { OperatorType.FMod, " %f" },
        { OperatorType.FNeg, "-" },
        { OperatorType.And, " &" },
        { OperatorType.Or, " |" },
        { OperatorType.Xor, " ^" },
        { OperatorType.Shr, " >>u" },
        { OperatorType.Sar, " >>" },
        { OperatorType.Shl, " <<" },
        { OperatorType.Cand, " &&" },
        { OperatorType.Cor, " ||" },
        { OperatorType.Lt, " <" },
        { OperatorType.Gt, " >" },
        { OperatorType.Le, " <=" },
        { OperatorType.Ge, " >=" },
        { OperatorType.Feq, " ==f" },
        { OperatorType.Fne, " !=f" },
        { OperatorType.Flt, " <f" },
        { OperatorType.Fgt, " >f" },
        { OperatorType.Fle, " <=f" },
        { OperatorType.Fge, " >=f" },
        { OperatorType.Ult, " <u" },
        { OperatorType.Ugt, " >u" },
        { OperatorType.Ule, " <=u" },
        { OperatorType.Uge, " >=u" },
        { OperatorType.Eq, " ==" },
        { OperatorType.Ne, " !=" },
        { OperatorType.Not, " !" },
        { OperatorType.Neg, "-" },
        { OperatorType.Comp, "~" },
        { OperatorType.AddrOf, "&" },
        { OperatorType.Comma, "," }
    };

    public OperationNode(int number, DataType dt, Operator op, params Node?[] inputs)
     : base(number, dt, inputs)
    {
        this.Operator = op;
    }

    public override string Label => operatorName[this.Operator.Type].Trim();

    public Operator Operator { get; }

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
        sw.Write(" = ");
        string opName = operatorName[this.Operator.Type];
        if (Inputs.Count == 2)
        {
            // Unary prefix operator
            sw.Write(opName);
            var input = Inputs[1];
            if (input is null)
                throw new InvalidOperationException();
            input.RenderReference(sw);
        }
        else
        {
            for (int i = 1; i < Inputs.Count; i++)
            {
                var input = Inputs[i];
                if (input is null)
                    throw new InvalidOperationException();

                if (i > 1)
                {
                    WriteOperator(opName, input, sw);
                }
                input.RenderReference(sw);
            }
        }
    }

    private void WriteOperator(string opName, Node input, TextWriter sw)
    {
        sw.Write(opName);
        var dtResult = this.DataType;
        if (dtResult.BitSize != ((ExpressionNode)input).DataType.BitSize)
            sw.Write(dtResult.BitSize);
        sw.Write(' ');
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitOperationNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitOperationNode(this, context);

}