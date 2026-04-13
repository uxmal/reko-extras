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
        this.RenderReference(sw);
    }

    public override void RenderReference(TextWriter sw)
    {
        sw.Write(Procedure.Name);
    }
}