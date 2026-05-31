using System.Diagnostics;
using Reko.Core;
using Reko.Core.Lib;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class UseNode : Node
{
    public UseNode(int number, Storage storage, BitRange bitRange, Node? cfNode)
        : base(number, storage.DataType, cfNode)
    {
        this.Storage = storage;
        this.BitRange = bitRange;
    }

    public BitRange BitRange { get; }

    public override string Label => "Use";

    public override void Render(TextWriter sw)
    {
        sw.Write("use ");
        sw.Write(this.Storage);
        sw.Write(':');
        var input = this.Inputs[1];
        if (input is null)
            throw new InvalidOperationException();
        input.RenderReference(sw);
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitUseNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitUseNode(this, context);
}