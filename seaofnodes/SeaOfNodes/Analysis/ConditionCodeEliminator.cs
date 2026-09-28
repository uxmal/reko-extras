#region License
/* 
 * Copyright (C) 1999-2026 John Källén.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; see the file COPYING.  If not, write to
 * the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.
 */
#endregion

using Reko.Core;
using Reko.Core.Analysis;
using Reko.Core.Collections;
using Reko.Core.Expressions;
using Reko.Core.Intrinsics;
using Reko.Core.Operators;
using Reko.Core.Services;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Nodes;
using System.Diagnostics;

namespace Reko.Extras.SeaOfNodes.Analysis;

/// <summary>
/// Removes any uses and definitions of condition codes.
/// </summary>
/// <remarks>
/// Removal of condition codes becomes exciting in situations like the following (x86 code):
///	<code>
///     add ax,bx
///     mov [si],ax
///     jnz foo
///	</code>
///	or
///	<code>
///	    cmp ax,0
///	    jl less
///	    jg greater
///	</code>
///	<para>
///	For best performance, preprocess the intermediate code with the ValuePropagator transformer
///	before using this transformation. Be sure to follow up with a <see cref="StoreFuser"/> pass
///	so that generated <see cref="MkSequence"/> expressions are propagated.
///	</para>
/// </remarks>
public class ConditionCodeEliminator : IAnalysis<ProcedureNode>
{
    private static readonly TraceSwitch trace = new TraceSwitch("CcodeEliminator", "Traces the progress of the condition code eliminator")
    {
        Level = TraceLevel.Warning,
    };

    private static readonly HashSet<OperatorType> isRealOperator =
    [
        OperatorType.FAdd,
        OperatorType.FSub,
        OperatorType.FMul,
        OperatorType.FDiv,
        OperatorType.FMod,

        OperatorType.FNeg,

        OperatorType.Feq,
        OperatorType.Fne,
        OperatorType.Flt,
        OperatorType.Fgt,
        OperatorType.Fle,
        OperatorType.Fge,
    ];

    private readonly NodeAnalysisContext context;

    /// <summary>
    /// Creates a new instance of <see cref="ConditionCodeEliminator"/>.
    /// </summary>
    /// <param name="context"><see cref="NodeAnalysisContext"/> to use.</param>
    public ConditionCodeEliminator(NodeAnalysisContext context)
    {
        this.context = context;
    }

    /// <inheritdoc/>
    public string Id => "cce";

    /// <inheritdoc/>
    public string Description => "Elimination of condition codes";


    /// <summary>
    /// Eliminates condition codes in the given SSA state.
    /// </summary>
    /// <param name="ssa">SSA stae of the procedure to transform.</param>
    /// <returns><inheritdoc/></returns>
    public (ProcedureNode, bool) Transform(ProcedureNode proc)
    {
        var w = CreateWorker(proc, context.PeepholeOptimizer);
        w.Transform();
        return (proc, w.Changed);
    }

    /// <summary>
    /// Creates an instance of the <see cref="ConditionCodeEliminator.Worker"/>
    /// class.
    /// </summary>
    /// <param name="proc"><see cref="ProcedureNode"/> of the procedure being transformed.
    /// </param>
    /// <returns>A <see cref="Worker"/> instance.
    /// </returns>
    public Worker CreateWorker(ProcedureNode proc, PeepholeOptimizer m)
    {
        return new Worker(context.Program, proc, m, context.EventListener);
    }

    /// <summary>
    /// Worker class that performs condition code elimination.
    /// </summary>
    public class Worker
    {
        private readonly ProcedureNode proc;
        private readonly IReadOnlyProgram program;
        private readonly IEventListener listener;
        private readonly PeepholeOptimizer m;
        private readonly HashSet<Identifier> aliases;
        private readonly Dictionary<(Node, ConditionCode), Node> generatedNodes;
        private readonly WorkList<Node> worklist;
        private readonly Dictionary<Node, PhiNode> phiNodes;

        /// <summary>
        /// Constructs an instance of the <see cref="Worker"/> class.
        /// </summary>
        /// <param name="program">Program in which the current SSA state is located.</param>
        /// <param name="proc">The <see cref="SsaState"/> of the procedure being transformed.</param>
        /// <param name="listener"><see cref="IEventListener"/> to report errors to.</param>
        public Worker(IReadOnlyProgram program, ProcedureNode proc, PeepholeOptimizer m, IEventListener listener)
        {
            this.proc = proc;
            this.m = m;
            this.program = program;
            this.listener = listener;
            this.aliases = [];
            this.generatedNodes = [];
            this.worklist = new WorkList<Node>();
            this.phiNodes = [];
            this.listener = listener;
        }

