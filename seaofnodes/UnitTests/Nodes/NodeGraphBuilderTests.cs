using Reko.Analysis;
using Reko.Core;
using Reko.Core.Collections;
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

        var factory = new NodeFactory();
        var builder = new NodeGraphBuilder(factory, this.programFlow, m.Architecture);
        var graph = builder.Transform(m.Procedure);
        var renderer = new NodeGraphRenderer();
        var sw = new StringWriter();
        sw.WriteLine();
        renderer.Render(graph, sw);
        // RenderGraph(renderer, graph, sw);
        var sActual = sw.ToString();
        if (sActual != sExpected)
        {
            Console.WriteLine(sActual);
            Assert.That(sActual, Is.EqualTo(sExpected));
        }
    }

    private void RenderGraph(NodeGraphRenderer renderer, StartNode graph, StringWriter sw)
    {
        try
        {
            renderer.Render(graph, sw);
        }
        catch
        {
            List<Node> nodes = [];
            HashSet<Node> visited = [];
            WorkList<Node> wl = new();
            wl.Add(graph);
            while (wl.TryGetWorkItem(out var node))
            {
                if (node is null || !visited.Add(node))
                    continue;
                nodes.Add(node);
                wl.AddRange(node.Inputs.Where(n => n is not null)!);
                wl.AddRange(node.Outputs);
            }
            foreach (var node in nodes.OrderBy(n => n.Number))
            {
                node.RenderReference(sw);
                sw.WriteLine($":{node.Label}");
                sw.Write($"    in: ");
                foreach (var input in node.Inputs)
                {
                    sw.Write(' ');
                    if (input is not null)
                        input.RenderReference(sw);
                    else
                        sw.Write("##");
                }
                sw.WriteLine();

                sw.Write($"    out:");
                foreach (var output in node.Outputs)
                {
                    sw.Write(' ');
                    output.RenderReference(sw);
                }
                sw.WriteLine();
            }

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
    return r1
ProcedureBuilder_exit:
";
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
    return n9
ProcedureBuilder_exit:
";
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
    return r1_9
ProcedureBuilder_exit:
    use r1:r1_9
";
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
    return
ProcedureBuilder_exit:
";
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
    return
ProcedureBuilder_exit:
    use r1:r1_11
";
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
    return r1_18
ProcedureBuilder_exit:
    use r1:r1_18
";
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
    public void Npb_RedundantPhi()
    {
        string sExpected =
        #region Expected
@"
ProcedureBuilder_entry:
l1:
    r2_11 = Mem9[0x123400<32>:word32]
    n13 = r2_11 >= 0<32>
    if (n13) goto m2_ge
    // succ: m1_lt, m2_ge
m1_lt:
    r1_15 = -r2_11
    goto m3_done
    // succ: m3_done
m2_ge:
m3_done:
    r1_16 = PHI(r1_15, r2_11)
    return r1_16
    // succ: ProcedureBuilder_exit
ProcedureBuilder_exit:
    use r1:r1_16
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
    def r2:word32
    def r1:word32
l1:
    r1_8 = PHI(r1, r1_10)
    r1_10 = r1_8 + 1<32>
    n13 = r1_10 * 8<32>
    n14 = 0x123400<32> + n13
    Mem16[n14:word32] = r2
    n17 = r1_10 < r2
    if (n17) goto l1
l2:
    return r1_10
ProcedureBuilder_exit:
    use r1:r1_10
";
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
    return
ProcedureBuilder_exit:
";
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
    return
ProcedureBuilder_exit:
";
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
        defs: r1:r1_13
    Mem15[0x12300<32>:word32] = r1_13
    return
ProcedureBuilder_exit:
    use r1:r1_13
    use r2:4<32>
";
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
    return
ProcedureBuilder_exit:
    use CZ:CZ_10
";
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
    return r1_9
ProcedureBuilder_exit:
    use r1:r1_9
";
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
    def fp:ptr32
    def stack:int32
l1:
    n13 = fp + 4<32>
    r1_14 = Mem10[n13:word32]
    n16 = r1_14 >u 5<32>
    if (n16) goto m4_default
m1:
    switch (r1_14) goto m2, m2, m3, m3, m2, m3
m2:
    sp_21 = fp - 4<32>
    Mem23[sp_21:word32] = 0x42<32>
    foo(stack)
    sp_30 = sp_21 + 4<32>
m3:
    sp_31 = PHI(fp, fp, fp, sp_30)
    sp_33 = sp_31 - 4<32>
    Mem35[sp_33:word32] = 0x2A<32>
    foo(stack)
    sp_41 = sp_33 + 4<32>
m4_default:
    sp_42 = PHI(fp, sp_41)
    sp_44 = sp_42 - 4<32>
    Mem46[sp_44:word32] = 0<32>
    foo(stack)
    sp_52 = sp_44 + 4<32>
    return
ProcedureBuilder_exit:
    use sp:sp_52
";
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

    [Test]
    public void Npb_overlapping_FlagGroups()
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
    C_12 = CZ_10 & 1<32>
    n13 = TEST(ULT, C_12)
    return n13
ProcedureBuilder_exit:
    use CZ:CZ_10
";
        #endregion

        RunTest(sExpected, m =>
        {
            var r1 = m.Reg32("r1", 1);
            var r2 = m.Reg32("r2", 2);
            var arch = (FakeArchitecture) pb.Program.Architecture;
            var _cz =  arch.GetFlagGroup(arch.Status, 3)!;
            var _c = arch.GetFlagGroup(arch.Status, 1)!;
            var CZ = m.Frame.EnsureFlagGroup(_cz);
            var C = m.Frame.EnsureFlagGroup(_c);
            m.Assign(CZ, m.Cond(CZ.DataType, m.ISub(r1, r2)));
            m.Return(m.Test(ConditionCode.ULT, C));
        });
    }

    [Test]
    public void Npb_Sequence()
    {
        string sExpected =
        #region Expected
        @"
ProcedureBuilder_entry:
l1:
    r2_8 = Mem6[0x123400<32>:word32]
    r1_10 = Mem6[0x123404<32>:word32]
    r4_12 = Mem6[0x123408<32>:word32]
    r3_14 = Mem6[0x12340C<32>:word32]
    r1_r2_15 = SEQ(r1_10, r2_8)
    r3_r4_16 = SEQ(r3_14, r4_12)
    r1_r2_17 = r1_r2_15 + r3_r4_16
    r1_19 = SLICE(r1_r2_17, word32, 32)
    r2_21 = SLICE(r1_r2_17, word32, 0)
    r3_23 = SLICE(r3_r4_16, word32, 32)
    r4_25 = SLICE(r3_r4_16, word32, 0)
    return
    // succ: ProcedureBuilder_exit
ProcedureBuilder_exit:
    use r1:r1_19
    use r2:r2_21
    use r3:r3_23
    use r4:r4_25
";
        #endregion

        RunTest(sExpected, m =>
        {
            var r1 = m.Reg32("r1", 1);
            var r2 = m.Reg32("r2", 2);
            var r3 = m.Reg32("r3", 3);
            var r4 = m.Reg32("r4", 4);

            var r1_r2 = m.Frame.EnsureSequence(
                PrimitiveType.Word64, r1.Storage, r2.Storage);
            var r3_r4 = m.Frame.EnsureSequence(
                PrimitiveType.Word64, r3.Storage, r4.Storage);
            
            m.Assign(r2, m.Mem32(m.Word32(0x123400)));
            m.Assign(r1, m.Mem32(m.Word32(0x123404)));
            m.Assign(r4, m.Mem32(m.Word32(0x123408)));
            m.Assign(r3, m.Mem32(m.Word32(0x12340C)));
            m.Assign(r1_r2, m.Seq(r1, r2));
            m.Assign(r3_r4, m.Seq(r3, r4));
            m.Assign(r1_r2, m.IAdd(r1_r2, r3_r4));
            m.Return();
        });
    }

    [Test]
    public void Npb_Sequence_Def()
    {
        string sExpected =
        #region Expected
        @"
ProcedureBuilder_entry:
    def r1_r2:word64
l1:
    r1_9 = SLICE(r1_r2, word32, 32)
    n11 = r1_9 >= 0<32>
    r2_17 = SLICE(r1_r2, word32, 0)
    if (n11) goto m2_done
m1_negative:
    r1_r2_20 = -r1_r2
    r1_23 = SLICE(r1_r2_20, word32, 32)
    r2_26 = SLICE(r1_r2_20, word32, 0)
m2_done:
    r1_22 = PHI(r1_9, r1_23)
    r2_25 = PHI(r2_17, r2_26)
    return
ProcedureBuilder_exit:
    use r1:r1_22
    use r2:r2_25
";
        #endregion

        RunTest(sExpected, m =>
        {
            var r1 = m.Reg32("r1", 1);
            var r2 = m.Reg32("r2", 2);
            var r3 = m.Reg32("r3", 2);

            var r1_r2 = m.Frame.EnsureSequence(
                PrimitiveType.Word64, r1.Storage, r2.Storage);

            m.BranchIf(m.Ge0(r1), "m2_done");

            m.Label("m1_negative");
            m.Assign(r1_r2, m.Neg(r1_r2));

            m.Label("m2_done");
            m.Return();
        });
    }

    [Test(Description = "Loads shouldn't affect the Memx identifier")]
    public void Dpb_TwoLoads()
    {
        string sExpected =
        #region Expected
            @"
ProcedureBuilder_entry:
    def r2:word32
l1:
    r1_8 = Mem6[00123400:word32]
    r1_10 = Mem6[00123404:word32]
    r1_12 = r1_10 + r2
    return
ProcedureBuilder_exit:
    use r1:r1_12
";
        #endregion

        RunTest(sExpected, m =>
        {
            var r1 = m.Reg32("r1", 1);
            var r2 = m.Reg32("r2", 2);

            m.Assign(r1, m.Mem32(Address.Ptr32(0x123400)));
            m.Assign(r1, m.Mem32(Address.Ptr32(0x123404)));
            m.Assign(r1, m.IAdd(r1, r2));
            m.Return();
        });
    }

    [Test]
    public void Npb_Overlapping_Registers()
    {
        string sExpected =
        #region Expected    
            @"
ProcedureBuilder_entry:
l1:
    rax_8 = Mem6[00123400:word64]
    eax_10 = Mem6[00123408:word32]
    ah_12 = Mem6[00123410:byte]
    al_14 = Mem6[00123411:byte]
    return
    // succ: ProcedureBuilder_exit
ProcedureBuilder_exit:
    use ah:ah_12
    use al:al_14
    use eax:eax_10
    use rax:rax_8
    use rbx:rax_8
";
        #endregion

        RunTest(sExpected, m =>
        {
            var al = m.Reg8("al", 0);
            var ah = m.Reg8("ah", 0, 8);
            var ax = m.Reg16("ax", 0);
            var eax = m.Reg32("eax", 0);
            var rax = m.Reg64("rax", 0);
            var rbx = m.Reg64("rbx", 3);

            m.Assign(rax, m.Mem64(Address.Ptr32(0x123400)));
            m.Assign(eax, m.Mem32(Address.Ptr32(0x123408)));
            m.Assign(ah, m.Mem8(Address.Ptr32(0x0123410)));
            m.Assign(al, m.Mem8(Address.Ptr32(0x0123411)));

            m.Assign(rbx, rax);
            m.Return();
        });
    }

    [Test]
    public void Dpb_Copy()
    {
        string sExpected =
        #region Expected
            @"
ProcedureBuilder_entry:
    def rcx:word64
l1:
    return
ProcedureBuilder_exit:
    use rax:rcx
";
        #endregion

        RunTest(sExpected, m =>
        {
            var rax = m.Reg64("rax", 0);
            var rcx = m.Reg64("rcx", 1);

            m.Assign(rax, rcx);
            m.Return();
        });
    }

    [Test]
    public void Dpb_Read_Slice()
    {
        string sExpected =
        #region Expected
@"
ProcedureBuilder_entry:
l1:
    rax_8 = Mem6[00123400:word64]
    ah_10 = SLICE(rax_8, byte, 8)
    Mem11[00123408:byte] = ah_10
    return
    // succ: ProcedureBuilder_exit
ProcedureBuilder_exit:
    use ah:ah_10
    use rax:rax_8
";
        #endregion

        RunTest(sExpected, m =>
        {
            var rax = m.Reg64("rax", 0);
            var ah = m.Reg8("ah", 0, 8);

            m.Assign(rax, m.Mem64(Address.Ptr32(0x123400)));
            m.MStore(Address.Ptr32(0x123408), ah);
            m.Return();
        });
    }
}
