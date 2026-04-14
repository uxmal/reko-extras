using System.Runtime.Serialization;
using Reko.Core;
using Reko.Core.Output;

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
}