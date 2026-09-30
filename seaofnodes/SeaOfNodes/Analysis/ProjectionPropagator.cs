using Reko.Core;
using Reko.Core.Analysis;
using Reko.Core.Code;
using Reko.Core.Collections;
using Reko.Core.Expressions;
using Reko.Core.Lib;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Nodes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.SeaOfNodes.Analysis;

/// <summary>
/// The purpose of this class is to resolve projections to make for cleaner
/// code. We have widening projections and narrowing projections to take care of.
/// Widening projections come in two kinds:
/// * Sequences where we use adjacent pieces of the same register:
///     de = SEQ(h, l)
/// * Sequences where we use two separate registers as a whole register.
///     es_bx = SEQ(dx, ax)
/// * Sequences where we use adjacent parts of the stack:
///     dwLoc0010 = SEQ(wLoc0012, wLoc0010)
/// * Sequences where we use adjacent parts of memory
///     es_bx = SEQ(Mem11[0x0234:word16],Mem11[0x0232:word16])
/// We convert SEQ(reg1,reg2) to either the widened register (i.e. hl)
/// the combined register dx_ax, or a widenened memory access, then "push" the widened
/// register to all the statements that use both halves.
/// 
/// Narrowing projections are casts or slices:
///     al = (byte) rax
///     bh = SLICE(rbx, 8, 8)
/// </summary>

public class ProjectionPropagator
{
    private static readonly TraceSwitch trace = new TraceSwitch(nameof(ProjectionPropagator), "Traces projection propagator") { Level = TraceLevel.Warning };

    private NodeAnalysisContext ctx;

    /// <summary>
    /// Constructs an instance of <see cref="ProjectionPropagator"/>.
    /// </summary>
    /// <param name="context"><see cref="AnalysisContext"/> to use.</param>
    public ProjectionPropagator(NodeAnalysisContext ctx)
    {
        this.ctx = ctx;
    }

    /// <inheritdoc/>
    public string Id => "prpr";

    /// <inheritdoc/>
    public string Description => "Propagates slices and sequences, trying to build larger values";

    /// <inheritdoc/>
    public (ProcedureNode, bool) Transform(ProcedureNode procNode)
    {
        var sac = new SegmentedAccessClassifier(procNode);
        sac.Classify();
        var worker = new Worker(procNode, sac, ctx);
        bool changed = worker.Transform();
        return (procNode, changed);
    }

    private class Worker : InstructionTransformer
    {
        private readonly SegmentedAccessClassifier sac;
        private readonly ProcedureNode procNode;
        private readonly NodeAnalysisContext ctx;
        public bool changed;

        public Worker(ProcedureNode procNode, SegmentedAccessClassifier sac, NodeAnalysisContext ctx)
        {
            this.procNode = procNode;
            this.sac = sac;
            this.ctx = ctx;
        }

        public bool Transform()
        {
            var wl = WorkList.Create(procNode.CollectReachableNodes());
            while (wl.TryGetWorkItem(out var node))
            {
                var prjf = new ProjectionFilter(procNode, node, sac, ctx);
                var instr = node.Accept(prjf);
                wl.AddRange(prjf.NewStatements);
                changed |= prjf.Changed;
            }
            return changed;
        }

        private class ProjectionFilter : INodeVisitor<Node>
        {
            private readonly IProcessorArchitecture arch;
            private readonly ProcedureNode procNode;
            private readonly SegmentedAccessClassifier sac;
            private readonly GvnEqualityComparer cmp;
            private readonly PeepholeOptimizer m;

            public ProjectionFilter(
                ProcedureNode procNode,
                Node node, 
                SegmentedAccessClassifier sac,
                NodeAnalysisContext ctx)
            {
                this.procNode = procNode;
                this.Statement = node;
                this.sac = sac;
                this.arch = procNode.Procedure.Architecture;
                this.NewStatements = [];
                this.cmp = new GvnEqualityComparer();
                this.m = ctx.PeepholeOptimizer;
            }

            public bool Changed { get; private set; }
            public HashSet<Node> NewStatements { get; }
            public Node Statement { get; private set; }

            private static bool AllSame<T>(T[] items, Func<T, T, bool> cmp)
            {
                var first = items[0];
                for (int i = 1; i < items.Length; ++i)
                {
                    if (!cmp(first, items[i]))
                        return false;
                }
                return true;
            }

