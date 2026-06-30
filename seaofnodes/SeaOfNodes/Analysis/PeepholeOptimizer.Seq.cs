using Reko.Core.Expressions;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Nodes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.SeaOfNodes.Analysis;

public partial class PeepholeOptimizer
{
    public Node Seq(DataType dt, params Node[] inputs)
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

        var cnode = FuseAdjacentConstants(dt, nodes);
        if (cnode is not null)
            return cnode;

        if (nodes.Count == 1)
            return nodes[0];
        return m.Seq(dt, nodes.ToArray());
    }

    private ConstantNode? FuseAdjacentConstants(DataType dt, List<Node> newSeq)
    {
        if (!newSeq.All(e => e is ConstantNode))
            return null;
        BigInteger value = BigInteger.Zero;
        for (int i = 0; i < newSeq.Count; ++i)
        {
            var c = (ConstantNode)newSeq[i];
            value = (value << c.DataType.BitSize) | c.Value.ToBigInteger();
        }
        return m.Const(Constant.Create(dt, value));
    }

    private List<Node> FuseAdjacentSlices(List<Node> nodes)
    {
        SliceNode? curSlice = null;
        Node? slicedNode = null;
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
                    slicedNode = s.Inputs[1]!;
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
