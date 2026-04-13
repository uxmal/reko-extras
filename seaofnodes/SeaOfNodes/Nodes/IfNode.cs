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
        sw.Write(")");
    }
}