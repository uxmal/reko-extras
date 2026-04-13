namespace Reko.Extras.SeaOfNodes.Nodes;

public class ReturnNode : CfNode
{
    public ReturnNode(int number, params Node?[] inputs) : base(number, inputs)
    {
    }

    public override void Render(TextWriter sw)
    {
        sw.Write($"return");
        if (Inputs.Count == 2)
        {
            var exp = Inputs[1]!;
            sw.Write(' ');
            exp.RenderReference(sw);
        }
    }
}