        internal bool Changed { get; set; }

        /// <summary>
        /// Performs condition code elimination on all <see cref="Identifier"/>s
        /// with a <see cref="FlagGroupStorage"/>.
        /// </summary>
        public void Transform()
        {
            this.worklist.AddRange(this.proc.CollectReachableNodes());
            while (worklist.TryGetWorkItem(out var s))
            {
                if (s is not CondNode && s.Storage is not FlagGroupStorage)
                    continue;
                var uses = ClosureOfUsingNodes(s);

                trace.Inform($"CCE: Tracing {s}: {uses}");
                foreach (var u in uses)
                {
                    trace.Inform("CCE:   used {0}", u);
                    var newNode = PropagateUsesTowardDefinitions(u, ConditionCode.None);
                    if (newNode is not null && newNode != u)
                    {
                        trace.Inform("CCE:    now {0}", newNode);
                        Node.Replace(u, newNode);
                    }
                    phiNodes.Clear();
                }
            }
        }

        private Node? PropagateUsesTowardDefinitions(Node use, ConditionCode cc)
        {
            switch (use)
            {
            case TestNode test:
                return PropagateUsesTowardDefinitions(test.Expression, test.ConditionCode);
            case CondNode cond:
                return UseConditionally(cond, cc);
            case SliceNode slice:   //$REVIEW: why slice used when masking with and is better?
                return PropagateUsesTowardDefinitions(slice.Expression, cc);
            case ApplicationNode { Procedure: ProcedureConstantNode pc }:
                //$TODO: use .IsInstanceOf
                if (pc.Procedure.Name == CommonOps.RorC.Name)
                {
                    throw new NotImplementedException($"Unimplemented: {use}");
                }
                if (pc.Procedure.Name == CommonOps.IAddC.Name)
                {
                    // Replace the IAddC with (a + b + (cy <= 0)) to
                    // model how carry works (on most architectures, carry is set
                    // if the result of an addition is less than either of the operands).
                    var cy = use.Inputs[4]!;
                    var cyNew = PropagateUsesTowardDefinitions(cy, ConditionCode.ULT);
                    cyNew ??= cy;
                    cyNew = m.Convert(cyNew, PrimitiveType.Bool, use.Inputs[2]!.DataType);
                    return Node.Replace(use, m.IAdd(m.IAdd(use.Inputs[2]!, use.Inputs[3]!), cyNew));
                }
                if (pc.Procedure.Name == CommonOps.ISubC.Name)
                {
                    var cy = use.Inputs[4]!;
                    var cyNew = PropagateUsesTowardDefinitions(cy, ConditionCode.ULT);
                    cyNew ??= cy;
                    cyNew = m.Convert(cyNew, PrimitiveType.Bool, use.Inputs[2]!.DataType);
                    return Node.Replace(use, m.ISub(m.ISub(use.Inputs[2]!, use.Inputs[3]!), cyNew));
                }
                break;
            case BinaryNode bin:
                Node? newBin = null;
                switch (bin.Operator.Type)
                {
                case OperatorType.Or:
                    if (bin.Right is ConstantNode)
                        return PropagateUsesTowardDefinitions(bin.Left, cc);
                    if (bin.Left is ConstantNode)
                        return PropagateUsesTowardDefinitions(bin.Right, cc);
                    var left = PropagateUsesTowardDefinitions(bin.Left, cc) ?? bin.Left;
                    var right = PropagateUsesTowardDefinitions(bin.Right, cc) ?? bin.Right;
                    newBin = this.m.Or(left, right);
                    return newBin;
                case OperatorType.And:
                    if (bin.Right is ConstantNode)
                        return PropagateUsesTowardDefinitions(bin.Left, cc);
                    left = PropagateUsesTowardDefinitions(bin.Left, cc);
                    right = PropagateUsesTowardDefinitions(bin.Right, cc);
                    if (left is not null)
                    {
                        if (right is not null)
                        {
                            return this.m.And(left, right);
                        }
                        else
                        {
                            return left;
                        }
                    }
                    else if (right is not null)
                    {
                        return right;
                    }
                    return newBin;
                case OperatorType.Eq:
                case OperatorType.Ge:
                case OperatorType.Gt:
                case OperatorType.Le:
                case OperatorType.Lt:
                case OperatorType.Ne:
                case OperatorType.Uge:
                case OperatorType.Ugt:
                case OperatorType.Ule:
                case OperatorType.Ult:
                    return bin;
                }
                break;
            case UseNode:
                return null;
            case PhiNode phi:
                if (this.phiNodes.TryGetValue(phi, out var newPhi))
                    return newPhi;
                // Add an argument-less phi to this.phiNodes to prevent
                // boundless recursion.
                newPhi = m.Phi(phi.DataType, phi.Inputs[0]!);
                this.phiNodes.Add(phi, newPhi);
                foreach (var input in phi.Inputs.Skip(1))
                {
                    var newInput = PropagateUsesTowardDefinitions(input!, cc) ?? input;
                    Node.AddEdge(newInput, newPhi);
                }
                return newPhi;
            }
            throw new NotImplementedException($"Unimplemented: {use}");
        }

