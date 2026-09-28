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
using Reko.Core.Code;
using Reko.Core.Expressions;
using Reko.Core.Operators;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Nodes;
using System;
using System.Collections.Generic;

namespace Reko.Extras.SeaOfNodes.Analysis;

/// <summary>
/// Looks at segmented pointer uses, to see if they always are associated
/// with the same base pointer / segment selector. If so, they can be 
/// treated as a pointer.
/// </summary>
public class SegmentedAccessClassifier : AbstractNodeVisitor
{
    private readonly ProcedureNode procNode;
    private readonly Dictionary<Node, Node> assocs;
    private readonly Dictionary<Node, Constant> consts;
    private readonly Node overAssociatedId = new DefNode(
        400,
        new TemporaryStorage("overAssociated", 40, VoidType.Instance),
        VoidType.Instance);
    private readonly Constant overAssociatedConst = Constant.Real64(0.0);

    /// <summary>
    /// Creates an instance of a <see cref="SegmentedAccessClassifier"/>
    /// working over the provded <see cref="ProcedureNode"> Value node graph.</see>
    /// </summary>
    /// <param name="procNode">Value node graph to work on.</param>
    public SegmentedAccessClassifier(ProcedureNode procNode)
    {
        this.procNode = procNode;
        assocs = [];
        consts = [];
    }

    /// <summary>
    /// Associates a base pointer identifier <paramref name="basePtr"/> with an 
    /// offset identifier (think "es" and "bx").
    /// </summary>
    public void Associate(Node basePtr, Node membPtr)
    {
        if (consts.ContainsKey(basePtr))
        {
            // If basePtr is already associated with a constant,
            // it is over-associated.
            assocs[basePtr] = overAssociatedId;
            consts[basePtr] = overAssociatedConst;
            return;
        }

        if (!assocs.TryGetValue(basePtr, out Node? a))
            assocs[basePtr] = membPtr;
        else if (a != membPtr)
            assocs[basePtr] = overAssociatedId;
        else
            assocs[basePtr] = membPtr;
    }

    /// <summary>
    /// Associates the segment selector <paramref name="basePtr"/> with a constant
    /// offset <paramref name="memberPtr"/>.
    /// </summary>
    public void Associate(Node basePtr, ConstantNode memberPtr)
    {
        if (assocs.ContainsKey(basePtr))
        {
            assocs[basePtr] = overAssociatedId;
            consts[basePtr] = overAssociatedConst;
            return;
        }
        consts[basePtr] = memberPtr.Value;
    }

    /// <summary>
    /// Given an identifier <paramref name="pointer"/>, returns the associated
    /// identifier.
    /// </summary>
    /// <param name="pointer"></param>
    /// <returns></returns>

    public Node? AssociatedIdentifier(Node pointer)
    {
        if (assocs.TryGetValue(pointer, out Node? id))
        {
            return (id != overAssociatedId) ? id : null;
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Classifies all segmented pointer accesses in the procedure.
    /// </summary>
    public void Classify()
    {
        foreach (var node in procNode.CollectReachableNodes())
        {
            node.Accept(this);
        }
    }

    /// <summary>
    /// Returns true if the given identifier <paramref name="pointer"/> is
    /// only associated with constants, i.e. it is not associated with another
    /// identifier.
    /// </summary>
    /// <param name="pointer"></param>
    /// <returns></returns>
    public bool IsOnlyAssociatedWithConstants(Node pointer)
    {
        return (consts.TryGetValue(pointer, out Constant? c) &&
                c != overAssociatedConst);
    }


    #region InstructionVisitorMembers

    /// <inheritdoc/>
    public override void VisitSegmentedPointerNode(SegmentedPointerNode segptr)
    {
        var pointer = segptr.Base;
        switch (segptr.Offset)
        {
        case BinaryNode bin:
            if (bin.Operator.Type == OperatorType.IAdd && bin.Right is ConstantNode)
            {
                Associate(pointer, bin.Left);
            }
            return;
        case ConstantNode c:
            Associate(pointer, c);
            return;
        default:
            Associate(pointer, segptr.Offset);
            break;
        }
    }

    #endregion
}
