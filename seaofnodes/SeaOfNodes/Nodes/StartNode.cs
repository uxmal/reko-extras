namespace Reko.Extras.SeaOfNodes.Nodes;

public class StartNode : Node
{
    public StartNode(int number, params Node?[] inputs) : base(number, inputs)
    {
    }

    public override void Render(TextWriter sw)
    {
        sw.Write($"start{base.Number}");
    }
}