using Reko.Core.Expressions;
using Reko.Core.Operators;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reko.Extras.SeaOfNodes.Analysis;

public class LongAddRewriter : INodeVisitor<Node?>
{
    private readonly NodeFactory factory;
    private readonly HashSet<Node> reachable;
    private readonly Dictionary<Node, Node?> replacements;

    public LongAddRewriter(NodeFactory factory)
    {
        this.factory = factory;
        this.reachable = new HashSet<Node>();
        this.replacements = new Dictionary<Node, Node?>();
    }

    public StartNode Transform(StartNode graph)
    {
        CollectReachable(graph);
        ProcessGraph();

        foreach (var (original, replacement) in replacements.Where(kv => kv.Value is not null))
        {
            replacement!.Storage = original.Storage;
            Node.Replace(original, replacement);
        }
        return graph;
    }

    private void CollectReachable(Node start)
    {
        var stack = new Stack<Node>();
        stack.Push(start);
        while (stack.TryPop(out var node))
        {
            if (!reachable.Add(node))
                continue;
            foreach (var output in node.Outputs)
                stack.Push(output);
        }
    }

    private void ProcessGraph()
    {
        foreach (var node in reachable.OfType<OperationNode>().OrderBy(n => n.Number).ToList())
        {
            if (IsAddOrSub(node))
                TryFuseLongOperation(node);
            else if (node.Operator.Type == OperatorType.Or)
                TryFuseLongShiftRight(node);
        }
    }

    private static bool IsAddOrSub(OperationNode node)
        => node.Operator.Type == OperatorType.IAdd || node.Operator.Type == OperatorType.ISub;

    private bool TryFuseLongOperation(OperationNode highOp)
    {
        // Pattern: high = high_part (+|-) carry; carry = cond(low)
        if (highOp.Inputs.Count != 3)
            return false;

        if (highOp.Inputs[2] is not CondNode carryGen || carryGen.Inputs.Count < 2)
            return false;

        if (carryGen.Inputs[1] is not OperationNode lowOp || !IsAddOrSub(lowOp))
            return false;

        if (lowOp.Operator.Type != highOp.Operator.Type)
            return false;

        // Restrict fusion to operations that are anchored in at least one common block.
        if (!GetConsumerBlocks(lowOp).Overlaps(GetConsumerBlocks(highOp)))
            return false;

        var lowLeft = lowOp.Inputs[1];
        var lowRight = lowOp.Inputs[2];
        var highLeft = highOp.Inputs[1];
        if (lowLeft is null || lowRight is null || highLeft is null)
            return false;

        var combinedType = CombineTypes(lowOp.DataType, highOp.DataType);

        var (highReg, highImm) = ExtractHighRegAndImm(highLeft);

        var seqLeft = factory.Seq(combinedType, highReg, lowLeft);
        var seqRight = BuildWideRhs(combinedType, highOp.DataType, lowOp.DataType, highImm, lowRight);

        var wideOp = factory.Bin(
            combinedType,
            lowOp.Operator.Type == OperatorType.IAdd ? Operator.IAdd : Operator.ISub,
            null,
            seqLeft,
            seqRight);

        var sliceLow = factory.Slice(lowOp.DataType, wideOp, 0);
        var sliceHigh = factory.Slice(highOp.DataType, wideOp, lowOp.DataType.BitSize);

        replacements[lowOp] = sliceLow;
        replacements[highOp] = sliceHigh;
        replacements[carryGen] = factory.Cond(carryGen.DataType, null, sliceLow);
        return true;
    }

    private bool TryFuseLongShiftRight(OperationNode orNode)
    {
        if (orNode.Inputs.Count != 3)
            return false;

        var left = orNode.Inputs[1];
        var right = orNode.Inputs[2];
        if (left is null || right is null)
            return false;

        if (!TryGetLowShiftAndSpill(left, right, out var lowShift, out var spillShift)
            && !TryGetLowShiftAndSpill(right, left, out lowShift, out spillShift))
            return false;

        if (lowShift.Inputs.Count != 3 || spillShift.Inputs.Count != 3)
            return false;

        var lowInput = lowShift.Inputs[1];
        var shiftAmount = lowShift.Inputs[2];
        var highInput = spillShift.Inputs[1];
        var spillAmount = spillShift.Inputs[2];
        if (lowInput is not ExpressionNode lowExpr || shiftAmount is null || highInput is not ExpressionNode highExpr || spillAmount is null)
            return false;

        if (!MatchesComplementaryShiftAmount(spillAmount, shiftAmount, lowExpr.DataType.BitSize))
            return false;

        var highShift = FindMatchingHighShift(highInput, shiftAmount, lowShift);
        if (highShift is null)
            return false;

        var combinedType = CombineTypes(lowExpr.DataType, highExpr.DataType);
        var seq = factory.Seq(combinedType, highInput, lowInput);
        var wideShift = factory.Bin(combinedType, lowShift.Operator, null, seq, shiftAmount);
        var sliceLow = factory.Slice(lowExpr.DataType, wideShift, 0);
        var sliceHigh = factory.Slice(highExpr.DataType, wideShift, lowExpr.DataType.BitSize);

        replacements[orNode] = sliceLow;
        replacements[highShift] = sliceHigh;
        return true;
    }