            /// <summary>
            /// Returns true if all the slices are slicing the same storage and 
            /// all the slices are bitwise adjacent.
            /// </summary>
            /// <param name="slices"></param>
            private static bool AllAdjacent(SliceNode[] slices)
            {
                int lsbLast = (int)slices[0].Offset;
                for (int i = 1; i < slices.Length; ++i)
                {
                    if (lsbLast != (int)slices[i].Offset + (int)slices[i].DataType.BitSize)
                        return false;
                    lsbLast = (int)slices[i].Offset;
                }
                return true;
            }

            public Node VisitAddressNode(AddressNode a) => a;
            public Node VisitApplicationNode(ApplicationNode a) => a;
            public Node VisitBinaryNode(BinaryNode a) => a;
            public Node VisitBlockNode(BlockNode a) => a;
            public Node VisitCallNode(CallNode a) => a;
            public Node VisitCastNode(CastNode a) => a;
            public Node VisitCondNode(CondNode a) => a;
            public Node VisitConstantNode(ConstantNode a) => a;
            public Node VisitConversionNode(ConversionNode c) => c;
            public Node VisitDefNode(DefNode c) => c;
            public Node VisitDereferenceNode(DereferenceNode c) => c;
            public Node VisitEndNode(EndNode c) => c;
            public Node VisitIfNode(IfNode c) => c;
            public Node VisitLoadNode(LoadNode c) => c;
            public Node VisitMemberPointerSelectorNode(MemberPointerSelectorNode c) => c;
            public Node VisitMemoryNode(MemoryNode c) => c;
            public Node VisitOutArgumentNode(OutArgumentNode c) => c;
            public Node VisitPhiNode(PhiNode c) => c;
            public Node VisitProcedureConstantNode(ProcedureConstantNode c) => c;
            public Node VisitStartNode(ProcedureNode c) => c;
            public Node VisitReturnNode(ReturnNode c) => c;
            public Node VisitSideEffectNode(SideEffectNode c) => c;
            public Node VisitSliceNode(SliceNode c) => c;
            public Node VisitStoreNode(StoreNode c) => c;
            public Node VisitStringNode(StringNode c) => c;
            public Node VisitTestNode(TestNode c) => c;
            public Node VisitSwitchNode(SwitchNode c) => c;
            public Node VisitUnaryNode(UnaryNode c) => c;
            public Node VisitUseNode(UseNode c) => c;
            public Node VisitSegmentedPointerNode(SegmentedPointerNode access)
            {
                var sidSeg = access.Base;
                if (sac.AssociatedIdentifier(sidSeg) is null)
                    return access;
                if (access.Offset is BinaryNode binEa)
                {
                    var sidLeft = binEa.Left;
                    var e = FuseIdentifiers(MakeSegPtr(sidLeft.DataType), sidSeg, sidLeft);
                    if (e is not null)
                    {
                        var r = Extend(binEa.Right, e.DataType);
                        return m.Bin(
                                e.DataType,
                                binEa.Operator,
                                null,
                                e,
                                r);
                    }

                    var sidRight = binEa.Right;
                    e = FuseIdentifiers(MakeSegPtr(sidRight.DataType), sidSeg, sidRight);
                    if (e is not null)
                    {
                        var l = Extend(binEa.Left, e.DataType);
                        return m.Bin(l.DataType, binEa.Operator, null, l, e);
                    }
                }
                else
                {
                    var sidEa = access.Offset;
                    var sids = new[] { sidSeg, sidEa };
                    var e = FuseIdentifiers(MakeSegPtr(sidEa.DataType), sids);
                    if (e is not null)
                        return e;
                }
                return access;
            }

            private Node Extend(Node e, DataType dataType)
            {
                var dtExtended = PrimitiveType.Create(e.DataType.Domain, dataType.BitSize);
                if (e is ConstantNode c)
                {
                    return m.Const(dtExtended, c.Value.ToInt64());
                }
                else
                {
                    return m.Convert(e, e.DataType, dtExtended);
                }
            }

            private static DataType? MakeSegPtr(DataType dtEa)
            {
                if (dtEa is MemberPointer mptr)
                    return new PointerType(mptr.Pointee, 32);
                else
                    return PrimitiveType.SegPtr32;
            }

