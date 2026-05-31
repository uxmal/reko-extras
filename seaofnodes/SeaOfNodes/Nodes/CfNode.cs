using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public abstract class CfNode : Node
{
    protected CfNode(int number, params Node?[] inputs) : base(number, VoidType.Instance, inputs)
    {
    }

    protected CfNode(int number, Node cfNode, params Node?[] inputs)
        : base(number, VoidType.Instance, cfNode, inputs)
    {
    }

}