    private static bool TryGetLowShiftAndSpill(Node candidateLow, Node candidateSpill, out OperationNode lowShift, out OperationNode spillShift)
    {
        lowShift = null!;
        spillShift = null!;
        if (candidateLow is not OperationNode lowOp || candidateSpill is not OperationNode spillOp)
            return false;
        if (lowOp.Operator.Type != OperatorType.Shr && lowOp.Operator.Type != OperatorType.Sar)
            return false;
        if (spillOp.Operator.Type != OperatorType.Shl)
            return false;
        lowShift = lowOp;
        spillShift = spillOp;
        return true;
    }

    private static bool MatchesComplementaryShiftAmount(Node spillAmount, Node shiftAmount, int bitSize)
    {
        if (spillAmount is not OperationNode add || add.Operator.Type != OperatorType.IAdd || add.Inputs.Count != 3)
            return false;
        if (add.Inputs[1] is not OperationNode neg || neg.Operator.Type != OperatorType.Neg || neg.Inputs.Count != 2)
            return false;
        if (!ReferenceEquals(neg.Inputs[1], shiftAmount))
            return false;
        if (add.Inputs[2] is not ConstantNode c)
            return false;
        return c.Value.ToUInt64() == (ulong)bitSize;
    }

    private OperationNode? FindMatchingHighShift(Node highInput, Node shiftAmount, OperationNode lowShift)
    {
        foreach (var user in highInput.Outputs)
        {
            if (ReferenceEquals(user, lowShift))
                continue;
            if (user is not OperationNode op)
                continue;
            if (op.Operator.Type != lowShift.Operator.Type)
                continue;
            if (op.Inputs.Count != 3)
                continue;
            if (!ReferenceEquals(op.Inputs[1], highInput))
                continue;
            if (!ReferenceEquals(op.Inputs[2], shiftAmount))
                continue;
            return op;
        }
        return null;
    }

    private static (Node highReg, Node? highImm) ExtractHighRegAndImm(Node highLeft)
    {
        if (highLeft is OperationNode highLeftOp
            && (highLeftOp.Operator.Type == OperatorType.IAdd || highLeftOp.Operator.Type == OperatorType.ISub)
            && highLeftOp.Inputs.Count == 3
            && highLeftOp.Inputs[1] is not null
            && highLeftOp.Inputs[2] is not null)
        {
            return (highLeftOp.Inputs[1]!, highLeftOp.Inputs[2]);
        }
        return (highLeft, null);
    }

    private Node BuildWideRhs(DataType combinedType, DataType highType, DataType lowType, Node? highPart, Node lowPart)
    {
        var hi = highPart ?? factory.Const(Constant.Create(highType, 0));

        if (hi is ConstantNode hc && lowPart is ConstantNode lc)
        {
            var lowBits = lowType.BitSize;
            var loMask = lowBits >= 64 ? ulong.MaxValue : ((1UL << lowBits) - 1UL);
            var value = (hc.Value.ToUInt64() << lowBits) | (lc.Value.ToUInt64() & loMask);
            return factory.Const(Constant.Create(combinedType, value));
        }

        if (TryCombineAdjacentHalfLoads(combinedType, hi, lowPart, out var wideLoad))
            return wideLoad;

        return factory.Seq(combinedType, hi, lowPart);
    }

