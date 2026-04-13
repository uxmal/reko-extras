using Reko.Core;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class AddressNode : ExpressionNode
{
    public AddressNode(int number, Address addr) : base(number, addr.DataType, [null])
    {
        this.Value = addr;
    }

    public Address Value { get; }

    public override void RenderReference(TextWriter sw)
    {
        sw.Write(Value.ToString());
    }

    public override void Render(TextWriter sw)
    {
        sw.Write(Value.ToString());
    }
}
