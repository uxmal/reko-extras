using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class DefNode : Node
{
    public DefNode(int number, string name, DataType dt, params Node?[] inputs) : base(number, inputs)
    {
        this.Name = name;
        this.DataType = dt;
    }

    public string Name { get; }

    public DataType DataType { get; }

    public override void Render(TextWriter sw)
    {
        sw.Write($"def {Name}:{DataType}");
    }

    public override void RenderReference(TextWriter sw)
    {
        sw.Write(Name);
    }
}