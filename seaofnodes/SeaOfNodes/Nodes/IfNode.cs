namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class IfNode : CfNode
{
    public IfNode(int number, params Node?[] inputs) : base(number, inputs)
    {
    }

    public Node Predicate => this.Inputs[1]!;
    public override void Render(TextWriter sw)
    {
        sw.Write("if (");
        this.Predicate.RenderReference(sw);
        sw.Write(") goto ");
        sw.Write(((BlockNode)this.Outputs[1]).Block.DisplayName);
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitIfNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitIfNode(this, context);
}