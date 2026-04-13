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
}