namespace Reko.Extras.SeaOfNodes.Nodes;

public class ReturnNode : CfNode
{
    public ReturnNode(int number, params Node?[] inputs)
        : base(number, inputs)
    {
    }

    public override string Label => "Return";

    public Node? Expression => Inputs.Count == 2 ? Inputs[1]! : null;

    public override void Render(TextWriter sw)
    {
        sw.Write($"return");
        var exp = Expression;
        if (exp is not null)
        {
            sw.Write(' ');
            exp.RenderReference(sw);
        }
    }

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitReturnNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitReturnNode(this, context);
}