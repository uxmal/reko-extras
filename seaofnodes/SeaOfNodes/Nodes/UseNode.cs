using Reko.Core;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class UseNode : Node
{
    public UseNode(int number, string name, Storage storage, Node value) : base(number, value)
    {
        this.Name = name;
        this.Storage = storage;
    }

    public string Name { get; }
    public Storage Storage { get; }

    public override void Render(TextWriter sw)
    {
        throw new NotImplementedException();
    }
}