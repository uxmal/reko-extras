using Reko.Core.Expressions;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class GvnEqualityComparer : IEqualityComparer<Node>, INodeVisitor<int>
{
    private readonly DataTypeComparer tycomp = new();
    private readonly ExpressionValueComparer ecmp = new();

    public bool Equals(Node? x, Node? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;
        return Equals(x, y, false);
    }

    private bool Equals(Node x, Node y, bool valueEquality)
    {
        if (!tycomp.Equals(x.DataType, y.DataType))
            return false;

        return (x, y) switch
        {
            (ConstantNode cx, ConstantNode cy) =>
                ecmp.Equals(cx.Value, cy.Value),
            (OperationNode bx, OperationNode by) =>
                bx.Operator == by.Operator &&
                ValueEquals(bx.Inputs, by.Inputs),
            (Node nnx, Node nny) =>
                throw new NotImplementedException($"Equality comparison not implemented for {nnx.GetType().Name} and {nny.GetType().Name}."),
            _ => false
        };
    }

    private bool ValueEquals<T>(List<T> x, List<T> y) where T : Node?
    {
        if (x.Count != y.Count)
            return false;
        for (int i = 0; i < x.Count; i++)
        {
            if (x[i] is null)
            {
                if (y[i] is not null)
                    return false;
            }
            else if (y[i] is null)
            {
                if (x[i] is not null)
                    return false;
            }
            if (!Equals(x[i]!, y[i]!, true))
                return false;
        }
        return true;
    }

    public int GetHashCode(Node node)
    {
        if (node.CachedHashCode == 0)
        {
            node.CachedHashCode = node.Accept(this);
        }
        return node.CachedHashCode;
    }

    public int VisitAddressNode(AddressNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitApplicationNode(ApplicationNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitBlockNode(BlockNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitCallNode(CallNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitCondNode(CondNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitConstantNode(ConstantNode node)
    {
        return ecmp.GetHashCode(node.Value);
    }

    public int VisitConversionNode(ConversionNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitDefNode(DefNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitEndNode(EndNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitIfNode(IfNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitLoadNode(LoadNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitMemoryNode(MemoryNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitOperationNode(OperationNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitOutArgumentNode(OutArgumentNode outArgumentNode)
    {
        throw new NotImplementedException();
    }

    public int VisitPhiNode(PhiNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitProcedureConstantNode(ProcedureConstantNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitReturnNode(ReturnNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitSeqNode(SeqNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitSideEffectNode(SideEffectNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitSliceNode(SliceNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitStartNode(StartNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitStoreNode(StoreNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitStringNode(StringNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitSwitchNode(SwitchNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitTestNode(TestNode node)
    {
        throw new NotImplementedException();
    }

    public int VisitUseNode(UseNode node)
    {
        throw new NotImplementedException();
    }
}
