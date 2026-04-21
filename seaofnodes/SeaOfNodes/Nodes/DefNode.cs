using Reko.Core;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class DefNode : ExpressionNode
{
    public DefNode(int number, Storage storage, DataType dt, params Node?[] inputs) : base(number, dt, inputs)
    {
        this.Storage = storage;
    }

    public override string Label => "Def";

    public override void Render(TextWriter sw)
    {
        sw.Write("def ");
        this.RenderReference(sw);
        sw.Write(':');
        sw.Write(DataType);
    }

    public override void RenderReference(TextWriter sw)
    {
        if (Storage is not null && 
            (Inputs.Count < 2 || Inputs[1] is not CallNode))
        {
            sw.Write(Storage.Name);
            return;
        }
        base.RenderReference(sw);
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitDefNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitDefNode(this, context);
}