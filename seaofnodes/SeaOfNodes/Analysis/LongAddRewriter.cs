using Reko.Core.Collections;
using Reko.Core.Diagnostics;
using Reko.Core.Expressions;
using Reko.Core.Operators;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Nodes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Reko.Extras.SeaOfNodes.Analysis;

/// <summary>
/// Detects and fuses long (wide) arithmetic operations that are split across multiple
/// smaller-width operations chained via carry/borrow flags.
/// </summary>
public class LongAddRewriter : INodeVisitor<Node?>
{
    private static readonly TraceSwitch trace = new(nameof(LongAddRewriter), "")
    {
        Level = TraceLevel.Verbose
    };

    private readonly PeepholeOptimizer m;
    private readonly Dictionary<Node, Node?> replacements;

    public LongAddRewriter(PeepholeOptimizer m)
    {
        this.m = m;
        this.replacements = [];
    }

    public StartNode Transform(StartNode graph)
    {
        // Collect all reachable nodes
        var reachable = CollectReachable(graph);

        // Find and fuse long operation patterns
        ProcessGraph(graph, reachable);
        return graph;
    }

    private HashSet<Node> CollectReachable(Node start)
    {
        var reachable = new HashSet<Node>();
        var stack = new Stack<Node>();
        stack.Push(start);
        while (stack.TryPop(out var node))
        {
            if (!reachable.Add(node))
                continue;
            foreach (var output in node.Outputs)
            {
                stack.Push(output);
            }
            foreach (var input in node.Inputs)
            {
                if (input is not null)
                    stack.Push(input);
            }
        }
        return reachable;
    }

    private void ProcessGraph(StartNode graph, IEnumerable<Node> reachable)
    {
        var opNodes = reachable.OfType<OperationNode>().OrderBy(n => n.Number).ToList();
        var wl = new WorkList<ExpressionNode>();
        wl.AddRange(opNodes);
        while (wl.TryGetWorkItem(out var node))
        {
            if (node.Number == 49)
                _ = this; //$DEBUG
            if (node is OperationNode opNode)
            {
                if (IsAddOrSub(opNode))
                {
                    var newNode = TryFuseAddSub(opNode);
                    if (newNode is not null)
                    {
                        Debug.WriteLine($"== {newNode.Label}{newNode.Number} =======");
                        Dump(graph);

                        // If we fused a long operation, we may have created new opportunities for fusion.
                        wl.Add(newNode);
                    }
                }
                else if (TryFuseLongShiftRight(opNode))
                {
                    wl.AddRange(node.Outputs.OfType<OperationNode>());
                }
            }
        }
    }

    private void Dump(StartNode graph)
    {
        var sw = new StringWriter();
        var ngr = new NodeGraphRenderer();
        ngr.Render(graph, sw);
        Debug.WriteLine(sw.ToString());
    }

    private ExpressionNode? TryFuseAddSub(OperationNode addSubNode)
    {
        if (addSubNode.Inputs.Count != 3)
            return null;
        if (addSubNode.Number == 49)
        {
            _ = this; //$DEBUG
        }
        var hiLeft = addSubNode.Inputs[1];
        if (hiLeft is null)
            throw new InvalidOperationException();
        var hiRight = addSubNode.Inputs[2];
        if (hiRight is OperationNode opRight &&
            opRight.Operator == addSubNode.Operator)
        {
            var cyLeft = opRight.Inputs[1]!;
            var cyRight = opRight.Inputs[2]!;
            if (IsAnd(cyRight, out var andLeft, out var andRight) &&
                andRight is ConstantNode)
            {
                cyRight = andLeft;
            }
            if (cyRight is CondNode cond)
            {
                var maybeAdd = cond.Inputs[1]!;
                if (maybeAdd is SliceNode slice)
                {
                    maybeAdd = slice.Inputs[1]!;
                }
                if (IsBinary(maybeAdd, out var op, out var loLeft, out var loRight)
                    &&
                    op == addSubNode.Operator.Type)
                {
                    // Found a candidate.
                    trace.Verbose("Larw: found candidate high={0}+{1}, low={2}+{3}",
                        hiLeft, cyLeft, loLeft, loRight);

                    var dtLo = ((ExpressionNode)loLeft).DataType;
                    var dtHi = ((ExpressionNode)hiLeft).DataType;
                    var dt = CombineTypes(dtLo, dtHi);
                    var seqLeft = m.Seq(dt, hiLeft, loLeft);
                    var seqRight = m.Seq(dt, cyLeft, loRight);
                    var wideSum = m.Bin(dt, addSubNode.Operator, null, seqLeft, seqRight);
                    var sumLo = m.Slice(dtLo, wideSum, 0);
                    var sumHi = m.Slice(dtHi, wideSum, dtLo.BitSize);
                    Node.Replace(addSubNode, sumHi);
                    Node.Replace(cond.Inputs[1]!, sumLo);
                    return wideSum;
                }
            }
        }
        return null;
    }

