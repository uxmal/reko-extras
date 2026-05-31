using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Nodes;

namespace Reko.Extras.SeaOfNodes.Analysis;

public partial class PeepholeOptimizer
{

    public Node Slice(DataType dt, Node input, int offset)
    {
        if (offset == 0 && dt.BitSize == input.DataType.BitSize)
        {
            return input;
        }
        return m.Slice(dt, input, offset);
    }
}
