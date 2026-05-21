using Reko.Core;
using Reko.Core.Operators;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Analysis;
using Reko.Extras.SeaOfNodes.Nodes;

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

    [Test]
    public void Peep_Condense_IAddIAdd()
    {
        var r3 = m.Def(block, RegisterStorage.Reg32("r3", 3), PrimitiveType.Word32);
        var add1 = m.IAdd(r3, 4);
        var add2 = m.IAdd(add1, 5);

        var result = peep.Bin(r3.DataType, Operator.IAdd, null, add1, m.Word32(5));

        Assert.That(result.ToString(), Is.EqualTo("n9 = r3 + 9<32>"));
    }

    [Test]
    public void Peep_Condense_IAddISub()
    {
        var r3 = m.Def(block, RegisterStorage.Reg32("r3", 3), PrimitiveType.Word32);
        var sub1 = m.ISub(r3, 4);

        var result = peep.Bin(r3.DataType, Operator.IAdd, null, sub1, m.Word32(5));

        Assert.That(result.ToString(), Is.EqualTo("n7 = r3 + 1<32>"));
    }

    [Test]
    public void Peep_Condense_ISubIAdd()
    {
        var r3 = m.Def(block, RegisterStorage.Reg32("r3", 3), PrimitiveType.Word32);
        var sub1 = m.IAdd(r3, 4);

        var result = peep.Bin(r3.DataType, Operator.ISub, null, sub1, m.Word32(5));

        Assert.That(result.ToString(), Is.EqualTo("n7 = r3 - 1<32>"));
    }

    [Test]
    public void Peep_Condense_ISubISub()
    {
        var r3 = m.Def(block, RegisterStorage.Reg32("r3", 3), PrimitiveType.Word32);
        var sub1 = m.ISub(r3, 4);

        var result = peep.Bin(r3.DataType, Operator.ISub, null, sub1, m.Word32(5));

        Assert.That(result.ToString(), Is.EqualTo("n7 = r3 - 9<32>"));
    }

    [Test]
    public void Peep_Self_Sub()
    {
        var r3 = m.Def(block, RegisterStorage.Reg32("r3", 3), PrimitiveType.Word32);
        
        var result = peep.ISub(r3, r3);

        Assert.That(result.ToString(), Is.EqualTo("0<32>"));
    }

    [Test]
    public void Peep_Self_Xor()
    {
        var r3 = m.Def(block, RegisterStorage.Reg32("r3", 3), PrimitiveType.Word32);

        var result = peep.ISub(r3, r3);

        Assert.That(result.ToString(), Is.EqualTo("0<32>"));
    }

    [Test]
    public void Peep_And_AllOnes()
    {
        var r3 = m.Def(block, RegisterStorage.Reg16("r3", 3));

        var result = peep.And(r3, 0xFFFF);

        Assert.That(result.ToString(), Is.EqualTo("def r3:word16"));
    }

    [Test]
    public void Peep_And_NotAllOnes()
    {
        var r3 = m.Def(block, RegisterStorage.Reg16("r3", 3));

        var result = peep.And(r3, 0xFFFE);

        Assert.That(result.ToString(), Is.EqualTo("n4 = r3 & 0xFFFE<16>"));
    }
}
