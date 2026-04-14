using Reko.Core;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class DefNode : Node
{
    public DefNode(int number, Storage storage, DataType dt, params Node?[] inputs) : base(number, inputs)
    {
        this.Storage = storage;
        this.DataType = dt;
    }

    public DataType DataType { get; }
    public Storage Storage { get; }

    public override void Render(TextWriter sw)
    {
        sw.Write("def ");
        this.RenderReference(sw);
        sw.Write(':');
        sw.Write(DataType);
    }

    public override void RenderReference(TextWriter sw)
    {
        if (Name is not null)
        {
            sw.Write(Name);
            return;
        }
        base.RenderReference(sw);
    }
}