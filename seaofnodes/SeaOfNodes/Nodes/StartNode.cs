namespace Reko.Extras.SeaOfNodes.Nodes;

public class StartNode : Node
{

    public StartNode(int number, params Node?[] inputs) : base(number, inputs)
    {
        this.EndNode = null!;
    }
    
    public EndNode EndNode { get; internal set; }

    public override string Label => "Start";

    public override void Render(TextWriter sw)
    {
        sw.Write($"start{base.Number}");
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitStartNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitStartNode(this, context);
}