            public Node VisitSeqNode(SeqNode seq)
            {
                Debug.Assert(seq.Inputs.Count > 1);
                var sids = seq.Inputs.Skip(1).ToArray();
                var expFused = FuseIdentifiers(null, sids!);
                return expFused ?? seq;
            }

            /// <summary>
            /// Attempt to fuse together the definitions of all the identifiers in <paramref name="sids"/>.
            /// </summary>
            /// <param name="dtWide">The data type of the result.</param>
            /// <param name="sids">Identifiers to fuse.</param>
            /// <returns>A new expression if the fusion succeeded, otherwise null.</returns>
            private Node? FuseIdentifiers(DataType? dtWide, params Node[] sids)
            {
                // Are all the definitions of the ids in the same basic block? If they're
                // not, we give up.
                //if (!AllSame(sids, (a, b) =>
                //    a.DefStatement?.Block == b.DefStatement?.Block &&
                //    a.Identifier.Storage.GetType() == b.Identifier.Storage.GetType()))
                //    return null;

                if (dtWide is null)
                {
                    // Caller has no opinion about the resulting data type, so fall back
                    // on word.
                    dtWide = PrimitiveType.CreateWord(sids.Sum(s => s.DataType.BitSize));
                }

                Storage? idWide = GenerateWideIdentifier(dtWide, sids);
                if (idWide is not null && idWide.DataType.BitSize != dtWide.BitSize)
                    return null;
                
                    // All assignments. Are they all slices?
                    var slices = sids.Select(AsSlice).ToArray();
                    if (slices.All(s => s is not null))
                    {
                        if (AllSame(slices, (a, b) => cmp.Equals(a?.Expression, b?.Expression)) &&
                            AllAdjacent(slices!))
                        {
                            trace.Verbose("Prpr: Fusing slices in {0}", procNode.Procedure.Name);
                            trace.Verbose("{0}", string.Join(Environment.NewLine, sids.Select(a => $"    {a}")));
                            Changed = true;
                            return RewriteSeqOfSlices(dtWide, sids, slices!);
                        }
                    }

                if (idWide is not null)
                {
                    var defs = sids.Select(s => s as DefNode).ToArray();
                    if (defs.All(d => d is not null))
                    {
                        // All the identifiers are defined by def statements.
                        Changed = true;
                        return RewriteSeqOfDefs(defs!, idWide);
                    }

                    var phis = sids.Select(s => s as PhiNode).ToArray();
                    if (phis.All(a => a is not null))
                    {
                        // We have a sequence of phi functions
                        Changed = true;
                        return RewriteSeqOfPhi(sids, phis!, idWide);
                    }
                    /*
                    if (sids[0] is CallInstruction call &&
                        sids.All(s => s.DefStatement == sids[0].DefStatement))
                    {
                        // All of the identifiers in the sequence were defined by the same call.
                        Changed = true;
                        return RewriteSeqDefinedByCall(sids, call, idWide);
                    }
                    */
                }
                trace.Warn("Prpr: Couldn't fuse statements in {0}", procNode.Procedure.Name);
                trace.Warn("{0}", string.Join(Environment.NewLine, sids.Select(s => $"    {s}")));
                return null;
            }

            private Storage? GenerateWideIdentifier(DataType dt, Node[] sids)
            {
                var sd = sids[0].Storage.Domain;
                Storage? regWide;
                if (AllSame(sids, (a, b) => a.Storage.Domain == b.Storage.Domain))
                {
                    var bits = sids.Aggregate(
                        new BitRange(),
                        (br, sid) => br | sid.Storage.GetBitRange());
                    regWide = arch.GetRegister(sd, bits);
                }
                else if (sids.All(sid => sid.Storage is StackStorage))
                {
                    regWide = CombineAdjacentStorages(sids);
                }
                else
                {
                    var id =  procNode.Procedure.Frame.EnsureSequence(
                        dt,
                        sids.Select(s => s.Storage).ToArray());
                    regWide = id.Storage;
                }
                return regWide;
            }

