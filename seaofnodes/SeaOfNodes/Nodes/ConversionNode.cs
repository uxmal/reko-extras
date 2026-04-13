using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class ConversionNode : ExpressionNode
{
    public ConversionNode(int number, DataType dstType, DataType srcType, Node? cfNode, Node input)
        : base(number, dstType, cfNode, input)
    {
        this.SourceDataType = srcType;
    }

    public DataType SourceDataType { get; }

    public override void Render(TextWriter sw)
    {
        this.RenderReference(sw);
        sw.Write(" = CONVERT(");
        this.Inputs[1]!.RenderReference(sw);
        sw.Write(", ");
        sw.Write(this.SourceDataType);
        sw.Write(", ");
        sw.Write(this.DataType);
        sw.Write(')');
    }
}
