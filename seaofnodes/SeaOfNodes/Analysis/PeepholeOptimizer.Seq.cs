using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Nodes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.SeaOfNodes.Analysis;

public partial class PeepholeOptimizer
{
    public ExpressionNode Seq(DataType dt, params Node[] inputs)
    {
        List<Node> nodes = [];
        foreach (var n in inputs)
        {
            if (n is SeqNode seq)
            {
                nodes.AddRange(seq.Inputs.Skip(1)!);
            }
            else
            {
                nodes.Add(n);
            }
        }
        nodes = FuseAdjacentSlices(nodes);
        if (nodes.Count == 1)
            return (ExpressionNode)nodes[0];
        return m.Seq(dt, nodes.ToArray());
    }

    private List<Node> FuseAdjacentSlices(List<Node> nodes)
    {
        SliceNode? curSlice = null;
        ExpressionNode? slicedNode = null;
        Domain dom = default;
        int bitsize = 0;
        int offset = 0;
        int j = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is SliceNode s)
            {
                if (curSlice is null)
                {
                    curSlice = s;
                    slicedNode = (ExpressionNode)s.Inputs[1]!;
                    dom = s.DataType.Domain;
                    bitsize = s.DataType.BitSize;
                    offset = s.Offset;
                }
                else if (AreAdjacentSlices(curSlice, s))
                {
                    bitsize += s.DataType.BitSize;
                    offset = s.Offset;
                }
                else
                {
                    Debug.Assert(slicedNode is not null);
                    nodes[j++] = this.Slice(
                        PrimitiveType.Create(dom, bitsize),
                        slicedNode,
                        offset);
                    curSlice = null;
                }
            }
            else
            {
                if (curSlice is not null)
                {
                    Debug.Assert(slicedNode is not null);
                    nodes[j++] = this.Slice(
                        PrimitiveType.Create(dom, bitsize),
                        slicedNode,
                        offset);
                    curSlice = null;
                }
                else
                {
                    nodes[j++] = nodes[i];
                }
            }
        }
        if (curSlice is not null)
        {
            Debug.Assert(slicedNode is not null);
            nodes[j++] = this.Slice(
                PrimitiveType.Create(dom, bitsize),
                slicedNode,
                offset);
        }
        nodes.RemoveRange(j, nodes.Count - j);
        return nodes;
    }

    private bool AreAdjacentSlices(SliceNode hi, SliceNode lo)
    {
        return
            hi.Inputs[1] == lo.Inputs[1] &&
            hi.Offset == lo.Offset + lo.DataType.BitSize;
    }
}
