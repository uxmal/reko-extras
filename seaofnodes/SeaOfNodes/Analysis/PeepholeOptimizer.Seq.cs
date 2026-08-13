using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Nodes;
using System.Diagnostics;
using System.Numerics;

namespace Reko.Extras.SeaOfNodes.Analysis;

public partial class PeepholeOptimizer
{
    public Node Seq(DataType dt, params Node[] inputs)
    {
        List<Node> nodes = FlattenNestedSequences(inputs);

        nodes = FuseAdjacentSlices(nodes);

        if (nodes.Count == 1)
            return nodes[0];

        var cnode = FuseAdjacentConstants(dt, nodes);
        if (cnode is not null)
            return cnode;

        var def = FuseDefs(dt, nodes);
        if (def is not null)
            return def;

        return m.Seq(dt, nodes.ToArray());
    }

    private static List<Node> FlattenNestedSequences(Node[] inputs)
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

        return nodes;
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

    private DefNode? FuseDefs(DataType dt, List<Node> newSeq)
    {
        if (!newSeq.All(e => e is DefNode))
            return null;
        var cfNode = newSeq[0].Inputs[0];
        if (!newSeq.All(e => e.Inputs[0] == cfNode))
            return null;
        var def = (DefNode)newSeq[0];
        var storages = new List<Storage>();
        Debug.Assert(def.Storage is not null, "Def nodes should always have a storage.");
        storages.Add(def.Storage);
        for (int i = 1; i < newSeq.Count; ++i)
        {
            var d = (DefNode)newSeq[i];
            if (d.Inputs[0] != def.Inputs[0])
                return null;
            Debug.Assert(d.Storage is not null, "Def nodes should always have a storage.");
            storages.Add(d.Storage);
        }
        var fusedStorage = MakeSequenceStorage(dt, storages);
        var fusedDef = m.Def(def.Inputs[0]!, fusedStorage, dt);
        int bitOffset = 0;
        for (int i = newSeq.Count - 1; i >= 0; --i)
        {
            var d = newSeq[i];
            var slice = this.Slice(d.DataType, fusedDef, bitOffset);
            Node.Replace(d, slice);
            bitOffset += d.DataType.BitSize;
        }
        return fusedDef;
    }

    private Storage MakeSequenceStorage(DataType dt, List<Storage> storages)
    {
        var elements = new List<Storage>();
        foreach (var s in storages)
        {
            if (s is SequenceStorage seq)
            {
                elements.AddRange(seq.Elements);
            }
            else
            {
                elements.Add(s);
            }
        }
        return new SequenceStorage(dt, elements.ToArray());
    }
}
