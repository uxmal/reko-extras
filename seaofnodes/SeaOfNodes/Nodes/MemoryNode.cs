using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class MemoryNode : Node
{
    public MemoryNode(int number, DataType dt, params Node?[] inputs) 
        : base(number, dt, inputs)
    {
    }

    public override string Label => "Mem";

    public override void Render(TextWriter sw)
    {
        sw.Write($"Mem{Number}");
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitMemoryNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitMemoryNode(this, context);
}
