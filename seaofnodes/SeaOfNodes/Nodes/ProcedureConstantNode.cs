using Reko.Core;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class ProcedureConstantNode : Node
{
    public ProcedureConstantNode(int number, ProcedureBase procedure) : base(number, [null])
    {
        this.Procedure = procedure;
    }

    public ProcedureBase Procedure { get; }

    public override void Render(TextWriter sw)
    {
        sw.Write($"proc{base.Number}({Procedure.Name})");
    }
}