    private bool TryCombineAdjacentHalfLoads(DataType combinedType, Node highPart, Node lowPart, out Node wideLoad)
    {
        wideLoad = null!;
        if (highPart is not LoadNode highLoad || lowPart is not LoadNode lowLoad)
            return false;
        if (highLoad.Inputs.Count != 3 || lowLoad.Inputs.Count != 3)
            return false;
        if (!ReferenceEquals(highLoad.Inputs[0], lowLoad.Inputs[0]))
            return false;
        if (!ReferenceEquals(highLoad.Inputs[1], lowLoad.Inputs[1]))
            return false;
        if (highLoad.Inputs[2] is null || lowLoad.Inputs[2] is null)
            return false;
        if (!TryGetBaseAndOffset(highLoad.Inputs[2]!, out var hiBase, out var hiOff))
            return false;
        if (!TryGetBaseAndOffset(lowLoad.Inputs[2]!, out var loBase, out var loOff))
            return false;
        if (!ReferenceEquals(hiBase, loBase))
            return false;

        var lowBytes = lowLoad.DataType.BitSize / 8;
        if (hiOff != loOff + lowBytes)
            return false;

        wideLoad = factory.Load(lowLoad.Inputs[0]!, lowLoad.Inputs[1]!, combinedType, lowLoad.Inputs[2]!);
        return true;
    }

    private static bool TryGetBaseAndOffset(Node ea, out Node baseNode, out long offset)
    {
        baseNode = null!;
        offset = 0;
        if (ea is OperationNode add && add.Operator.Type == OperatorType.IAdd && add.Inputs.Count == 3)
        {
            if (add.Inputs[1] is not null && add.Inputs[2] is ConstantNode c)
            {
                baseNode = add.Inputs[1]!;
                offset = unchecked((long)c.Value.ToUInt64());
                return true;
            }
            if (add.Inputs[2] is not null && add.Inputs[1] is ConstantNode c2)
            {
                baseNode = add.Inputs[2]!;
                offset = unchecked((long)c2.Value.ToUInt64());
                return true;
            }
        }
        return false;
    }

    private HashSet<BlockNode> GetConsumerBlocks(Node node)
    {
        var result = new HashSet<BlockNode>();
        var seen = new HashSet<Node>();
        var wl = new Queue<Node>(node.Outputs);
        while (wl.Count > 0)
        {
            var n = wl.Dequeue();
            if (!seen.Add(n))
                continue;
            if (n.IsFloating)
            {
                foreach (var o in n.Outputs)
                    wl.Enqueue(o);
                continue;
            }
            var b = FindOwningBlock(n);
            if (b is not null)
                result.Add(b);
        }
        return result;
    }

    private static BlockNode? FindOwningBlock(Node n)
    {
        var seen = new HashSet<Node>();
        var cur = n;
        while (cur.Inputs.Count > 0 && cur.Inputs[0] is not null)
        {
            var cf = cur.Inputs[0]!;
            if (!seen.Add(cf))
                break;
            if (cf is BlockNode b)
                return b;
            cur = cf;
        }
        return null;
    }

    private static DataType CombineTypes(DataType lower, DataType upper)
    {
        var totalBits = lower.BitSize + upper.BitSize;
        return PrimitiveType.Create(upper.Domain, totalBits);
    }

    public Node? VisitAddressNode(AddressNode node) => null;
    public Node? VisitApplicationNode(ApplicationNode node) => null;
    public Node? VisitBlockNode(BlockNode node) => null;
    public Node? VisitCallNode(CallNode node) => null;
    public Node? VisitCondNode(CondNode node) => null;
    public Node? VisitConstantNode(ConstantNode node) => null;
    public Node? VisitConversionNode(ConversionNode node) => null;
    public Node? VisitDefNode(DefNode node) => null;
    public Node? VisitEndNode(EndNode node) => null;
    public Node? VisitIfNode(IfNode node) => null;
    public Node? VisitLoadNode(LoadNode node) => null;
    public Node? VisitMemoryNode(MemoryNode node) => null;
    public Node? VisitOperationNode(OperationNode node) => null;
    public Node? VisitOutArgumentNode(OutArgumentNode outArgumentNode) => null;
    public Node? VisitPhiNode(PhiNode node) => null;
    public Node? VisitProcedureConstantNode(ProcedureConstantNode node) => null;
    public Node? VisitReturnNode(ReturnNode node) => null;
    public Node? VisitSeqNode(SeqNode node) => null;
    public Node? VisitSideEffectNode(SideEffectNode node) => null;
    public Node? VisitSliceNode(SliceNode node) => null;
    public Node? VisitStartNode(StartNode node) => null;
    public Node? VisitStoreNode(StoreNode node) => null;
    public Node? VisitStringNode(StringNode node) => null;
    public Node? VisitSwitchNode(SwitchNode node) => null;
    public Node? VisitTestNode(TestNode node) => null;
    public Node? VisitUseNode(UseNode node) => null;
}
