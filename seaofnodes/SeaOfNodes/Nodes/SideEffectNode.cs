namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class SideEffectNode : Node
{
    public SideEffectNode(int number, Node ctrl,  Node input) 
        : base(number, ctrl, input)
    {
    }

    public override string Label => "SideEffect";

    public override void Render(TextWriter sw)
    {
        this.Inputs[1]!.Render(sw);
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitSideEffectNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitSideEffectNode(this, context);
}