#region License
/* 
 * Copyright (C) 1999-2026 John Källén.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2, or (at your option)
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; see the file COPYING.  If not, write to
 * the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.
 */
#endregion

using Moq;
using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Analysis;
using Reko.Extras.SeaOfNodes.Nodes;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using ProgramDataFlow = Reko.Analysis.ProgramDataFlow;

namespace Reko.Extras.SeaOfNodes.UnitTests.Analysis;

[TestFixture]
public class LongAddRewriterTests
{
    private LongAddRewriter? rw;
    private IProcessorArchitecture arch;
    private Program program;
    private ServiceContainer sc;
    private ProgramDataFlow programFlow = default!;
    private Identifier ax;
    private Identifier bx;
    private Identifier cx;
    private Identifier dx;
    private Identifier rdx;
    private Identifier es;
    private Identifier SCZ;
    private Identifier CF;
    private ProcedureBuilder m;

    public LongAddRewriterTests()
    {
    }

    [SetUp]
    public void Setup()
    {
        sc = new ServiceContainer();
        arch = new FakeArchitecture(sc);
        var platform = new FakePlatform(sc, arch);
        program = new Program()
        {
            Architecture = arch,
            Platform = platform,
            SegmentMap = new SegmentMap(Address.Ptr32(0))
        };
        m = new ProcedureBuilder(arch);
        var binder = m.Frame;
        ax = binder.EnsureRegister(RegisterStorage.Reg16("ax", 0));
        bx = binder.EnsureRegister(RegisterStorage.Reg16("bx", 3));
        cx = binder.EnsureRegister(RegisterStorage.Reg16("cx", 1));
        dx = binder.EnsureRegister(RegisterStorage.Reg16("dx", 2));
        rdx = binder.EnsureRegister(RegisterStorage.Reg64("rdx", 2));
        es = binder.EnsureRegister(RegisterStorage.Reg16("es", 14));
        SCZ = binder.EnsureFlagGroup(arch.GetFlagGroup("SCZ")!);
        CF = binder.EnsureFlagGroup(arch.CarryFlag!);
    }

    [TearDown]
    public void TearDown()
    {
        sc?.Dispose();
    }

    protected void RunTest(Program program, TextWriter writer)
    {
        var eventListener = new FakeDecompilerEventListener();
        foreach (var proc in program.Procedures.Values)
        {
            var factory = new NodeFactory();
            var peep = new PeepholeOptimizer(factory);
            var npb = new NodeGraphBuilder(factory, programFlow, program.Architecture);
            var graph = npb.Transform(m.Procedure);
            var vp = new NodeValuePropagator(factory);
            graph = vp.Transform(graph);

            var larw = new LongAddRewriter(peep);
            StartNode graphNew = larw.Transform(graph);

            new NodeGraphRenderer().Render(graphNew, writer);
            writer.WriteLine();
        }
    }

    private void RunTest(Action<ProcedureBuilder> builder)
    {
        builder(m);
        var dynamicLinker = new Mock<IDynamicLinker>();
        var factory = new NodeFactory();
        var sst = new NodeGraphBuilder(
            factory,
            programFlow,
            program.Architecture);
        var graph = sst.Transform(m.Procedure);

        var peep = new PeepholeOptimizer(factory);
        rw = new LongAddRewriter(peep);
    }

