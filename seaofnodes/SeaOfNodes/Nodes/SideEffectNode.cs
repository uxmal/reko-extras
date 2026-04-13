namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class SideEffectNode : Node
{
    public SideEffectNode(int number, Node ctrl,  Node input) 
        : base(number, ctrl, input)
    {
    }

    public override void Render(TextWriter sw)
    {
        this.Inputs[1]!.Render(sw);
    }
}