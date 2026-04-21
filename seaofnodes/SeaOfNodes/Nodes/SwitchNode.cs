namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class SwitchNode : CfNode
{
    private readonly string[] targets;

    public SwitchNode(int number, Node cfNode, Node selector, string[] targets)
        : base(number, cfNode, selector)
    {
        this.targets = targets;
    }

    public override string Label => "Switch";

    public Node Selector => this.Inputs[1]!;

    public override void Render(TextWriter sw)
    {
        sw.Write("switch (");
        this.Selector.RenderReference(sw);
        sw.Write(") goto ");
        sw.Write(string.Join(", ", targets));
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitSwitchNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitSwitchNode(this, context);
}
