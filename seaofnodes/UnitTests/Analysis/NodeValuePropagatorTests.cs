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
        var builder = new NodeGraphBuilder(factory, programFlow, m.Architecture);
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
    r1_10 = r1 + 5<32>
    return
ProcedureBuilder_exit:
    use r1:r1_10
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
    v12 = r == 3<32>
    if (v12) goto m_skip
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

    [Test]
    public void Nvp_ConditionCodeElimination_Phi2()
    {
        string sExpected =
        #region Expected
            @"
ProcedureBuilder_entry:
    def r1:word32
l1:
    v11 = r1 < 0<32>
    if (v11) goto m2_skip
m1:
    v17 = r1 - 3<32>
    Z_18 = cond(v17)
    v25 = r1 == 3<32>
    goto m3_join
m2_skip:
    v14 = r1 - 0xFFFFFFFC<32>
    Z_15 = cond(v14)
    v26 = r1 == 0xFFFFFFFC<32>
m3_join:
    Z_19 = PHI(Z_18, Z_15)
    r2_20 = PHI(v25, v26)
    return
ProcedureBuilder_exit:
    use r2:r2_20
    use Z:Z_19
";
        #endregion

        RunTest(sExpected, m =>
        {
            var r = m.Reg32("r1", 1);
            var r2 = m.Reg32("r2", 2);
            var z = m.Flags("Z");  // is a condition code.
            var y = m.Flags("C");  // is a condition code.

            m.BranchIf(m.Lt0(r), "m2_skip");

            m.Label("m1");
            m.Assign(z, m.Cond(z.DataType, m.ISub(r, 3)));
            m.Goto("m3_join");

            m.Label("m2_skip");
            m.Assign(z, m.Cond(z.DataType, m.ISub(r, -4)));

            m.Label("m3_join");
            m.Assign(r2, m.Test(ConditionCode.EQ, z));
            m.Return();
        });
    }

    [Test]
    public void Nvp_ConditionCodeElimination_Phi3()
    {
        string sExpected =
        #region Expected
            @"
ProcedureBuilder_entry:
    def r1:word32
l1:
    v13 = r1 < 0<32>
    if (v13) goto m3_skip
l2:
    v19 = r1 < 0xA<32>
    if (v19) goto m2
m1:
    v25 = r1 - 0xF<32>
    Z_26 = cond(v25)
    v33 = r1 == 0xF<32>
    goto m4_join
m2:
    v22 = r1 - 3<32>
    Z_23 = cond(v22)
    v34 = r1 == 3<32>
    goto m4_join
m3_skip:
    v16 = r1 - 0xFFFFFFFC<32>
    Z_17 = cond(v16)
    v35 = r1 == 0xFFFFFFFC<32>
m4_join:
    Z_27 = PHI(Z_26, Z_23, Z_17)
    r2_28 = PHI(v33, v34, v35)
    return
ProcedureBuilder_exit:
    use r2:r2_28
    use Z:Z_27
";
        #endregion

        RunTest(sExpected, m =>
        {
            var r = m.Reg32("r1", 1);
            var r2 = m.Reg32("r2", 2);
            var z = m.Flags("Z");  // is a condition code.

            m.BranchIf(m.Lt0(r), "m3_skip");
            m.BranchIf(m.Lt(r, 10), "m2");

            m.Label("m1");
            m.Assign(z, m.Cond(z.DataType, m.ISub(r, 15)));
            m.Goto("m4_join");

            m.Label("m2");
            m.Assign(z, m.Cond(z.DataType, m.ISub(r, 3)));
            m.Goto("m4_join");

            m.Label("m3_skip");
            m.Assign(z, m.Cond(z.DataType, m.ISub(r, -4)));

            m.Label("m4_join");
            m.Assign(r2, m.Test(ConditionCode.EQ, z));
            m.Return();
        });
    }
}
