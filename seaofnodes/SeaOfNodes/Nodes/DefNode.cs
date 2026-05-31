using Reko.Core;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class DefNode : Node
{
    public DefNode(int number, Storage storage, DataType dt, params Node?[] inputs)
        : base(number, dt, inputs)
    {
        this.Storage = storage;
    }

    public override string Label => "Def";

    public override void Render(TextWriter w)
    {
        w.Write("def ");
        this.RenderReference(w);
        w.Write(':');
        w.Write(DataType);
    }

    public override void RenderReference(TextWriter w)
    {
        if (Storage is not null && 
            (Inputs.Count < 2 || Inputs[1] is not CallNode))
        {
            if (Storage is SequenceStorage seq)
            {
                var seqId = string.Join("_", seq.Elements.Select(e => e.Name));
                w.Write(seqId);
            }
            else
            {
                w.Write(Storage.Name);
            }
            return;
        }
        base.RenderReference(w);
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitDefNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitDefNode(this, context);
}