            private Storage? CombineAdjacentStorages(Node[] sids)
            {
                var stgs = sids.Select(s => (StackStorage)s.Storage)
                    .OrderBy(s => s.StackOffset)
                    .ToArray();
                int byteOffsetMin = stgs[0].StackOffset;
                int byteOffsetMax = byteOffsetMin + (int)stgs[0].DataType.Size;
                for (int i = 1; i < stgs.Length; ++i)
                {
                    if (stgs[i].StackOffset != byteOffsetMax)
                        return null;
                    byteOffsetMax += (int)stgs[i].DataType.Size;
                }
                var word = PrimitiveType.CreateWord(DataType.BitsPerByte * (byteOffsetMax - byteOffsetMin));
                var x = procNode.Procedure.Frame.EnsureStackVariable(byteOffsetMin, word);
                return x.Storage;
            }

            /// <summary>
            /// Given an sequence of adjacent slices, rewrite the sequence a single
            /// slice. If the slice is a no-op, just return the underlying storage.
            /// </summary>
            /// <param name="dtSequence">The <see cref="DataType"/> of the resulting
            /// expression.</param>
            /// <param name="sids">The <see cref="Node"/>s comprising 
            /// the sequenced, arranged in big-endian order.</param>
            /// <param name="slices">The corresponding <see cref="Slice"/> expressions
            /// arrangend in the same order as <paramref name="sids"/>.
            /// </param>
            /// <returns>A simplified expression.
            /// </returns>
            /// <remarks>
            /// We have:
            /// <code>
            ///  sid_1 = SLICE(sid_0,...)
            ///  sid_2 = SLICE(sid_0,...)
            ///  ...
            ///  ...SEQ(sid_1, sid_2)
            /// </code>
            /// We want:
            /// <code>
            /// ...SLICE(sid_0, ...)
            /// </code>
            /// and ideally
            /// <code>
            ///...sid_0
            /// </code>
            /// </remarks>

            private Node RewriteSeqOfSlices(DataType dtSequence, Node[] sids, SliceNode[] slices)
            {
                var totalSliceSize = slices.Sum(s => s.DataType.BitSize);
                var totalSliceOffset = slices[^1].Offset;
                var expWide = slices[0].Expression;
                if (expWide.Storage is not null)
                {
                    if ((int)expWide.Storage.BitAddress == totalSliceOffset &&
                        (int)expWide.Storage.BitSize == totalSliceSize)
                    {
                        return expWide;
                    }
                }
                else
                {
                    if (expWide.DataType.BitSize == totalSliceSize)
                    {
                        return expWide;
                    }
                }
                return m.Slice(expWide, dtSequence, totalSliceOffset);
            }

            private SliceNode? AsSlice(Node? ass)
            {
                if (ass is null)
                    return null;
                if (ass is SliceNode slice)
                    return slice;
                if (ass is CastNode cast)
                    return (SliceNode) m.Slice(cast.Expression, cast.DataType, 0);
                return null;
            }

            private DefNode RewriteSeqOfDefs(DefNode[] sids, Storage idWide)
            {
                // We have:
                //  def a
                //  def b
                //  ....SEQ(a, b)
                // We want:
                //  def a_b
                //  a = SLICE(a_b...)
                //  b = SLICE(a_b...)
                //  ....a_b
                // It's likely that the a = SLICE(...) statements
                // will be dead after this transformation, which
                // DeadCode.Eliminate will discover.

                // Add a def for the wide register, placing it "above" or "before"
                // the narrow 'defs', which will be mutated to slices.
                var sidWide = m.Def(sids[0].Inputs[0]!, idWide);

                // Replace uses of the individual defs.
                foreach (var s in sids)
                {
                    s.Outputs.Remove(this.Statement);
                    var slice = m.Slice(
                        sidWide,
                        s.DataType,
                        idWide.OffsetOf(s.Storage!));
                    Node.Replace(s, slice);
                }
                return sidWide;
            }

