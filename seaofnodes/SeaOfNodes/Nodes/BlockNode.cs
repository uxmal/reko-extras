using Reko.Core;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class BlockNode : CfNode
{
    public BlockNode(int number, Block block, params Node?[] inputs) : base(number, inputs)
    {
        this.Block = block;
    }

    public Block Block { get; }

    public override void Render(TextWriter sw)
    {
        sw.Write("block");
    }
}