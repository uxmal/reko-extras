namespace Reko.Extras.SeaOfNodes.Nodes;

public abstract class CfNode : Node
{
    protected CfNode(int number, params Node?[] inputs) : base(number, inputs)
    {
    }

    protected CfNode(int number, Node cfNode, params Node?[] inputs)
        : base(number, cfNode, inputs)
    {
    }

}