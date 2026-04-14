using System.Diagnostics;
using Reko.Core;
using Reko.Core.Lib;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class UseNode : Node
{
    public UseNode(int number, Storage storage, BitRange bitRange, Node? cfNode) : base(number, cfNode)
    {
        this.Storage = storage;
        this.BitRange = bitRange;
    }

    public new string Name => Storage.Name;
    public Storage Storage { get; }
    public BitRange BitRange { get; }

    public override void Render(TextWriter sw)
    {
        sw.Write(this.Storage);
        sw.Write(':');
        var input = this.Inputs[1];
        Debug.Assert(input is not null);
        input.RenderReference(sw);
    }
}