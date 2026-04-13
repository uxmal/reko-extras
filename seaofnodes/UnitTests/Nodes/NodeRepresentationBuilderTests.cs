using Reko.Extras.SeaOfNodes.Nodes;

namespace Reko.Extras.SeaOfNodes.UnitTests.Nodes;

[TestFixture]
public class NodeRepresentationBuilderTests
{
    private void RunTest(string sExpected, Action<ProcedureBuilder> testCodeBuilder)
    {
        var m = new ProcedureBuilder();
        testCodeBuilder(m);
        m.Procedure.Write(false, Console.Out);
        var builder = new NodeRepresentationBuilder();
        var graph = builder.Select(m.Procedure);
        var renderer = new NodeGraphRenderer();
        var sw = new StringWriter();
        sw.WriteLine();
        renderer.Render(graph, sw);
        var sActual = sw.ToString();
        if (sActual != sExpected)
        {
            Console.WriteLine(sActual);
            Assert.That(sActual, Is.EqualTo(sExpected));
        }
    }

    [Test]
    public void Npb_Create()
    {
        var sExpected = 
        #region Expected
@"
ProcedureBuilder_entry:
l1:
    return
    // succ: ProcedureBuilder_exit
ProcedureBuilder_exit:
";
        #endregion

        RunTest(sExpected, m =>
        {
            m.Return();
        });
    }

    [Test]
    public void Npb_ReturnValue()
    {
        var sExpected = 
        #region Expected
@"
ProcedureBuilder_entry:
l1:
    return 0x2A<32>
    // succ: ProcedureBuilder_exit
ProcedureBuilder_exit:
";
        #endregion

        RunTest(sExpected, m =>
        {
            m.Return(m.Word32(42));
        });
    }

    [Test]
    public void Npb_DefReturn()
    {
        var sExpected = 
        #region Expected
@"
ProcedureBuilder_entry:
    def r1:word32
l1:
    return r1";
        #endregion

        RunTest(sExpected, m =>
        {
            var r1 = m.Reg32("r1", 1);
            m.Return(r1);
        });
    }

    [Test]
    public void Npb_Add()
    {
        var sExpected = 
        #region Expected
@"
ProcedureBuilder_entry:
    def r1:word32
    def r2:word32
l1:
    n9 = r1 + r2
    return n9";
        #endregion

        RunTest(sExpected, m =>
        {
            var r1 = m.Reg32("r1", 1);
            var r2 = m.Reg32("r2", 2);
            m.Return(m.IAdd(r1, r2));
        });
    }

    [Test]
    public void Npb_Add_Variable()
    {
        var sExpected = 
        #region Expected
@"
ProcedureBuilder_entry:
    def r1:word32
    def r2:word32
l1:
    n9 = r1 + r2
    return n9";
        #endregion

        RunTest(sExpected, m =>
        {
            var r1 = m.Reg32("r1", 1);
            var r2 = m.Reg32("r2", 2);
            m.Assign(r1, m.IAdd(r1, r2));
            m.Return(r1);
        });
    }

    [Test]
    public void Npb_Store()
    {
        var sExpected = 
        #region Expected
@"
ProcedureBuilder_entry:
    def r1:word32
    def r2:word32
l1:
    Mem9[r1:word32] = r2
    return";
        #endregion

        RunTest(sExpected, m =>
        {
            var r1 = m.Reg32("r1", 1);
            var r2 = m.Reg32("r2", 2);
            m.MStore(r1, r2);
            m.Return();
        });
    }

    [Test]
    public void Npb_Store_Fork()
    {
        string sExpected = 
        #region Expected
@"
ProcedureBuilder_entry:
    def r1:word32
    def r2:word32
l1:
    n11 = r1 + r2
    n13 = n11 >= 0<32>
    if (n13) goto m2_nonneg
m1_neg:
    Mem19[0x123400<32>:word32] = n11
    return
m2_nonneg:
    Mem16[0x123404<32>:word32] = n11
    return";
        #endregion

        RunTest(sExpected, m =>
        {
            var r1 = m.Reg32("r1", 1);
            var r2 = m.Reg32("r2", 2);
            m.Assign(r1, m.IAdd(r1, r2));
            m.BranchIf(m.Ge0(r1), "m2_nonneg");

            m.Label("m1_neg");
            m.MStore(m.Word32(0x00123400), r1);
            m.Return();

            m.Label("m2_nonneg");
            m.MStore(m.Word32(0x00123404), r1);
            m.Return();
        });
    }

    [Test]
    public void Npb_Phi_diamond()
    {
        string sExpected = 
        #region Expected
@"
ProcedureBuilder_entry:
    def r1:word32
    def r2:word32
l1:
    n12 = r1 >= r2
    if (n12) goto m2_ge
m1_lt:
    n17 = r2 + 1<32>
    goto m3_done
m2_ge:
    n15 = r1 - 1<32>
m3_done:
    n18 = PHI(n17, n15)
    return n18";
        #endregion

        RunTest(sExpected, m =>
        {
            var r1 = m.Reg32("r1", 1);
            var r2 = m.Reg32("r2", 2);
            m.BranchIf(m.Ge(r1, r2), "m2_ge");

            m.Label("m1_lt");
            m.Assign(r1, m.IAdd(r2, 1));
            m.Goto("m3_done");

            m.Label("m2_ge");
            m.Assign(r1, m.ISub(r1, 1));

            m.Label("m3_done");
            m.Return(r1);
        });
    }

    [Test]
    public void Npb_redundantPhi()
    {
        string sExpected = 
        #region Expected
@"
ProcedureBuilder_entry:
l1:
    n11 = Mem11[0x123400<32>:word32]
    n13 = n11 >= 0<32>
    if (n13) goto m2_ge
    // succ: m1_lt, m2_ge
m1_lt:
    n15 = -n11
    goto m3_done
    // succ: m3_done
m2_ge:
m3_done:
    n16 = PHI(n15, n11)
    return n16
    // succ: ProcedureBuilder_exit
ProcedureBuilder_exit:
";
        #endregion

        RunTest(sExpected, m =>
        {
            var r1 = m.Reg32("r1", 1);
            var r2 = m.Reg32("r2", 2);
            m.Assign(r2, m.Mem32(m.Word32(0x123400)));
            m.BranchIf(m.Ge0(r2), "m2_ge");

            m.Label("m1_lt");
            m.Assign(r1, m.Neg(r2));
            m.Goto("m3_done");

            m.Label("m2_ge");
            m.Assign(r1, r2);

            m.Label("m3_done");
            m.Return(r1);
        });
    }

    [Test]
    public void Nbp_Phi_loop()
    {
        string sExpected = 
        #region Expected
@"
ProcedureBuilder_entry:
    def r1:word32
    def r2:word32
l1:
    n11 = r1 + 1<32>
    n14 = n11 * 8<32>
    n15 = 0x123400<32> + n14
    Mem18[n15:word32] = r2
    n19 = n11 < r2
    if (n19) goto l1
l2:
    return n11";
        #endregion

        RunTest(sExpected, m =>
        {
            var r1 = m.Reg32("r1", 1);
            var r2 = m.Reg32("r2", 2);
            m.Label("l1");
            m.Assign(r1, m.IAdd(r1, 1));
            m.MStore(m.IAdd(m.Word32(0x123400), m.IMul(r1, 8)), r2);
            m.BranchIf(m.Lt(r1, r2), "l1");
            m.Label("l2");
            m.Return(r1);
        });
    }
}
