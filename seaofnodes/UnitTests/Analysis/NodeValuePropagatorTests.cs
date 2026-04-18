using Reko.Analysis;
using Reko.Extras.SeaOfNodes.Analysis;
using Reko.Extras.SeaOfNodes.Nodes;

namespace Reko.Extras.SeaOfNodes.UnitTests.Analysis;

[TestFixture]
public class NodeValuePropagatorTests
{
    private readonly ProgramDataFlow programFlow;

    public NodeValuePropagatorTests()
    {
        this.programFlow = new ProgramDataFlow();
    }

    private void RunTest(string sExpected, Action<ProcedureBuilder> build)
    {
        var m = new ProcedureBuilder();
        build(m);
        var factory = new NodeFactory();
        var builder = new NodeRepresentationBuilder(factory, programFlow);
        var graph = builder.Transform(m.Procedure);
        var nvp = new NodeValuePropagator(factory);
        nvp.Transform(graph);
        var renderer = new NodeGraphRenderer();
        var sw = new StringWriter();
        sw.WriteLine();
        renderer.Render(graph, sw);
        var actual = sw.ToString();
        var expectedNorm = sExpected.Replace("\r\n", "\n");
        var actualNorm = actual.Replace("\r\n", "\n");
        if (actualNorm != expectedNorm)
        {
            Console.WriteLine(actual);
        }
        Assert.That(actualNorm, Is.EqualTo(expectedNorm));
    }

    [Test]
    public void Nvp_Add_Const_Const()
    {
        string sExpected =
        #region Expected
        @"
ProcedureBuilder_entry:
    def r1:word32
l1:
    return
ProcedureBuilder_exit:
    r1_11 = r1 + 5<32>
    use r1:r1_11
";
        #endregion

        RunTest(sExpected, m =>
        {
            var r1 = m.Reg32("r1", 1);

            m.Assign(r1, m.IAdd(r1, m.Word32(2)));
            m.Assign(r1, m.IAdd(r1, m.Word32(3)));
            m.Return();
        });
    }
}
