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
}   