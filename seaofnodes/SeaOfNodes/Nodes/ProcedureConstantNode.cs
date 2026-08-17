using Reko.Core;
using Reko.Core.Output;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class ProcedureConstantNode : Node
{
    public ProcedureConstantNode(int number, DataType dt, ProcedureBase procedure) : base(number, dt, [null])
    {
        this.Procedure = procedure;
    }

    public override string Label => Procedure.Name;

    public ProcedureBase Procedure { get; }

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
    }

    public override void RenderReference(TextWriter sw)
    {
        sw.Write(Procedure.Name);
        var genArgs = Procedure.GetGenericArguments();
        var InnerFormatter = new TextFormatter(sw);
        if (genArgs.Length > 0)
        {
            var sep = '<';
            var tf = new TypeReferenceFormatter(InnerFormatter);
            foreach (var arg in genArgs)
            {
                InnerFormatter.Write(sep);
                sep = ',';
                tf.WriteTypeReference(arg);
            }
            InnerFormatter.Write('>');
        }
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitProcedureConstantNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitProcedureConstantNode(this, context);
}