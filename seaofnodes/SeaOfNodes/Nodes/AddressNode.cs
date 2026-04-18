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

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitAddressNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitAddressNode(this, context);
}
