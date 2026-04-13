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

}
