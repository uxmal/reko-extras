using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class MemoryNode : Node
{
    public MemoryNode(int number, params Node?[] inputs) : base(number, inputs)
    {
    }

    public override void Render(TextWriter sw)
    {
        sw.Write($"Mem{Number}");
    }
}
