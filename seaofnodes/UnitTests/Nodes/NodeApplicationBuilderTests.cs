using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Nodes;

namespace Reko.Extras.SeaOfNodes.UnitTests.Nodes;

[TestFixture]
public class NodeApplicationBuilderTests
{
    [Test]
    public void Nab_Build_from_application_translates_callee_and_arguments()
    {
        var m = new ProcedureBuilder();
        var r1 = m.Reg32("r1", 1);
        var application = m.Fn(Reko.Core.Intrinsics.CommonOps.Abs, r1);
        var procedure = ((ProcedureConstant) application.Procedure).Procedure;

        var factory = new NodeFactory();
        var builder = new NodeApplicationBuilder(factory);
        var start = factory.CreateStartNode(m.Procedure);
        var callee = factory.ProcedureConstant(procedure);
        var argument = factory.CreateDefNode(start, r1.Storage, r1.DataType);

        var node = builder.Build(
            application,
            start,
            expression =>
            {
                if (ReferenceEquals(expression, application.Procedure))
                    return callee;
                if (ReferenceEquals(expression, application.Arguments[0]))
                    return argument;
                throw new InvalidOperationException($"Unexpected expression '{expression}'.");
            });

        Assert.That(node.DataType, Is.EqualTo(application.DataType));
        Assert.That(node.Inputs, Has.Count.EqualTo(3));
        Assert.That(node.Inputs[0], Is.SameAs(start));
        Assert.That(node.Inputs[1], Is.SameAs(callee));
        Assert.That(node.Inputs[2], Is.SameAs(argument));
        Assert.That(callee.Outputs, Contains.Item(node));
        Assert.That(argument.Outputs, Contains.Item(node));
    }

    [Test]
    public void Nab_Build_from_nodes_preserves_argument_order()
    {
        var m = new ProcedureBuilder();
        var application = m.Fn("mix", m.Word32(1), m.Word32(2));
        var procedure = ((ProcedureConstant) application.Procedure).Procedure;

        var factory = new NodeFactory();
        var builder = new NodeApplicationBuilder(factory);
        var start = factory.CreateStartNode(m.Procedure);
        var callee = factory.ProcedureConstant(procedure);
        var first = factory.Const(Constant.Word32(1));
        var second = factory.Const(Constant.Word32(2));

        var node = builder.Build(PrimitiveType.Word32, start, callee, first, second);

        Assert.That(node.Inputs, Has.Count.EqualTo(4));
        Assert.That(node.Inputs[0], Is.SameAs(start));
        Assert.That(node.Inputs[1], Is.SameAs(callee));
        Assert.That(node.Inputs[2], Is.SameAs(first));
        Assert.That(node.Inputs[3], Is.SameAs(second));
    }
}
