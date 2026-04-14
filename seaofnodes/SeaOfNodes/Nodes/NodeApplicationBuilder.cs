using Reko.Core.Expressions;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class NodeApplicationBuilder
{
    private readonly NodeFactory factory;

    public NodeApplicationBuilder(NodeFactory factory)
    {
        this.factory = factory;
    }

    public ApplicationNode Build(Application application, Node? cfNode, Func<Expression, Node> translateExpression)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(translateExpression);

        var callee = translateExpression(application.Procedure);
        var arguments = application.Arguments.Select(translateExpression).ToArray();
        return Build(application.DataType, cfNode, callee, arguments);
    }

    public ApplicationNode Build(DataType dataType, Node? cfNode, Node callee, params Node[] arguments)
    {
        ArgumentNullException.ThrowIfNull(dataType);
        ArgumentNullException.ThrowIfNull(callee);
        ArgumentNullException.ThrowIfNull(arguments);

        return (ApplicationNode) factory.Apply(dataType, cfNode, callee, arguments);
    }
}
