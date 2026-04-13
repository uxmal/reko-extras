namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class DefNode : Node
{
    public DefNode(int number, string name, params Node?[] inputs) : base(number, inputs)
    {
        this.Name = name;
    }

    public string Name { get; }

    public override void Render(TextWriter sw)
    {
        sw.Write($"def {Name}");
    }

    public override void RenderReference(TextWriter sw)
    {
        sw.Write(Name);
    }
}