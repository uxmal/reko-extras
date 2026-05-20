using Reko.Core;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Analysis;
using Reko.Extras.SeaOfNodes.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.SeaOfNodes.UnitTests.Analysis;

[TestFixture]
public class PeepholeOptimizerTests
{

    private NodeFactory m;
    private PeepholeOptimizer peep;
    private BlockNode block=default!;

    [SetUp]
    public void Setup()
    {
        m = new NodeFactory();
        peep = new PeepholeOptimizer(m);
        var arch = new FakeArchitecture();
        var proc = new Procedure(arch, "test", Address.Ptr32(0x123400), arch.CreateFrame());
        var b = new Block(proc, proc.EntryAddress, Reko.Core.NamingPolicy.Instance.BlockName(proc.EntryAddress));
        block = m.Block(b); 
    }

    [Test]
    public void Peep_SeqOfSlices()
    {
        var r3 = m.Def(block, RegisterStorage.Reg32("r3", 3), PrimitiveType.Word32);
        var r3_lo = m.Slice(PrimitiveType.Word16, r3, 0);
        var r3_hi = m.Slice(PrimitiveType.Word16, r3, 16);

        var result = peep.Seq(PrimitiveType.Word32, r3_hi, r3_lo);

        Assert.That(result.ToString(), Is.EqualTo("def r3:word32"));
    }
}
