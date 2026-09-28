using Reko.Analysis;
using Reko.Arch.Zilog;
using Reko.Core;
using Reko.Core.Services;
using Reko.Extras.SeaOfNodes.Analysis;
using Reko.Extras.SeaOfNodes.Nodes;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.SeaOfNodes.UnitTests.Analysis;

public class AnalysisTestHelper
{
    private ServiceContainer sc;
    private IProcessorArchitecture arch;
    private Program program;
    private ProgramDataFlow programFlow;

    public AnalysisTestHelper(IProcessorArchitecture? arch)
    {
        this.sc = new ServiceContainer();
        this.arch = arch ?? new FakeArchitecture(sc);
        var platform = new FakePlatform(sc, this.arch);
        this.program = new Program()
        {
            Architecture = this.arch,
            Platform = platform,
            SegmentMap = new SegmentMap(Address.Ptr32(0))
        };
        programFlow = new ProgramDataFlow();
    }

    public void RunTest(
        string sExpected,
        Action<ProcedureBuilder> builder,
        Func<NodeAnalysisContext, ProcedureNode, AnalysisTestHelper, ProcedureNode> test,
        bool includeOutputRefs,
        string testName)
    {
        var m = new ProcedureBuilder(testName);
        builder(m);

        var factory = new NodeFactory();
        var ngb = new NodeGraphBuilder(factory, programFlow, program.Architecture);
        var graph = ngb.Transform(m.Procedure);

        var writer2 = new StringWriter();
        new NodeGraphRenderer().Render(graph, writer2, includeOutputRefs);
        var sActual2 = writer2.ToString();
        Debug.WriteLine(sActual2);

        var peep = new PeepholeOptimizer(factory);
        IEventListener listener = new FakeDecompilerEventListener();
        var ctx = new NodeAnalysisContext(program, peep, listener);

        var writer = new StringWriter();
        string sActual;
        try
        {
            var newGraph = test(ctx, graph, this);
            writer.WriteLine();

            new NodeGraphRenderer().Render(newGraph, writer, includeOutputRefs);
            sActual = writer.ToString();
        }
        catch
        {
            writer.WriteLine();
            new NodeGraphRenderer().Render(graph, writer, includeOutputRefs);
            Console.WriteLine($"** {testName} failed ******");
            Console.WriteLine(writer.ToString());
            throw;
        }
        if (sExpected != sActual)
        {
            Console.WriteLine($"** {testName} failed ******");
            Console.WriteLine("Expected:");
            Console.WriteLine(sExpected);
            Console.WriteLine("Actual:");
            Console.WriteLine(sActual);
            Console.WriteLine();
            Assert.That(sActual, Is.EqualTo(sExpected));
        }

    }




}
