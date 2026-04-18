using Reko.Analysis;
using Reko.Core.Expressions;
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
    r1_11 = r1 + 5<32>
    return
ProcedureBuilder_exit:
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

    [Test]
    public void Nvp_ConditionCodeElimination()
    {
            string sExpected =
            #region Expected
                @"
ProcedureBuilder_entry:
    def r:word32
l1:
    n13 = r == 3<32>
    if (n13) goto m_skip
l2:
    return
m_skip:
    return
ProcedureBuilder_exit:
";
            #endregion
        
        RunTest(sExpected, m =>
        {
			var r = m.Reg32("r", 1);
			var z = m.Flags("Z");  // is a condition code.
            var y = m.Flags("C");  // is a condition code.

            m.Assign(z, m.Cond(z.DataType, m.ISub(r, 3)));
            m.Assign(y, z);

			m.BranchIf(m.Test(ConditionCode.EQ, y), "m_skip");
            m.Return();
            m.Label("m_skip");
            m.Return();
        });
    }
}
