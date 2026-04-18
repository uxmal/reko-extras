namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class EndNode : Node
{
    public EndNode(int number, params Node?[] inputs) : base(number, inputs)
    {
    }

    public override void Render(TextWriter sw)
    {
        sw.Write($"end{base.Number}");
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitEndNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitEndNode(this, context);
}   