            /// <summary>
            /// We have
            /// <code>
            ///     a_3 = PHI(a_1, a_2)
            ///     b_3 = PHI(b_1, a_2)
            ///     ...SEQ(a_3,b_3)
            /// </code>
            /// and we want 
            /// <code>
            ///    ab_3 = PHI(ab_1, ab_2)
            ///    a_3 = SLICE(ab_3, ...)
            ///    b_3 = SLICE(ab_3, ...)
            ///    ...ab_3
            /// </code>
            /// </summary>
            private Node RewriteSeqOfPhi(Node[] sids, PhiNode[] phis, Storage idWide)
            {
                // Insert a PHI statement placeholder at the beginning
                // of the basic block.
                var stmPhi = m.Phi(idWide.DataType, phis[0].Inputs[0]!);

                // Generate fused identifiers for all phi slots.
                var widePhiArgs = new List<Node>();
                throw new NotImplementedException();
                /*
                for (var iBlock = 0; iBlock < phis[0].Src.Arguments.Length; ++iBlock)
                {
                    // Make a fused identifier in each predecessor block and "push"
                    // the SEQ statements into the predecessors.
                    var pred = phis[0].Src.Arguments[iBlock].Block;
                    var sidPred = MakeFusedIdentifierInPredecessorBlock(sids, phis, idWide, stmPhi, iBlock, pred);
                    widePhiArgs.Add(sidPred);
                }

                stmPhi.Inputs.AddRange(widePhiArgs);

                // Replace all the "unfused" phis with slices of the "fused" phi.
                foreach (var sid in sids)
                {
                    var slice = m.Slice(
                        stmPhi,
                        sid.DataType,
                        idWide.Storage!.OffsetOf(sid.Storage!));
                    Node.Replace(sid, slice);
                }
                return stmPhi;
                */
            }

            /// <summary>
            /// Given a sequence of SSA identifiers in <paramref name="sids"/>, generate
            /// add a new SSA identifier whose definition is sid_new = SEQ(sids...) and
            /// place it in the basic block <paramref name="pred"/>.
            /// </summary>
            /// <remarks>
            /// If the SSA identifiers all all slices of the same identifier, short-circuit
            /// the work by using the sliced identifier directly.
            /// </remarks>
            private Node MakeFusedIdentifierInPredecessorBlock(Node[] sids, PhiAssignment[] phis, Identifier idWide, Statement stmPhi, int iBlock, Block pred)
            {
                throw new NotImplementedException();
                /*
                SsaIdentifier sidPred;
                var sidPreds = phis.Select(p => procNode.Identifiers[(Identifier)p.Src.Arguments[iBlock].Value].DefStatement?.Instruction as Assignment).ToArray();
                var slices = sidPreds.Select(AsSlice).ToArray();
                var aliases = sidPreds.Select(s => s as AliasAssignment).ToArray();
                if (slices.All(s => s is not null) &&
                    AllSame(slices, (a, b) => this.cmp.Equals(a!.Expression, b!.Expression)) &&
                    AllAdjacent(slices!))
                {
                    if (slices[0]!.Expression is Identifier id)
                    {
                        // All sids were slices of the same identifier `id`,
                        // so just use that instead.
                        sidPred = procNode.Identifiers[id];
                        sidPred.Uses.Add(stmPhi);
                        return sidPred;
                    }
                }

                var stmPred = AddStatementToEndOfBlock(pred, sids[0].DefStatement.Address, null!);
                sidPred = procNode.Identifiers.Add(idWide, stmPred, false);
                var phiArgs = phis.Select(p => p.Src.Arguments[iBlock].Value).ToArray();
                stmPred.Instruction =
                    new AliasAssignment(
                        sidPred.Identifier,
                        m.Seq(
                            sidPred.Identifier.DataType,
                            phiArgs));
                this.NewStatements.Add(stmPred);
                procNode.AddUses(stmPred);

                sidPred.Uses.Add(stmPhi);
                return sidPred;
                */
            }

            private Node RewriteSeqDefinedByCall(Node[] sids, CallNode callStm, Node idWide)
            {
                // We have:
                // call
                //    def: a_1, b_2
                // ...
                // ...SEQ(a_1, b_2)
                // We want 
                // call
                //    def: a_b_3
                // a_1 = SLICE(a_b_3, ...)
                // b_2 = SLICE(a_b_3, ...)
                // ...
                // ...a_b_3

                // Create an SSA ID for the fused identifier and replace the
                // definitions of its parts with a single definition of the fused identifier
                //var sidDst = procNode.Identifiers.Add(idWide, callStm, false);
                callStm.Outputs.Add(m.Def(callStm, idWide.Storage!));

                // Add alias assignments for the sub-identifiers after the call statement.
                foreach (var s in sids)
                {
                    var slice = m.Slice(
                        idWide,
                        s.DataType, 
                        idWide.Storage!.OffsetOf(s.Storage!));
                    Node.Replace(s, slice);
                }
                return idWide;
            }
        }
    }
}
