using Reko.Analysis;
using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Operators;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Nodes;

namespace Reko.Extras.SeaOfNodes.UnitTests.Nodes;

[TestFixture]
public class NodeRepresentationBuilderTests
{
    private ProgramBuilder pb;
    private ProgramDataFlow programFlow;

    public NodeRepresentationBuilderTests()
    {
        this.pb = new ProgramBuilder();
        this.programFlow = new Reko.Analysis.ProgramDataFlow();
    }

    private void RunTest(string sExpected, Action<ProcedureBuilder> testCodeBuilder)
    {
        this.pb = new ProgramBuilder();
        this.programFlow = new ProgramDataFlow();
        var m = new ProcedureBuilder();
        m.ProgramBuilder = this.pb;

        testCodeBuilder(m);

        var builder = new NodeRepresentationBuilder(this.programFlow);
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
    r1_9 = r1 + r2
    return r1_9";
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
    r1_11 = r1 + r2
    n13 = r1_11 >= 0<32>
    if (n13) goto m2_nonneg
m1_neg:
    Mem19[0x123400<32>:word32] = r1_11
    return
m2_nonneg:
    Mem16[0x123404<32>:word32] = r1_11
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
    r1_17 = r2 + 1<32>
    goto m3_done
m2_ge:
    r1_15 = r1 - 1<32>
m3_done:
    r1_18 = PHI(r1_17, r1_15)
    return r1_18";
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
    r1_11 = Mem11[0x123400<32>:word32]
    n13 = r1_11 >= 0<32>
    if (n13) goto m2_ge
    // succ: m1_lt, m2_ge
m1_lt:
    r1_15 = -r1_11
    goto m3_done
    // succ: m3_done
m2_ge:
m3_done:
    r1_16 = PHI(r1_15, r1_11)
    return r1_16
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
    r1_11 = r1 + 1<32>
    n14 = r1_11 * 8<32>
    n15 = 0x123400<32> + n14
    Mem18[n15:word32] = r2
    n19 = r1_11 < r2
    if (n19) goto l1
l2:
    return r1_11";
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

    [Test]
    public void Npb_Convert()
    {
        var sExpected =
        #region Expected
@"
ProcedureBuilder_entry:
    def r2:word32
l1:
    n9 = SLICE(r2, byte, 0)
    n10 = CONVERT(n9, byte, uint64)
    Mem11[0x123400<32>:uint64] = n10
    return";
        #endregion

        RunTest(sExpected, m =>
        {
            var r2 = m.Reg32("r2", 2);
            m.MStore(m.Word32(0x123400), m.Convert(m.Slice(r2, PrimitiveType.Byte), PrimitiveType.Byte, PrimitiveType.UInt64));
            m.Return();
        });
    }

    [Test]
    public void Npb_Slice()
    {
        var sExpected =
        #region Expected
@"
ProcedureBuilder_entry:
    def r1:word32
l1:
    n9 = SLICE(r1, byte, 0)
    Mem10[0x123400<32>:byte] = n9
    return";
        #endregion

        RunTest(sExpected, m =>
        {
            var r1 = m.Reg32("r1", 1);
            m.MStore(m.Word32(0x123400), m.Slice(r1, PrimitiveType.Byte));
            m.Return();
        });
    }

    [Test]
    public void Npb_Address()
    {
        var sExpected =
        #region Expected
@"
ProcedureBuilder_entry:
l1:
    return 0800:0042
    // succ: ProcedureBuilder_exit
ProcedureBuilder_exit:
";
        #endregion

        RunTest(sExpected, m =>
        {
            m.Return(Address.SegPtr(0x800, 0x42));
        });
    }

    [Test]
    public void Npb_call()
    {
        string sExpected =
        #region Expected
@"
ProcedureBuilder_entry:
l1:
    call procSub
        uses: r1:3<32> r2:4<32>
        defs: r1:n13
    Mem15[0x12300<32>:word32] = n13
    return";
        #endregion

        RunTest(sExpected, m =>
        {
            var r1 = m.Reg32("r1", 1);
            var r2 = m.Reg32("r2", 2);

            // Simulate the creation of a subroutine.
            var procSub = Procedure.Create(
                m.Architecture,
                "procSub", 
                Address.Ptr32(0x12380),
                m.Architecture.CreateFrame());
            var procSubFlow = new ProcedureFlow(m.Procedure)
            {
                BitsUsed = {
                     { r1.Storage, r1.Storage.GetBitRange() },
                     { r2.Storage, r2.Storage.GetBitRange() }
                },
                Trashed = { r1.Storage }
            };
            programFlow.ProcedureFlows.Add(procSub, procSubFlow);

            m.Assign(r1, 3);
            m.Assign(r2, 4);
            m.Call(procSub, 4);
            m.MStore(m.Word32(0x012300), r1);
            m.Return();
        });
    }

    [Test]
    public void Npb_cond_test()
    {
        string sExpected =
        #region Expected
@"
ProcedureBuilder_entry:
    def r1:word32
    def r2:word32
l1:
    n9 = r1 - r2
    CZ_10 = cond(n9)
    n12 = TEST(LE, CZ_10)
    Mem13[0x123400<32>:bool] = n12
    return";
        #endregion

        RunTest(sExpected, m =>
        {
            var r1 = m.Reg32("r1", 1);
            var r2 = m.Reg32("r2", 2);
            var status = RegisterStorage.Reg32("status", 0x10);
            var _grf = new FlagGroupStorage(status, 3, "CZ");
            var CZ = m.Frame.EnsureFlagGroup(_grf);
            m.Assign(CZ, m.Cond(status.DataType, m.ISub(r1, r2)));
            m.MStore(m.Word32(0x123400), m.Test(ConditionCode.LE, CZ));
            m.Return();
        });
    }

    [Test]
    public void Npb_application()
    {
        string sExpected =
        #region Expected
        @"
ProcedureBuilder_entry:
    def r1:word32
l1:
    r1_9 = abs<word32>(r1)
    return r1_9";
        #endregion

        RunTest(sExpected, m =>
        {
            var r1 = m.Reg32("r1", 1);
            m.Assign(r1, m.Fn(Reko.Core.Intrinsics.CommonOps.Abs, r1));
            m.Return(r1);
        });
    }

    [Test]
    public void Npb_switch()
    {
        string sExpected =
        #region Expected
        @"
ProcedureBuilder_entry:
    def sp_11:ptr32
l1:
    n13 = sp_11 + 4<32>
    r1_14 = Mem14[n13:word32]
    n16 = r1_14 >u 5<32>
    if (n16) goto m4_default
m1:
    switch (r1_14) goto m2, m2, m3, m3, m2, m3
m2:
    sp_21 = sp_11 - 4<32>
    Mem23[sp_21:word32] = 0x42<32>
    sp_26 = sp_21 + 4<32>
m3:
    sp_27 = PHI(sp_11, sp_11, sp_11, sp_26)
    sp_29 = sp_27 - 4<32>
    Mem31[sp_29:word32] = 0x2A<32>
    sp_34 = sp_29 + 4<32>
m4_default:
    sp_35 = PHI(sp_11, sp_34)
    sp_37 = sp_35 - 4<32>
    Mem39[sp_37:word32] = 0<32>
    sp_42 = sp_37 + 4<32>
    return";
        #endregion

        RunTest(sExpected, m =>
        {
            var sp = m.Frame.EnsureRegister(m.Architecture.StackRegister);
            var r1 = m.Reg32("r1", 1);
            var r2 = m.Reg32("r2", 2);
            var foo = new ExternalProcedure("foo", FunctionType.Action(
                [new Identifier("arg1", PrimitiveType.Int32, new StackStorage(4, PrimitiveType.Int32))]));
            m.Assign(sp, m.Frame.FramePointer);
            m.Assign(r1, m.Mem32(m.IAdd(sp, 4)));
            m.BranchIf(m.Ugt(r1, m.Word32(0x5)), "m4_default");
            m.Label("m1");
            m.Switch(r1,
                "m2", "m2", "m3", "m3", "m2", "m3");
            m.Label("m2");
            m.Assign(sp, m.ISub(sp, 4));
            m.MStore(sp, m.Word32(0x42));
            m.Call(foo, 4);
            m.Assign(sp, m.IAdd(sp, 4));
            // fall through
            m.Label("m3");
            m.Assign(sp, m.ISub(sp, 4));
            m.MStore(sp, m.Word32(42));
            m.Call(foo, 4);
            m.Assign(sp, m.IAdd(sp, 4));
            // fall through
            m.Label("m4_default");
            m.Assign(sp, m.ISub(sp, 4));
            m.MStore(sp, m.Word32(0));
            m.Call(foo, 4);
            m.Assign(sp, m.IAdd(sp, 4));

            m.Return();
        });
    }
}