    private static bool IsAnd(
        Node node, 
        [MaybeNullWhen(false)] out Node left,
        [MaybeNullWhen(false)] out Node right)
    {
        left = null;
        right = null;
        if (node is not OperationNode op || op.Operator.Type != OperatorType.And || op.Inputs.Count != 3)
            return false;
        if (op.Inputs[1] is null || op.Inputs[2] is null)
            return false;
        left = op.Inputs[1]!;
        right = op.Inputs[2]!;
        return true;
    }

    private static bool IsBinary(
        Node node,
        [MaybeNullWhen(false)] out OperatorType opType,
        [MaybeNullWhen(false)] out Node left,
        [MaybeNullWhen(false)] out Node right)
    {
        opType = default;
        left = null;
        right = null;
        if (node is not OperationNode op || (op.Operator.Type != OperatorType.IAdd && op.Operator.Type != OperatorType.ISub) || op.Inputs.Count != 3)
            return false;
        if (op.Inputs.Count != 3)
            return false;
        if (op.Inputs[1] is null || op.Inputs[2] is null)
            return false;
        opType = op.Operator.Type;
        left = op.Inputs[1]!;
        right = op.Inputs[2]!;
        return true;
    }

    private void CoalesceDuplicateFloatingNegations(IEnumerable<OperationNode> opNodes)
    {
        var groups = new Dictionary<(Node operand, int bitSize, Domain domain), List<OperationNode>>();
        foreach (var node in opNodes)
        {
            if (node.Operator.Type != OperatorType.Neg)
                continue;
            if (!node.IsFloating || node.Inputs.Count != 2 || node.Inputs[0] is not null)
                continue;
            var operand = node.Inputs[1];
            if (operand is null)
                continue;

            var key = (operand, node.DataType.BitSize, node.DataType.Domain);
            if (!groups.TryGetValue(key, out var bucket))
            {
                bucket = [];
                groups[key] = bucket;
            }
            bucket.Add(node);
        }

        foreach (var bucket in groups.Values)
        {
            if (bucket.Count < 2)
                continue;
            var canonical = bucket.OrderBy(n => n.Number).Last();
            foreach (var duplicate in bucket)
            {
                if (!ReferenceEquals(duplicate, canonical))
                    replacements[duplicate] = canonical;
            }
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
        trace.Verbose("Larw: fusing {0} and {1} via {2}", lowOp, highOp, carryGen);
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

        var seqLeft = m.Seq(combinedType, highReg, lowLeft);
        var seqRight = BuildWideRhs(combinedType, highOp.DataType, lowOp.DataType, highImm, lowRight);

        var wideOp = m.Bin(
            combinedType,
            lowOp.Operator.Type == OperatorType.IAdd ? Operator.IAdd : Operator.ISub,
            null,
            seqLeft,
            seqRight);

        var sliceLow = m.Slice(lowOp.DataType, wideOp, 0);
        var sliceHigh = m.Slice(highOp.DataType, wideOp, lowOp.DataType.BitSize);

        replacements[lowOp] = sliceLow;
        replacements[highOp] = sliceHigh;
        replacements[carryGen] = m.Cond(carryGen.DataType, null, sliceLow);
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
        var seq = m.Seq(combinedType, highInput, lowInput);
        var wideShift = m.Bin(combinedType, lowShift.Operator, null, seq, shiftAmount);
        var sliceLow = m.Slice(lowExpr.DataType, wideShift, 0);
        var sliceHigh = m.Slice(highExpr.DataType, wideShift, lowExpr.DataType.BitSize);

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
        var hi = highPart ?? m.Const(Constant.Create(highType, 0));

        if (hi is ConstantNode hc && lowPart is ConstantNode lc)
        {
            var lowBits = lowType.BitSize;
            var loMask = lowBits >= 64 ? ulong.MaxValue : ((1UL << lowBits) - 1UL);
            var value = (hc.Value.ToUInt64() << lowBits) | (lc.Value.ToUInt64() & loMask);
            return m.Const(Constant.Create(combinedType, value));
        }

        if (TryCombineAdjacentHalfLoads(combinedType, hi, lowPart, out var wideLoad))
            return wideLoad;

        return m.Seq(combinedType, hi, lowPart);
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

        wideLoad = m.Load(lowLoad.Inputs[0]!, lowLoad.Inputs[1]!, combinedType, lowLoad.Inputs[2]!);
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