    private void RunTest(
        string sExpected,
        Action<ProcedureBuilder> builder,
        bool includeOutputRefs = false,
        [CallerMemberName] string testName = "")
    {
        builder(m);
        var factory = new NodeFactory();
        var ngb = new NodeGraphBuilder(factory, programFlow, program.Architecture);
        var graph = ngb.Transform(m.Procedure);

        var writer2 = new StringWriter();
        new NodeGraphRenderer().Render(graph, writer2, includeOutputRefs);
        var sActual2 = writer2.ToString();
        Debug.WriteLine(sActual2);

        var peep = new PeepholeOptimizer(factory);
        rw = new LongAddRewriter(peep);

        var graphNew = rw.Transform(graph);

        var writer = new StringWriter();
        new NodeGraphRenderer().Render(graphNew, writer, includeOutputRefs);
        var sActual = writer.ToString();
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

    [Test]
    public void Larw_add()
    {
        var sExp =
@"ProcedureBuilder_entry:
    def dx_ax:word32
    def bx_cx:word32
l1:
    v28 = dx_ax + bx_cx
    ax_9 = SLICE(v28, word16, 0)
    dx_16 = SLICE(v28, word16, 16)
    CZS_17 = cond(v28)
    CZS_10 = cond(ax_9)
    C_15 = CZS_10 & 1<32>
    return
ProcedureBuilder_exit:
    use ax:ax_9
    use dx:dx_16
    use CZS:CZS_17
";
        RunTest(sExp, m =>
        {
            m.Assign(ax, m.IAdd(ax, cx));
            m.Assign(SCZ, m.Cond(SCZ.DataType, ax));
            m.Assign(dx, m.IAddC(dx, bx, CF));
            m.Assign(SCZ, m.Cond(SCZ.DataType, dx));
            m.Return();
        });        
    }

    [Test]
    public void Larw_AddChain()
    {
        var sExp =
@"ProcedureBuilder_entry:
    def cx_bx_dx_ax:word64 # [ bx_dx_ax_49, cx_29, v60 ]
l1:
    v9 = Mem6[0x1234<32>:word16] # [ v45, v52, v59 ]
    v15 = Mem6[0x1236<32>:word16] # [ v45, v52, v59 ]
    v23 = Mem6[0x1238<32>:word16] # [ v52, v59 ]
    v31 = Mem6[0x123A<32>:word16] # [ v59 ]
    v59 = SEQ(v31, v23, v15, v9) # [ v60 ]
    v60 = cx_bx_dx_ax + v59 # [ v53, cx_34, CZS_35 ]
    v53 = SLICE(v60, word48, 0) # [ v46, bx_26, CZS_27 ]
    v46 = SLICE(v53, word32, 0) # [ ax_10, dx_18, CZS_19 ]
    ax_10 = SLICE(v46, word16, 0) # [ CZS_11, ax_37 ]
    bx_26 = SLICE(v53, word16, 32) # [ bx_38 ]
    cx_34 = SLICE(v60, word16, 48) # [ cx_39 ]
    dx_18 = SLICE(v46, word16, 16) # [ dx_40 ]
    CZS_35 = cond(v60) # [ CZS_41 ]
    CZS_27 = cond(v53) # [ C_33 ]
    CZS_19 = cond(v46) # [ C_25 ]
    CZS_11 = cond(ax_10) # [ C_17 ]
    C_33 = CZS_27 & 1<32> # [  ]
    C_25 = CZS_19 & 1<32> # [  ]
    C_17 = CZS_11 & 1<32> # [  ]
    return # [  ]
ProcedureBuilder_exit:
    use ax:ax_10 # [  ]
    use bx:bx_26 # [  ]
    use cx:cx_34 # [  ]
    use dx:dx_18 # [  ]
    use CZS:CZS_35 # [  ]
";
        RunTest(sExp, m =>
        {
            m.Assign(ax, m.IAdd(ax, m.Mem16(m.Word32(0x001234))));
            m.Assign(SCZ, m.Cond(SCZ.DataType, ax));
            m.Assign(dx, m.IAddC(dx, m.Mem16(m.Word32(0x001236)), CF));
            m.Assign(SCZ, m.Cond(SCZ.DataType, dx));
            m.Assign(bx, m.IAddC(bx, m.Mem16(m.Word32(0x001238)), CF));
            m.Assign(SCZ, m.Cond(SCZ.DataType, bx));
            m.Assign(cx, m.IAddC(cx, m.Mem16(m.Word32(0x00123A)), CF));
            m.Assign(SCZ, m.Cond(SCZ.DataType, cx));
            m.Return();
        }, includeOutputRefs: true);
    }


    [Test]
    public void Larw_Match_AddRecConst()
    {
        var sExp =
@"ProcedureBuilder_entry:
    def dx_ax:word32
l1:
    v23 = dx_ax + 0x12345678<32>
    ax_9 = SLICE(v23, word16, 0)
    dx_14 = SLICE(v23, word16, 16)
    C_10 = cond(ax_9)
    return
ProcedureBuilder_exit:
    use ax:ax_9
    use dx:dx_14
    use C:C_10
";
        RunTest(sExp, m =>
        {
            m.Assign(ax, m.IAdd(ax, 0x5678));
            m.Assign(CF, m.Cond(CF.DataType, ax));
            m.Assign(dx, m.IAddC(dx, m.Word16(0x1234), CF));
            m.Return();
        });
    }

    [Test]
    public void Larw_Match_AddConstant()
    {
        var sExp =
@"ProcedureBuilder_entry:
    def dx_ax:word32
l1:
    v23 = dx_ax + 1<32>
    ax_9 = SLICE(v23, word16, 0)
    dx_14 = SLICE(v23, word16, 16)
    C_10 = cond(ax_9)
    return
ProcedureBuilder_exit:
    use ax:ax_9
    use dx:dx_14
    use C:C_10
";
        RunTest(sExp, m =>
        {
            m.Assign(ax, m.IAdd(ax, 1));
            m.Assign(CF, m.Cond(CF.DataType, ax));
            m.Assign(dx, m.IAddC(dx, m.Word16(0), CF));
            m.Return();
        });
    }

    [Test]
    public void Larw_Replace_AddReg()
    {
        var sExp =
@"ProcedureBuilder_entry:
    def bx:word16
    def dx_ax:word32
l1:
    v10 = bx + 0x300<16>
    v11 = Mem6[v10:word16]
    v17 = bx + 0x302<16>
    v18 = Mem6[v17:word16]
    v28 = SEQ(v18, v11)
    v29 = dx_ax + v28
    ax_12 = SLICE(v29, word16, 0)
    dx_19 = SLICE(v29, word16, 16)
    C_20 = cond(v29)
    C_13 = cond(ax_12)
    return
ProcedureBuilder_exit:
    use ax:ax_12
    use dx:dx_19
    use C:C_20
";
        RunTest(sExp, m =>
        {
            m.Assign(ax, m.IAdd(ax, m.Mem16(m.IAdd(bx, 0x300))));
            m.Assign(CF, m.Cond(CF.DataType, ax));
            m.Assign(dx, m.IAddC(dx, m.Mem16(m.IAdd(bx, 0x302)), CF));
            m.Assign(CF, m.Cond(CF.DataType, dx));
            m.Return();
        });
    }

    [Test(Description = "Avoid building long adds if the instructions shouldn't be paired")]
    public void Larw_Avoid()
    {

        var sExp =
@"ProcedureBuilder_entry:
    def cx:word16
l1:
    v9 = cx - 0x30<16>
    CZS_10 = cond(v9)
    C_13 = CZS_10 & 1<32>
    ax_14 = 0<16> +16 C_13
    v17 = cx - 0x3A<16>
    CZS_18 = cond(v17)
    C_20 = CZS_18 & 1<32>
    C_21 = !C_20
    ax_23 = __addc<word16,word32>(ax_14, ax_14, C_21)
    CZS_24 = cond(ax_23)
    CZS_15 = cond(ax_14)
    return
ProcedureBuilder_exit:
    use ax:ax_23
    use CZS:CZS_24
";
        RunTest(sExp, m =>
        {
            m.Assign(SCZ, m.Cond(SCZ.DataType, m.ISub(cx, 0x0030)));
            m.Assign(ax, m.IAdd(m.Word16(0x0000), CF));
            m.Assign(SCZ, m.Cond(SCZ.DataType, ax));
            m.Assign(SCZ, m.Cond(SCZ.DataType, m.ISub(cx, 0x003A)));
            m.Assign(CF, m.Not(CF));
            m.Assign(ax, m.IAddC(ax, ax, CF));
            m.Assign(SCZ, m.Cond(SCZ.DataType, ax));
            m.Return();
        });
    }

    [Test]
    public void Larw_InterleavedMemoryAccesses()
    {
        var sExp =
@"ProcedureBuilder_entry:
l1:
    ax_8 = Mem6[0210:word16]
    dx_10 = Mem6[0212:word16]
    es_cx_12 = Mem6[0214:word32]
    v32 = SEQ(dx_10, ax_8)
    v33 = v32 - es_cx_12
    ax_15 = SLICE(v33, word16, 0)
    Mem18[0218:word16] = ax_15
    dx_22 = SLICE(v33, word16, 16)
    Mem24[021A:word16] = dx_22
    es_13 = SLICE(es_cx_12, word16, 16)
    cx_14 = SLICE(es_cx_12, word16, 0)
    CZS_16 = cond(ax_15)
    C_21 = CZS_16 & 1<32>
    return
    // succ: ProcedureBuilder_exit
ProcedureBuilder_exit:
    use es:es_13
    use cx:cx_14
    use ax:ax_15
    use bx:es_13
    use dx:dx_22
    use CZS:CZS_16
";
        RunTest(sExp, m =>
        {
            var es_cx = m.Procedure.Frame.EnsureSequence(PrimitiveType.Word32, es.Storage, cx.Storage);
            m.Assign(ax, m.Mem16(m.Ptr16(0x210)));
            m.Assign(dx, m.Mem16(m.Ptr16(0x212)));
            m.Assign(es_cx, m.Mem32(m.Ptr16(0x214)));
            m.Assign(bx, es);
            m.Assign(ax, m.ISub(ax, cx));
            m.Assign(this.SCZ, m.Cond(SCZ.DataType, ax));
            m.MStore(m.Ptr16(0x218), ax);
            m.Assign(dx, m.ISubC(dx, bx, this.CF));
            m.MStore(m.Ptr16(0x21A), dx);
            m.Return();
        });
    }

    /// <summary>
    /// It is theoretically possible for an add/adc pair to be in 
    /// separate basic blocks. 
    /// </summary>
    [Test]
    public void Larw_span_multiple_blocks()
    {
        var sExp =
        #region Expected
@"ProcedureBuilder_entry:
l1:
    ax_9 = Mem7[0210:word16]
    dx_11 = Mem7[0212:word16]
    v13 = Mem7[0220:word16]
    // succ: m2
m2:
    v18 = Mem7[0222:word16]
    v25 = SEQ(dx_11, ax_9)
    v26 = SEQ(v18, v13)
    v27 = v25 + v26
    dx_21 = SLICE(v27, word16, 16)
    ax_14 = SLICE(v27, word16, 0)
    CZS_15 = cond(ax_14)
    C_20 = CZS_15 & 1<32>
    return
    // succ: ProcedureBuilder_exit
ProcedureBuilder_exit:
    use dx:dx_21
    use C:C_20
";
        #endregion

        RunTest(sExp, m =>
        {
            m.Assign(ax, m.Mem16(m.Ptr16(0x210)));
            m.Assign(dx, m.Mem16(m.Ptr16(0x212)));
            m.Assign(ax, m.IAdd(ax, m.Mem16(m.Ptr16(0x0220))));
            m.Assign(this.SCZ, m.Cond(SCZ.DataType, ax));
            m.Goto("m2");

            m.Label("m2");
            m.Assign(dx, m.IAddC(dx, m.Mem16(m.Ptr16(0x0222)), this.CF));
            m.Return();
        });
    }

    [Test]
    public void Larw_Multiply_Accumulate()
    {
        var sExp =
        #region  Expected
@"ProcedureBuilder_entry:
l1:
    v8 = Mem6[0x5418<32>:word16]
    v15 = Mem6[0x6FF0<32>:word32]
    v22 = Mem18[0x6FF4<32>:word32]
    v33 = SEQ(v22, v15)
    eax_9 = CONVERT(v8, word16, int32)
    edx_eax_11 = 0xF000<32> *s64 eax_9
    v34 = v33 - edx_eax_11
    tmp1_16 = SLICE(v34, word32, 0)
    Mem18[0x6FF0<32>:word32] = tmp1_16
    tmp2_25 = SLICE(v34, word32, 32)
    Mem27[0x6FF4<32>:word32] = tmp2_25
    edx_12 = SLICE(edx_eax_11, word32, 32)
    eax_13 = SLICE(edx_eax_11, word32, 0)
    CZS_28 = cond(v34)
    CZS_19 = cond(tmp1_16)
    C_24 = CZS_19 & 1<32>
    return
    // succ: ProcedureBuilder_exit
ProcedureBuilder_exit:
    use edx:edx_12
    use eax:eax_13
    use CZS:CZS_28
";
        #endregion
        RunTest(sExp, m =>
        {
            var eax = m.Reg32("eax", 0);
            var edx = m.Reg32("edx", 2);
            var edx_eax = m.Frame.EnsureSequence(PrimitiveType.Word64, edx.Storage, eax.Storage);
            var tmp1 = m.Temp(PrimitiveType.Word32, "tmp1");
            var tmp2 = m.Temp(PrimitiveType.Word32, "tmp2");
            m.Assign(eax, m.Convert(m.Mem16(m.Word32(0x5418)), PrimitiveType.Word16, PrimitiveType.Int32));
            m.Assign(edx, m.Word32(0xF000));
            m.Assign(edx_eax, m.SMul(PrimitiveType.Int64, edx, eax));
            m.Assign(tmp1, m.ISub(m.Mem32(m.Word32(0x6FF0)), eax));
            m.MStore(m.Word32(0x6FF0), tmp1);
            m.Assign(this.SCZ, m.Cond(SCZ.DataType, tmp1));
            m.Assign(tmp2, m.ISubC(m.Mem32(m.Word32(0x6FF4)), edx, this.CF));
            m.MStore(m.Word32(0x6FF4), tmp2);
            m.Assign(this.SCZ, m.Cond(SCZ.DataType, tmp2));
            m.Return();
        });
    }

    [Test]
    public void Larw_Add16to32()
    {
        var sExp =
        #region Expected
@"ProcedureBuilder_entry:
    def bx:word16
    def dx_ax:word32
l1:
    v10 = bx + 2<16>
    v11 = Mem6[v10:word16]
    v21 = bx + 6<16>
    v22 = Mem6[v21:word16]
    v27 = bx + 8<16>
    v28 = Mem6[v27:word16]
    v40 = SEQ(0<16>, v11)
    v41 = dx_ax + v40
    v44 = SEQ(v28, v22)
    v45 = v41 + v44
    ax_23 = SLICE(v45, word16, 0)
    dx_31 = SLICE(v45, word16, 16)
    CZS_32 = cond(v45)
    ax_12 = SLICE(v41, word16, 0)
    dx_19 = SLICE(v41, word16, 16)
    CZS_24 = cond(ax_23)
    CZS_13 = cond(ax_12)
    C_30 = CZS_24 & 1<32>
    C_18 = CZS_13 & 1<32>
    return
ProcedureBuilder_exit:
    use ax:ax_23
    use dx:dx_31
    use CZS:CZS_32
";
        #endregion

        RunTest(sExp, m =>
        {
            m.Assign(ax, m.IAdd(ax, m.Mem16(m.IAdd(bx, 2))));
            m.Assign(SCZ, m.Cond(SCZ.DataType, ax));
            m.Assign(dx, m.IAddC(dx, m.Word16(0), CF));

            m.Assign(ax, m.IAdd(ax, m.Mem16(m.IAdd(bx, 6))));
            m.Assign(SCZ, m.Cond(SCZ.DataType, ax));
            m.Assign(dx, m.IAddC(dx, m.Mem16(m.IAdd(bx, 8)), CF));
            m.Assign(SCZ, m.Cond(SCZ.DataType, dx));
            m.Return();
        });
    }

    [Test(Description = "PDP-11 had a single operand ADC instruction")]
    public void Larw_Pdp11LongAdd()
    {
        var sExp =
        #region Expected
@"ProcedureBuilder_entry:
    def dx_ax:word32
    def bx_cx:word32
l1:
    v30 = dx_ax + bx_cx
    ax_9 = SLICE(v30, word16, 0)
    dx_19 = SLICE(v30, word16, 16)
    CZS_17 = cond(v30)
    CZS_10 = cond(ax_9)
    C_15 = CZS_10 & 1<32>
    return
ProcedureBuilder_exit:
    use ax:ax_9
    use dx:dx_19
    use CZS:CZS_17
";
        #endregion
        RunTest(sExp, m =>
        {
            m.Assign(ax, m.IAdd(ax, cx));
            m.Assign(SCZ, m.Cond(SCZ.DataType, ax));
            m.Assign(dx, m.IAddC(dx, m.Word16(0), CF));
            m.Assign(SCZ, m.Cond(SCZ.DataType, dx));
            m.Assign(dx, m.IAdd(dx, bx));
            m.Return();
        });
    }

    /// <summary>
    /// An add followed by a subc shouldn't be fused into a long add.
    /// </summary>
    [Test]
    public void Larw_Non_related_add_sbc()
    {
        var sExp =
        #region Expected
@"ProcedureBuilder_entry:
    def ax:word16
    def rdx:word64
l1:
    ax_8 = ax + ax
    CZS_9 = cond(ax_8)
    C_10 = SLICE(CZS_9, bool, 1)
    rdx_14 = __subc<word64,word32>(rdx, 3<64>, C_10)
    return
ProcedureBuilder_exit:
    use ax:ax_8
    use rdx:rdx_14
    use CZS:CZS_9
";
        #endregion
        RunTest(sExp, m =>
        {
            m.Assign(ax, m.IAdd(ax, ax));
            m.Assign(SCZ, m.Cond(SCZ.DataType, ax));
            m.Assign(CF, m.Slice(SCZ, PrimitiveType.Bool, 1));
            m.Assign(rdx, m.ISubC(rdx, m.Word64(3), CF));
            m.Return();
        });
    }


    [Test]
    public void LarwZeroExtendSmallerAugend()
    {
        var sExp =
        #region Expected
@"ProcedureBuilder_entry:
    def r7:byte
l1:
    b_a_9 = r7 *u16 6<8>
    b_10 = SLICE(b_a_9, byte, 8)
    v27 = SLICE(b_a_9, byte, 0)
    v28 = SEQ(0<8>, v27)
    v30 = v28 + 0x14<16>
    a_20 = SLICE(v30, byte, 8)
    a_13 = SLICE(v30, byte, 0)
    CZS_14 = cond(a_13)
    a_11 = SLICE(b_a_9, byte, 0)
    C_19 = CZS_14 & 1<32>
    return
ProcedureBuilder_exit:
    use b:b_10
    use a:a_20
    use DPH:a_20
    use DPL:a_13
    use CZS:CZS_14
";
        #endregion

        RunTest(sExp, m =>
        {
            var R7 = m.Reg8("r7", 0);
            var A = m.Reg8("a", 1);
            var B = m.Reg8("b", 2);
            var B_A = m.Frame.EnsureSequence(PrimitiveType.Word16, B.Storage, A.Storage);
            var psw = RegisterStorage.Reg16("psw", 3);
            var DPTR = m.Reg16("DPTR", 4);
            var DPL = m.Register(RegisterStorage.Reg8("DPL", 4, 0));
            var DPH = m.Register(RegisterStorage.Reg8("DPH", 4, 8));

            m.Assign(A, R7);
            m.Assign(B, 6);
            m.Assign(B_A, m.UMul(PrimitiveType.Word16, A, B));
            m.Assign(A, m.IAdd(A, 0x14));
            m.Assign(SCZ, m.Cond(SCZ.DataType, A));
            m.Assign(DPL, A);
            m.Assign(A, 0);
            m.Assign(A, m.IAddC(A, m.Word16(0), CF));
            m.Assign(DPH, A);

            m.Return();
        });
    }

    [Test]
    public void LarwNegateLong()
    {
        var sExp =
        #region Expected
@"ProcedureBuilder_entry:
    def dx_ax:word32 # [ ax_12, dx_7, v27 ]
l1:
    v27 = -dx_ax # [ ax_13, dx_19 ]
    ax_13 = SLICE(v27, word16, 0) # [ v15, ax_21 ]
    dx_19 = SLICE(v27, word16, 16) # [ dx_22 ]
    v15 = ax_13 != 0<16> # [ C_16 ]
    C_16 = cond(v15) # [ C_23 ]
    return # [  ]
ProcedureBuilder_exit:
    use ax:ax_13 # [  ]
    use dx:dx_19 # [  ]
    use C:C_16 # [  ]
";
        #endregion

        RunTest(sExp, m =>
        {
            var ax = m.Reg16("ax", 0);
            var dx = m.Reg16("dx", 2);

            m.Assign(dx, m.Neg(dx));
            m.Assign(CF, m.Cond(CF.DataType, m.Ne0(dx)));
            m.Assign(ax, m.Neg(ax));
            m.Assign(CF, m.Cond(CF.DataType, m.Ne0(ax)));
            m.Assign(dx, m.ISubB(dx, m.Word16(0), CF));

            m.Return();
        }, true);
    }

    [Test]
    public void LarwNegateLong_c_set_before_negation()
    {
        var sExp =
        #region Expected
@"ProcedureBuilder_entry:
    def dx_ax:word32
l1:
    v25 = -dx_ax
    ax_14 = SLICE(v25, word16, 0)
    dx_17 = SLICE(v25, word16, 16)
    ax_11 = SLICE(dx_ax, word16, 0)
    C_13 = ax_11 != 0<16>
    return
ProcedureBuilder_exit:
    use ax:ax_14
    use dx:dx_17
    use C:C_13
";
        #endregion

        RunTest(sExp, m =>
        {
            var ax = m.Reg16("ax", 0);
            var dx = m.Reg16("dx", 2);

            m.Assign(CF, m.Ne0(dx));
            m.Assign(dx, m.Neg(dx));
            m.Assign(CF, m.Ne0(ax));
            m.Assign(ax, m.Neg(ax));
            m.Assign(dx, m.ISubB(dx, m.Word16(0), CF));

            m.Return();
        });
    }

    [Test]
    public void LarwNegateLong_xor_neg()
    {
        var sExp =
        #region Expected
@"ProcedureBuilder_entry:
    def ax:word16
l1:
    v23 = SEQ(0<16>, ax)
    v24 = -v23
    ax_14 = SLICE(v24, word16, 0)
    dx_17 = SLICE(v24, word16, 16)
    CZS_18 = cond(v24)
    return
ProcedureBuilder_exit:
    use ax:ax_14
    use dx:dx_17
    use CZS:CZS_18
";
        #endregion

        RunTest(sExp, m =>
        {
            var ax = m.Reg16("ax", 0);
            var dx = m.Reg16("dx", 2);

            m.Assign(dx, m.Word16(0));
            m.Assign(CF, m.Ne0(dx));
            m.Assign(dx, m.Neg(dx));
            m.Assign(CF, m.Ne0(ax));
            m.Assign(ax, m.Neg(ax));
            m.Assign(dx, m.ISubB(dx, m.Word16(0), CF));
            m.Assign(SCZ, m.Cond(SCZ.DataType, dx));

            m.Return();
        });
    }

    [Test]
    public void Larw_LongShiftRight()
    {
        var sExp =
        #region Expected
@"ProcedureBuilder_entry:
    def cl:byte
    def dx_ax:word32
l1:
    v29 = dx_ax >>u32 cl
    ax_16 = SLICE(v29, word16, 0)
    Mem18[0x1234<16>:word16] = ax_16
    dx_11 = SLICE(v29, word16, 16)
    Mem20[0x1236<16>:word16] = dx_11
    dx_7 = SLICE(dx_ax, word16, 16)
    cl_12 = -cl
    cl_14 = cl_12 + 0x10<8>
    bx_15 = dx_7 <<16 cl_14
    return
ProcedureBuilder_exit:
    use ax:ax_16
    use bx:bx_15
    use cl:cl_14
    use dx:dx_11
";
        #endregion

        RunTest(sExp, m =>
        {
            var ax = m.Reg16("ax", 0);
            var cl = m.Reg8("cl", 1);
            var dx = m.Reg16("dx", 2);
            var bx = m.Reg16("bx", 3);

            m.Assign(bx, dx);
            m.Assign(ax, m.Shr(ax, cl));
            m.Assign(dx, m.Shr(dx, cl));
            m.Assign(cl, m.Neg(cl));
            m.Assign(cl, m.IAdd(cl, 0x10));
            m.Assign(bx, m.Shl(bx, cl));
            m.Assign(ax, m.Or(ax, bx));
            m.MStore(m.Word16(0x1234), ax);
            m.MStore(m.Word16(0x1236), dx);

            m.Return();
        });
    }

    /*
bx = ax
ax = ax << cl
SCZO = cond(ax)
dx = dx << cl
SCZO = cond(dx)
C = cl != 0x00
cl = -cl
SZO = cond(cl)
cl = cl + 0x10
SCZO = cond(cl)
bx = bx >>u cl
SCZO = cond(bx)
dx = dx | bx
SZ = cond(dx)
O = 0x00
C = 0x00


bx = dx
ax = ax >>u cl
SCZO = cond(ax)
dx = dx >> cl
SCZO = cond(dx)
C = cl != 0x00
cl = -cl
SZO = cond(cl)
cl = cl + 0x10
SCZO = cond(cl)
bx = bx << cl
SCZO = cond(bx)
ax = ax | bx
SZ = cond(ax)
O = 0x00
C = 0x00


r8 = 0x20 - r12
VNZC = cond(r8)
r6 = r6 | r7
NZ = cond(r6)
r7 = r7 >>u r12
NZC = cond(r7)
r9 = r10 << r8
NZC = cond(r9)
r7 = r7 | r9
NZ = cond(r7)
r10 = r10 >>u r12
NZC = cond(r10)
r9 = r11 << r8
NZC = cond(r9)
r10 = r10 | r9
NZ = cond(r10)
r11 = r11 >>u r12
NZC = cond(r11)

     * 
     */


    [Test]
    public void LarwMipsNegate()
    {
        var sExpected =
        #region Expected
@"ProcedureBuilder_entry:
    def r5_r4:word64
l1:
    v28 = -r5_r4
    r4_8 = SLICE(v28, word32, 0)
    Mem17[0x123400<32>:word32] = r4_8
    r5_15 = SLICE(v28, word32, 32)
    Mem19[0x123404<32>:word32] = r5_15
    v12 = r4_8 <u 0<32>
    r9_13 = CONVERT(v12, bool, word32)
    return
ProcedureBuilder_exit:
    use r2:0xFFFFFFFF<32>
    use r4:r4_8
    use r5:r5_15
    use r9:r9_13
";
        #endregion

        RunTest(sExpected, m =>
        {
            var r4 = m.Reg32("r4", 4);
            var r5 = m.Reg32("r5", 5);
            var r9 = m.Reg32("r9", 9);
            var r2 = m.Reg32("r2", 2);

            m.Assign(r4, m.Neg(r4));
            m.Assign(r5, m.Neg(r5));
            m.Assign(r9, m.Convert(m.Ult0(r4), PrimitiveType.Bool, PrimitiveType.Word32));
            m.Assign(r2, m.Word32(~0u));
            m.Assign(r5, m.ISub(r5, r9));
            m.MStore(m.Word32(0x123400), r4);
            m.MStore(m.Word32(0x123404), r5);

            m.Return();
        });
    }

    [Test]
    public void LarwM68xNegate()
    {
        var sExpected =
        #region Expected
@"ProcedureBuilder_entry:
    def d1_d0:word64
l1:
    v24 = -d1_d0
    d0_8 = SLICE(v24, word32, 0)
    d1_15 = SLICE(v24, word32, 32)
    CZS_16 = cond(v24)
    CZS_9 = cond(d0_8)
    C_11 = CZS_9 & 4<32>
    return
ProcedureBuilder_exit:
    use d0:d0_8
    use d1:d1_15
    use CZS:CZS_16
";
        #endregion

        RunTest(sExpected, m =>
        {
            var d0 = m.Reg32("d0", 0);
            var d1 = m.Reg32("d1", 1);

            m.Assign(d0, m.Neg(d0));
            m.Assign(SCZ, m.Cond(SCZ.DataType, d0));
            m.Assign(CF, m.And(SCZ, 4));
            m.Assign(d1, m.ISubC(m.Zero(d1.DataType), d1, CF));
            m.Assign(SCZ, m.Cond(SCZ.DataType, d1));

            m.Return();
        });
    }
}