        private Node UseConditionally(CondNode cond, ConditionCode cc)
        {
            Node left = cond.Expression;
            Node? right = null;
            bool isReal = false;
            if (left is BinaryNode bin)
            {
                isReal = isRealOperator.Contains(bin.Operator.Type);
                if (bin.Operator == Operator.ISub)
                {
                    left = bin.Left;
                    right = bin.Right;
                }
            }
            if (right is null)
            {
                //$TODO: what about reals?
                right = m.Const(left.DataType, 0);
            }
            Operator op;
            switch (cc)
            {
            case ConditionCode.EQ: op = isReal ? Operator.Feq : Operator.Eq; break;
            case ConditionCode.LE: op = isReal ? Operator.Fle : Operator.Le; break;
            case ConditionCode.LT: op = isReal ? Operator.Flt : Operator.Lt; break;
            case ConditionCode.GE: op = isReal ? Operator.Fge : Operator.Ge; break;
            case ConditionCode.GT: op = isReal ? Operator.Fgt : Operator.Gt; break;
            case ConditionCode.NE: op = isReal ? Operator.Fne : Operator.Ne; break;
            case ConditionCode.SG: op = Operator.Lt; break;
            case ConditionCode.NS: op = Operator.Ge; break;
            case ConditionCode.ULE: op = Operator.Ule; break;
            case ConditionCode.ULT: op = Operator.Ult; break;
            case ConditionCode.UGE: op = Operator.Uge; break;
            case ConditionCode.UGT: op = Operator.Ugt; break;
            case ConditionCode.OV:
                return ComparisonFromOverflow(cond.Expression, false);
            case ConditionCode.NO:
                return ComparisonFromOverflow(cond.Expression, true);
            case ConditionCode.IS_NAN:
                return OrderedComparison(cond.Expression, false);
            case ConditionCode.NOT_NAN:
                return OrderedComparison(cond.Expression, true);

            default:
                trace.Verbose("Cond used directly.");
                return cond;
            }
            return m.Bin(PrimitiveType.Bool, op, null, left, right);
        }

        private HashSet<Node> ClosureOfUsingNodes(Node s)
        {
            static bool IsPropagatable(Node n)
            {
                if (n is BinaryNode bin)
                {
                    if (bin.Operator == Operator.And &&
                        bin.Right is ConstantNode)
                        return true;
                    if (bin.Operator == Operator.Or)
                        return true;
                }
                if (n is UnaryNode u)
                    return true;
                if (n is PhiNode)
                    return true;
                return false;
            }
            var visited = new HashSet<Node>();
            var uses = new HashSet<Node>();
            var wl = new WorkList<Node>(s.Outputs);
            while (wl.TryGetWorkItem(out var use))
            {
                if (!visited.Add(use))
                    continue;
                if (IsPropagatable(use))
                {
                    wl.AddRange(use.Outputs);
                }
                else
                {
                    uses.Add(use);
                }
            }
            return uses;
        }

        private Node ComparisonFromOverflow(Node bin, bool isNegated)
        {
            var intrinsic = CommonOps.Overflow.MakeInstance(bin.DataType);
            Node e = m.Fn(
                PrimitiveType.Bool,
                null,
                intrinsic,
                bin);
            if (isNegated)
            {
                e = m.Not(e);
            }
            return e;
        }

        /// <summary>
        /// Generate a comparison using the standard C
        /// "isunordered" function. 
        /// </summary>
        public Node OrderedComparison(Node node, bool isNegated)
        {
            Node left;
            Node right;
            if (node is BinaryNode bin)
            {
                left = bin.Left;
                right = bin.Right;
            }
            else
            {
                left = node;
                right = m.Const(Constant.Zero(node.DataType));
            }
            var sig = new FunctionType(
                [
                    new Identifier("x", left.DataType, null!),
                    new Identifier("y", right.DataType, null!)
                ],
                [
                    new Identifier("", PrimitiveType.Bool, null!)
                ]);
            Node e = m.Fn(
                PrimitiveType.Bool,
                null,
                new IntrinsicProcedure("isunordered", false, sig),
                left,
                right);
            if (isNegated)
            {
                e = m.Not(e);
            }
            return e;
        }
    }
}
