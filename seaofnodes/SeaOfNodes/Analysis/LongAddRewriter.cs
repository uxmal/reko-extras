using Reko.Core.Collections;
using Reko.Core.Diagnostics;
using Reko.Core.Expressions;
using Reko.Core.Intrinsics;
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
        Dump(graph);

        // Collect all reachable nodes
        var reachable = graph.CollectReachableNodes();

        // Find and fuse long operation patterns
        ProcessGraph(graph, reachable);
        return graph;
    }

    private void ProcessGraph(StartNode graph, IEnumerable<Node> reachable)
    {
        var opNodes = reachable.OrderBy(n => n.Number).ToList();
        var wl = new WorkList<Node>();
        wl.AddRange(opNodes);
        while (wl.TryGetWorkItem(out var node))
        {
            if (replacements.ContainsKey(node))
                continue;
            if (node is BinaryNode bin)
            {
                switch (bin.Operator.Type)
                {
                case OperatorType.ISub:
                    var newNode = TryFuseNegation(bin);
                    if (newNode is not null)
                    {
                        Debug.WriteLine($"== {newNode.Label}{newNode.Number} =======");
                        Dump(graph);

                        // If we fused a long operation, we may have created new opportunities for fusion.
                        wl.Add(newNode);
                    }
                    break;
                case OperatorType.Or:
                    if (TryFuseLongShiftRight(bin))
                    {
                        wl.AddRange(node.Outputs.OfType<BinaryNode>());
                    }
                    break;
                }
            }
            if (node is ApplicationNode appl)
            {
                if (appl.Inputs[1] is ProcedureConstantNode pc)
                {
                    Node? newNode = null;
                    var name = pc.Procedure.Name;
                    if (name == CommonOps.IAddC.Name)
                    {
                        newNode = TryFuseAddSub(appl, Operator.IAdd);
                    }
                    else if (name == CommonOps.ISubC.Name)
                    {
                        newNode = TryFuseAddSub(appl, Operator.ISub);
                        if (newNode is null)
                        {
                            newNode = this.TryFuseNegation(appl);
                        }
                    }
                    else if (name == CommonOps.RorC.Name)
                    {
                        newNode = TryFuseSarRorC(appl);
                    }
                    else if (name == CommonOps.RolC.Name)
                    {
                        newNode = TryFuseShlRolC(appl);
                    }
                    if (newNode is not null)
                    {
                        Debug.WriteLine($"== {newNode.Label}{newNode.Number} =======");
                        Dump(graph);

                        // If we fused a long operation, we may have created new opportunities for fusion.
                        wl.Add(newNode);
                    }
                }
            }
        }
    }

    private Node? TryFuseSarRorC(ApplicationNode rorc)
    {
        var cy = rorc.Inputs[4]!;
        if (IsAnd(cy, out var andLeft, out var andRight) &&
            andRight is ConstantNode)
        {
            cy = andLeft;
        }
        if (cy is CondNode { Expression: BinaryNode shift })
        {
            if (shift.Operator == Operator.Sar || shift.Operator == Operator.Shr)
            {
                if (IsOne(shift.Right) && IsOne(rorc.Inputs[3]!))
                {
                    var dtNew = CombineTypes(shift.DataType, rorc.DataType);
                    var seqNode = m.Seq(dtNew, shift.Left, rorc.Inputs[2]!);
                    var newNode = m.Bin(dtNew, shift.Operator, null, seqNode, shift.Right);
                    var loSlice = m.Slice(rorc.DataType, newNode, 0);
                    var hiSlice = m.Slice(shift.DataType, newNode, rorc.DataType.BitSize);
                    ReplaceCondOfs(rorc, newNode);
                    Node.Replace(rorc, loSlice);
                    Node.Replace(shift, hiSlice);
                    replacements[rorc] = loSlice;
                    replacements[shift] = hiSlice;
                    Node.RemoveFromInputs(rorc);
                    Node.RemoveFromInputs(shift);
                    return newNode;
                }
            }
        }
        return null;
    }

    private Node? TryFuseShlRolC(ApplicationNode rolc)
    {
        var cy = rolc.Inputs[4]!;
        if (IsAnd(cy, out var andLeft, out var andRight) &&
            andRight is ConstantNode)
        {
            cy = andLeft;
        }
        if (cy is CondNode { Expression: BinaryNode shift })
        {
            if (shift.Operator == Operator.Shl)
            {
                if (IsOne(shift.Right) && IsOne(rolc.Inputs[3]!))
                {
                    var dtNew = CombineTypes(rolc.DataType, shift.DataType);
                    var seqNode = m.Seq(dtNew, rolc.Inputs[2]!, shift.Left);
                    var newNode = m.Bin(dtNew, shift.Operator, null, seqNode, shift.Right);
                    var loSlice = m.Slice(shift.DataType, newNode, 0);
                    var hiSlice = m.Slice(rolc.DataType, newNode, shift.DataType.BitSize);
                    ReplaceCondOfs(rolc, newNode);
                    Node.Replace(shift, loSlice);
                    Node.Replace(rolc, hiSlice);
                    replacements[shift] = loSlice;
                    replacements[rolc] = hiSlice;
                    Node.RemoveFromInputs(shift);
                    Node.RemoveFromInputs(rolc);
                    return newNode;
                }
            }
        }
        return null;
    }


    private static bool IsOne(Node node)
    {
        return node is ConstantNode c && c.Value.ToInt64() == 1;
    }


    private void Dump(StartNode graph)
    {
        var sw = new StringWriter();
        var ngr = new NodeGraphRenderer();
        ngr.Render(graph, sw);
        Debug.WriteLine(sw.ToString());
    }

    private Node? TryFuseAddSub(ApplicationNode addcSubc, BinaryOperator opType)
    {
        if (addcSubc.Inputs.Count != 5)
            return null;
        // Try detecting an addc/subc pattern.
        var hiLeft = addcSubc.Inputs[2]!;
        Node hiToReplace = addcSubc;
        var hiRight = addcSubc.Inputs[3]!;
        var cyRight = addcSubc.Inputs[4]!;
        if (!IsMaybeMaskedCondNode(cyRight, out var cond))
            return null;

        // We've established that addcSubc is indeed an ADDC/SUBC

        var loAddSub = cond.Inputs[1];
        if (loAddSub is SliceNode slice)
            loAddSub = slice.Inputs[1];

        if (IsAddSub(loAddSub, out var opLo, out var loLeft, out var loRight)
            && opLo == opType.Type)
        {
            // We may be seeing a PDP-11 style addc, which has only one argument. 
            //    add r2,r0
            //    adc r3
            //    add r3,r1
            if (hiRight is ConstantNode cZero && cZero.Value.IsZero)
            {
                // Check if the (only) non-cond users of addcSubc are another add/sub.
                foreach (var o in addcSubc.Outputs)
                {
                    if (IsAddSub(o, out var opEx, out var exLeft, out var exRight) &&
                        exLeft == addcSubc &&
                        opEx == opType.Type &&
                        !IsCarryAddSub(o))
                    {
                        hiRight = exRight;
                        hiToReplace = o;
                        break;
                    }
                }
            }
            // Found a candidate.
            trace.Verbose("Larw: found candidate high={0}+{1}, low={2}+{3}",
                hiLeft, hiRight, loLeft, loRight);

            var dtLo = loLeft.DataType;
            var dtHi = hiLeft.DataType;
            var dt = CombineTypes(dtHi, dtLo);
            var seqLeft = m.Seq(dt, hiLeft, loLeft);
            var seqRight = m.Seq(dt, hiRight, loRight);
            var wideSum = m.Bin(dt, opType, null, seqLeft, seqRight);
            var sumLo = m.Slice(dtLo, wideSum, 0);
            var sumHi = m.Slice(dtHi, wideSum, dtLo.BitSize);
            ReplaceCondOfs(addcSubc, wideSum);
            Node.Replace(hiToReplace, sumHi);
            Node.Replace(loAddSub!, sumLo);
            replacements[addcSubc] = sumHi;
            replacements[loAddSub!] = sumLo;

            Node.RemoveFromInputs(addcSubc);
            Node.RemoveFromInputs(loAddSub!);
            return wideSum;
        }
        return null;
    }

    private static bool IsCarryAddSub(Node node)
    {
        var inputs = node.Inputs;
        return node is BinaryNode opNode &&
             IsAddOrSub(opNode) &&
             IsMaybeMaskedCondNode(inputs[2]!, out _);
    }

    private static bool IsMaybeMaskedCondNode(Node n, [MaybeNullWhen(false)] out Node condNode)
    {
        if (IsAnd(n, out var andLeft, out var andRight) &&
            andRight is ConstantNode)
        {
            n = andLeft;
        }
        if (n is not CondNode cond)
        {
            condNode = null;
            return false;
        }
        condNode = cond;
        return true;
    }

    private void ReplaceCondOfs(Node addcSubc, Node wideSum)
    {
        foreach (var use in addcSubc.Outputs.ToList())
        {
            if (use is CondNode cond)
            {
                cond.ReplaceInput(1, wideSum);
            }
        }
    }

    private static bool IsAnd(
        Node node, 
        [MaybeNullWhen(false)] out Node left,
        [MaybeNullWhen(false)] out Node right)
    {
        left = null;
        right = null;
        if (node is not BinaryNode op || op.Operator.Type != OperatorType.And)
            return false;
        left = op.Left;
        right = op.Right;
        return true;
    }

    private static bool IsAddSub(
        Node? node,
        [MaybeNullWhen(false)] out OperatorType opType,
        [MaybeNullWhen(false)] out Node left,
        [MaybeNullWhen(false)] out Node right)
    {
        opType = default;
        left = null;
        right = null;
        if (node is not BinaryNode op || (!op.Operator.Type.IsAddOrSub()))
            return false;
        opType = op.Operator.Type;
        left = op.Left;
        right = op.Right;
        return true;
    }


    private static bool IsAddOrSub(BinaryNode node)
        => node.Operator.Type.IsAddOrSub();

    private bool TryFuseLongOperation(BinaryNode highOp)
    {
        // Pattern: high = high_part (+|-) carry; carry = cond(low)
        if (highOp.Inputs.Count != 3)
            return false;

        if (highOp.Inputs[2] is not CondNode carryGen || carryGen.Inputs.Count < 2)
            return false;

        if (carryGen.Inputs[1] is not BinaryNode lowOp || !IsAddOrSub(lowOp))
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

        var combinedType = CombineTypes(highOp.DataType, lowOp.DataType);

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

    private Node? TryFuseNegation(ApplicationNode subcNode)
    {
        // d0_8 = -d0
        // SCZ_9 = cond(d0_8)
        // CF_10 = SCZ_9 & 4<32>
        // d1_11 = __subc(0<32>, d1, CF_10)
        // SCZ = cond(d1_11)
        Debug.Assert(subcNode.Inputs.Count == 5);
        var cZeroLeft = subcNode.Inputs[2] as ConstantNode;
        var hiNegNode = subcNode.Inputs[3]!;
        var carry = subcNode.Inputs[4]!;
        if (cZeroLeft is not null && 
            cZeroLeft.Value.IsZero &&
            IsMaybeMaskedCondNode(carry, out var cond) &&
            cond.Inputs[1] is UnaryNode loNegNode &&
            loNegNode.Operator == Operator.Neg)
        {
            var dtLo = loNegNode.DataType;
            var dtHi = hiNegNode.DataType;
            var dt = CombineTypes(dtHi, dtLo);
            var seq = m.Seq(dt, hiNegNode, loNegNode.Inputs[1]!);
            var wideNeg = m.Neg(dt, seq);
            var sliceLo = m.Slice(dtLo, wideNeg, 0);
            var sliceHi = m.Slice(dtHi, wideNeg, dtLo.BitSize);
            ReplaceCondOfs(subcNode, wideNeg);
            Node.Replace(subcNode, sliceHi);
            Node.Replace(loNegNode, sliceLo);
            Node.RemoveFromInputs(subcNode);
            return wideNeg;
        }
        if (subcNode.Inputs[3] is ConstantNode cZeroRight &&
            cZeroRight.Value.IsZero)
        {
            BinaryNode? opCy;
            if (IsCarryNe0(subcNode, carry, out hiNegNode, out opCy))
            {
                var loNegNode2 = FindNegation(opCy.Inputs[1]!);
                if (loNegNode2 is null)
                    return null;
                var dtLo = loNegNode2.DataType;
                var dtHi = hiNegNode.DataType;
                var dt = CombineTypes(dtHi, dtLo);
                var seq = m.Seq(dt, hiNegNode, loNegNode2.Inputs[1]!);
                var wideNeg = m.Neg(dt, seq);
                var sliceLo = m.Slice(dtLo, wideNeg, 0);
                var sliceHi = m.Slice(dtHi, wideNeg, dtLo.BitSize);
                ReplaceCondOfs(subcNode, wideNeg);
                Node.Replace(loNegNode2, sliceLo);
                Node.Replace(subcNode, sliceHi);
                Node.RemoveFromInputs(subcNode);
                return wideNeg;

            }
        }
        return null;
    }

    private bool IsCarryNe0(
        ApplicationNode subcNode,
        Node carry,
        [MaybeNullWhen(false)] out Node hiNegNode,
        [MaybeNullWhen(false)] out BinaryNode opCy)
    {
        hiNegNode = null;
        opCy = null;
        if (!IsNeg(subcNode.Inputs[2], out hiNegNode))
            return false;
        if (carry is CondNode cn)
        {
            carry = cn.Inputs[1]!;
        }
        if (carry is not BinaryNode op)
            return false;
        if (op.Operator != Operator.Ne ||
            op.Inputs[2] is not ConstantNode cCy ||
            !cCy.Value.IsZero)
            return false;

        opCy = op;
        return true;
    }

    private Node? FindNegation(Node node)
    {
        if (node is UnaryNode o && o.Operator == Operator.Neg)
            return o;
        foreach (var use in node.Outputs)
        {
            if (use is UnaryNode op && op.Operator == Operator.Neg)
                return op;
        }
        return null;
    }


    private bool IsNeg(Node? node, [MaybeNullWhen(false)] out Node negatedNode)
    {
        if (node is UnaryNode o &&
            o.Operator == Operator.Neg)
        {
            negatedNode = o.Inputs[1]!;
            return true;
        }
        negatedNode = null;
        return false;
    }

    private Node? TryFuseNegation(BinaryNode subNode)
    {
        //     d0_8 = -d0
        //    n13 = -d1
        // CZ_9 = cond(d0_8)
        // C_11 = CZ_9 & 4 < 32 >
        // d1_14 = n13 - C_11
        // CZ_15 = cond(d1_14)
        var subLeft = subNode.Inputs[1]!;
        var subRight = subNode.Inputs[2]!;
        if (subLeft is UnaryNode opLeft && opLeft.Operator == Operator.Neg)
        {
            if (subRight is BinaryNode andNode &&
                andNode.Operator == Operator.And &&
                andNode.Inputs[2] is ConstantNode)
            {
                subRight = andNode.Inputs[1]!;
            }
            if (subRight is CondNode cond)
            {
                if (cond.Inputs[1] is UnaryNode negLo && negLo.Operator == Operator.Neg)
                {
                    var lo = negLo.Inputs[1]!;
                    var hi = opLeft.Inputs[1]!;
                    var dtLo = lo.DataType;
                    var dtHi = hi.DataType;
                    var dt = CombineTypes(dtHi, dtLo);
                    var seq = m.Seq(dt, hi, lo);
                    var wideNeg = m.Neg(dt, seq);
                    var sliceLo = m.Slice(dtLo, wideNeg, 0);
                    var sliceHi = m.Slice(dtHi, wideNeg, dtLo.BitSize);
                    ReplaceCondOfs(subNode, wideNeg);
                    Node.Replace(subNode, sliceHi);
                    Node.Replace(negLo, sliceLo);
                    Node.RemoveFromInputs(subNode);
                    return wideNeg;
                }
            }
            if (subLeft is UnaryNode negHi && 
                negHi.Operator == Operator.Neg &&
                subRight is ConversionNode conv &&
                conv.Inputs[1] is BinaryNode ucmp &&
                ucmp.Operator == Operator.Ult &&
                ucmp.Inputs[2] is ConstantNode zero &&
                zero.Value.IsZero &&
                ucmp.Inputs[1] is UnaryNode negLo2 &&
                negLo2.Operator == Operator.Neg)
            {
                var lo = negLo2.Inputs[1]!;
                var hi = negHi.Inputs[1]!;
                var dtLo = lo.DataType;
                var dtHi = hi.DataType;
                var dt = CombineTypes(dtHi, dtLo);
                var seq = m.Seq(dt, hi, lo);
                var wideNeg = m.Neg(dt, seq);
                var sliceLo = m.Slice(dtLo, wideNeg, 0);
                var sliceHi = m.Slice(dtHi, wideNeg, dtLo.BitSize);
                ReplaceCondOfs(subNode, wideNeg);
                Node.Replace(subNode, sliceHi);
                Node.Replace(negLo2, sliceLo);
                Node.RemoveFromInputs(subNode);
                return wideNeg;
            }
        }
        return null;
    }

    private bool TryFuseLongShiftRight(BinaryNode orNode)
    {
        if (orNode.Inputs.Count != 3 || orNode.Operator.Type != OperatorType.Or)
            return false;

        var left = orNode.Left;
        var right = orNode.Right;
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
        if (lowInput is not Node lowExpr || shiftAmount is null || highInput is not Node highExpr || spillAmount is null)
            return false;

        if (!MatchesComplementaryShiftAmount(spillAmount, shiftAmount, lowExpr.DataType.BitSize))
            return false;

        var highShift = FindMatchingHighShift(highInput, shiftAmount, lowShift);
        if (highShift is null)
            return false;

        var combinedType = CombineTypes(highExpr.DataType, lowExpr.DataType);
        var seq = m.Seq(combinedType, highInput, lowInput);
        var wideShift = m.Bin(combinedType, lowShift.Operator, null, seq, shiftAmount);
        var sliceLow = m.Slice(lowExpr.DataType, wideShift, 0);
        var sliceHigh = m.Slice(highExpr.DataType, wideShift, lowExpr.DataType.BitSize);

        replacements[orNode] = sliceLow;
        replacements[highShift] = sliceHigh;

        Node.Replace(orNode, sliceLow);
        Node.Replace(highShift, sliceHigh);

        return true;
    }

    private static bool TryGetLowShiftAndSpill(Node candidateLow, Node candidateSpill, out BinaryNode lowShift, out BinaryNode spillShift)
    {
        lowShift = null!;
        spillShift = null!;
        if (candidateLow is not BinaryNode lowOp || candidateSpill is not BinaryNode spillOp)
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
        if (spillAmount is not BinaryNode add || add.Operator.Type != OperatorType.IAdd)
            return false;
        if (add.Left is not UnaryNode neg || neg.Operator.Type != OperatorType.Neg)
            return false;
        if (!ReferenceEquals(neg.Expression, shiftAmount))
            return false;
        if (add.Right is not ConstantNode c)
            return false;
        return c.Value.ToUInt64() == (ulong)bitSize;
    }

    private BinaryNode? FindMatchingHighShift(Node highInput, Node shiftAmount, BinaryNode lowShift)
    {
        foreach (var user in highInput.Outputs)
        {
            if (ReferenceEquals(user, lowShift))
                continue;
            if (user is not BinaryNode op)
                continue;
            if (op.Operator.Type != lowShift.Operator.Type)
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
        if (highLeft is BinaryNode highLeftOp
            && highLeftOp.Operator.Type.IsAddOrSub())
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
        if (ea is BinaryNode add && add.Operator.Type == OperatorType.IAdd)
        {
            if (add.Left is not null && add.Right is ConstantNode c)
            {
                baseNode = add.Left;
                offset = unchecked((long)c.Value.ToUInt64());
                return true;
            }
            if (add.Right is not null && add.Left is ConstantNode c2)
            {
                baseNode = add.Right;
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

    private static PrimitiveType CombineTypes(DataType upper, DataType lower)
    {
        var totalBits = lower.BitSize + upper.BitSize;
        if (upper.IsWord)
            return PrimitiveType.CreateWord(totalBits);
        else
            return PrimitiveType.Create(upper.Domain, totalBits);
    }

    public Node? VisitAddressNode(AddressNode node) => null;
    public Node? VisitApplicationNode(ApplicationNode node) => null;
    public Node? VisitBinaryNode(BinaryNode node) => null;
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
    public Node? VisitUnaryNode(UnaryNode node) => null;
    public Node? VisitUseNode(UseNode node) => null;
}
