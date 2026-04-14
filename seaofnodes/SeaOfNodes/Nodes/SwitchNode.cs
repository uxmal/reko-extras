namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class SwitchNode : CfNode
{
    private readonly string[] targets;

    public SwitchNode(int number, Node cfNode, Node selector, string[] targets)
        : base(number, cfNode, selector)
    {
        this.targets = targets;
    }

    public Node Selector => this.Inputs[1]!;

    public override void Render(TextWriter sw)
    {
        sw.Write("switch (");
        this.Selector.RenderReference(sw);
        sw.Write(") goto ");
        sw.Write(string.Join(", ", targets));
    }
}
