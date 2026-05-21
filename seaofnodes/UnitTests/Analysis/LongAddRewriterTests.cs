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
using ProgramDataFlow = Reko.Analysis.ProgramDataFlow;
using Reko.Core;
using Reko.Core.Analysis;
using Reko.Core.Expressions;
using Reko.Core.Operators;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Analysis;
using Reko.Extras.SeaOfNodes.Nodes;
using Reko.Extras.SeaOfNodes.UnitTests;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Reko.Extras.SeaOfNodes.UnitTests.Analysis;

[TestFixture]
public class LongAddRewriterTests
{
    private IStorageBinder binder;
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
        arch = new FakeArchitecture(new ServiceContainer());
        var platform = new FakePlatform(sc, arch);
        program = new Program()
        {
            Architecture = arch,
            Platform = platform,
            SegmentMap = new SegmentMap(Address.Ptr32(0))
        };
        sc = new ServiceContainer();
        m = new ProcedureBuilder(arch);
        binder = m.Frame;
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
        string sExp,
        Action<ProcedureBuilder> builder,
        bool includeOutputRefs = false,
        [CallerMemberName] string testName = "")
    {
        builder(m);
        var dynamicLinker = new Mock<IDynamicLinker>();
        var factory = new NodeFactory();
        var ngb = new NodeGraphBuilder(factory, programFlow, program.Architecture);
        var graph = ngb.Transform(m.Procedure);

        var peep = new PeepholeOptimizer(factory);
        rw = new LongAddRewriter(peep);

        var graphNew = rw.Transform(graph);

        var writer = new StringWriter();
        new NodeGraphRenderer().Render(graphNew, writer, includeOutputRefs);
        var sActual = writer.ToString();
        if (sExp != sActual)
        {
            Console.WriteLine($"** {testName} failed ******");
            Console.WriteLine("Expected:");
            Console.WriteLine(sExp);
            Console.WriteLine("Actual:");
            Console.WriteLine(sActual);
            Console.WriteLine();
            Assert.That(sActual, Is.EqualTo(sExp));
        }
    }

    [Test]
    public void Larw_add()
    {
        var sExp =
@"ProcedureBuilder_entry:
    def ax:word16
    def cx:word16
    def dx:word16
    def bx:word16
l1:
    n22 = SEQ(dx, ax)
    n23 = SEQ(bx, cx)
    n24 = n22 + n23
    n9 = SLICE(n24, word16, 0)
    n16 = SLICE(n24, word16, 16)
    CZ_17 = cond(n24)
    CZ_10 = cond(n9)
    C_15 = CZ_10 & 1<32>
    return
ProcedureBuilder_exit:
    use ax:n9
    use dx:n16
    use CZ:CZ_17
";
        RunTest(sExp, m =>
        {
            m.Assign(ax, m.IAdd(ax, cx));
            m.Assign(SCZ, m.Cond(SCZ.DataType, ax));
            m.Assign(dx, m.IAdd(m.IAdd(dx, bx), CF));
            m.Assign(SCZ, m.Cond(SCZ.DataType, dx));
            m.Return();
        });        
    }

    [Test]
    public void Larw_AddChain()
    {
        var sExp =
@"ProcedureBuilder_entry:
    def ax:word16 # [ n42, n47, n52 ]
    def dx:word16 # [ n15, n42, n47, n52 ]
    def bx:word16 # [ n23, n47, n52 ]
    def cx:word16 # [ n31, n52 ]
l1:
    n9 = Mem6[0x1234<32>:word16] # [ n43, n48, n53 ]
    n14 = Mem6[0x1236<32>:word16] # [ n15, n43, n48, n53 ]
    n22 = Mem6[0x1238<32>:word16] # [ n23, n48, n53 ]
    n30 = Mem6[0x123A<32>:word16] # [ n31, n53 ]
    n52 = SEQ(cx, bx, dx, ax) # [ n54 ]
    n53 = SEQ(n30, n22, n14, n9) # [ n54 ]
    n54 = n52 + n53 # [ n49, n34, CZ_35 ]
    n49 = SLICE(n54, cuiposr48, 0) # [ n44, n26, CZ_27 ]
    n44 = SLICE(n49, uipr32, 0) # [ n10, n18, CZ_19 ]
    n10 = SLICE(n44, word16, 0) # [ CZ_11, ax_37 ]
    n26 = SLICE(n49, word16, 32) # [ bx_38 ]
    n34 = SLICE(n54, word16, 48) # [ cx_39 ]
    n18 = SLICE(n44, word16, 16) # [ dx_40 ]
    CZ_35 = cond(n54) # [ CZ_41 ]
    CZ_27 = cond(n49) # [ C_33 ]
    CZ_19 = cond(n44) # [ C_25 ]
    CZ_11 = cond(n10) # [ C_17 ]
    C_33 = CZ_27 & 1<32> # [  ]
    C_25 = CZ_19 & 1<32> # [  ]
    C_17 = CZ_11 & 1<32> # [  ]
    return # [  ]
ProcedureBuilder_exit:
    use ax:n10 # [  ]
    use bx:n26 # [  ]
    use cx:n34 # [  ]
    use dx:n18 # [  ]
    use CZ:CZ_35 # [  ]
";
        RunTest(sExp, m =>
        {
            m.Assign(ax, m.IAdd(ax, m.Mem16(m.Word32(0x001234))));
            m.Assign(SCZ, m.Cond(SCZ.DataType, ax));
            m.Assign(dx, m.IAdd(m.IAdd(dx, m.Mem16(m.Word32(0x001236))), CF));
            m.Assign(SCZ, m.Cond(SCZ.DataType, dx));
            m.Assign(bx, m.IAdd(m.IAdd(bx, m.Mem16(m.Word32(0x001238))), CF));
            m.Assign(SCZ, m.Cond(SCZ.DataType, bx));
            m.Assign(cx, m.IAdd(m.IAdd(cx, m.Mem16(m.Word32(0x00123A))), CF));
            m.Assign(SCZ, m.Cond(SCZ.DataType, cx));
            m.Return();
        }, includeOutputRefs: true);
    }


    [Test]
    public void Larw_Match_AddRecConst()
    {
        var sExp =
@"ProcedureBuilder_entry:
    def ax:word16
    def dx:word16
l1:
    ax_9 = ax + 0x5678<16>
    n13 = dx + 0x1234<16>
    C_10 = cond(ax_9)
    dx_14 = n13 +16 C_10
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
            m.Assign(dx, m.IAdd(m.IAdd(dx, 0x1234), CF));
            m.Return();
        });
    }

    [Test]
    public void Larw_Match_AddConstant()
    {
        var sExp =
@"l1:
	dx_ax_6 = SEQ(dx, ax)
	dx_ax_7 = dx_ax_6 + 1<32>
	ax_2 = SLICE(dx_ax_7, word16, 0) (alias)
	dx_5 = SLICE(dx_ax_7, word16, 16) (alias)
	C_3 = cond(ax_2)
	return
";
        RunTest(sExp, m =>
        {
            m.Assign(ax, m.IAdd(ax, 1));
            m.Assign(CF, m.Cond(CF.DataType, ax));
            m.Assign(dx, m.IAdd(m.IAdd(dx, 0), CF));
            m.Return();
        });
    }

    [Test]
    public void Larw_Replace_AddReg()
    {
        var sExp =
@"ProcedureBuilder_entry:
    def ax:word16
    def bx:word16
    def dx:word16
l1:
    n10 = bx + 0x300<16>
    n11 = Mem11[n10:word16]
    n16 = bx + 0x302<16>
    n17 = Mem17[n16:word16]
    n25 = SEQ(dx, ax)
    n26 = SEQ(n17, n11)
    n27 = n25 + n26
    n12 = SLICE(n27, word16, 0)
    n19 = SLICE(n27, word16, 16)
    C_20 = cond(n27)
    C_13 = cond(n12)
    return
ProcedureBuilder_exit:
    use ax:n12
    use dx:n19
    use C:C_20
";
        RunTest(sExp, m =>
        {
            m.Assign(ax, m.IAdd(ax, m.Mem16(m.IAdd(bx, 0x300))));
            m.Assign(CF, m.Cond(CF.DataType, ax));
            m.Assign(dx, m.IAdd(m.IAdd(dx, m.Mem16(m.IAdd(bx, 0x302))), CF));
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
    n9 = cx - 0x30<16>
    CZ_10 = cond(n9)
    C_13 = CZ_10 & 1<32>
    ax_14 = 0<16> +16 C_13
    n22 = ax_14 + ax_14
    n17 = cx - 0x3A<16>
    CZ_18 = cond(n17)
    C_20 = CZ_18 & 1<32>
    C_21 = !C_20
    ax_23 = n22 +16 C_21
    CZ_24 = cond(ax_23)
    CZ_15 = cond(ax_14)
    return
ProcedureBuilder_exit:
    use ax:ax_23
    use CZ:CZ_24
";
        RunTest(sExp, m =>
        {
            m.Assign(SCZ, m.Cond(SCZ.DataType, m.ISub(cx, 0x0030)));
            m.Assign(ax, m.IAdd(m.Word16(0x0000), CF));
            m.Assign(SCZ, m.Cond(SCZ.DataType, ax));
            m.Assign(SCZ, m.Cond(SCZ.DataType, m.ISub(cx, 0x003A)));
            m.Assign(CF, m.Not(CF));
            m.Assign(ax, m.IAdd(m.IAdd(ax, ax), CF));
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
    ax_8 = Mem8[0210:word16]
    dx_10 = Mem10[0212:word16]
    es_cx_12 = Mem12[0214:word32]
    n32 = SEQ(dx_10, ax_8)
    n33 = n32 - es_cx_12
    n15 = SLICE(n33, word16, 0)
    Mem18[0218:word16] = n15
    n22 = SLICE(n33, word16, 16)
    Mem24[021A:word16] = n22
    bx_13 = SLICE(es_cx_12, word16, 16)
    cx_14 = SLICE(es_cx_12, word16, 0)
    CZ_16 = cond(n15)
    C_21 = CZ_16 & 1<32>
    return
    // succ: ProcedureBuilder_exit
ProcedureBuilder_exit:
    use es:bx_13
    use cx:cx_14
    use ax:n15
    use bx:bx_13
    use dx:n22
    use CZ:CZ_16
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
            m.Assign(dx, m.ISub(m.ISub(dx, bx), this.CF));
            m.MStore(m.Ptr16(0x21A), dx);
            m.Return();
        });
    }

    // We don't wish to carry out a long-add replacement if the ADC part is in 
    // a different block from the ADD part. A really pathological program might
    // have this behavior, at which point we might need to reconsider.
    [Test]
    public void Larw_do_not_span_multiple_blocks()
    {
        var sExp =
        #region Expected
@"l1:
	ax_2 = Mem0[0x0210<p16>:word16]
	dx_3 = Mem0[0x0212<p16>:word16]
	ax_4 = ax_2 + Mem0[0x0220<p16>:word16]
	SCZ_5 = cond(ax_4)
	C_6 = SCZ_5 & 4<32> (alias)
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
            m.Assign(dx, m.IAdd(m.IAdd(dx, m.Mem16(m.Ptr16(0x0222))), this.CF));
            m.Return();
        });
    }

    [Test]
    public void Larw_Multiply_Accumulate()
    {
        var sExp =
@"l1:
	eax_2 = CONVERT(Mem0[0x5418<32>:word16], word16, int32)
	edx_3 = 0xF000<32>
	edx_eax_4 = edx_3 *s64 eax_2
	eax_5 = SLICE(edx_eax_4, word32, 0) (alias)
	edx_9 = SLICE(edx_eax_4, word32, 32) (alias)
	edx_eax_17 = SEQ(edx_9, eax_5)
	tmp2_tmp1_18 = Mem0[0x6FF0<32>:ui64] - edx_eax_17
	tmp1_6 = SLICE(tmp2_tmp1_18, word32, 0) (alias)
	tmp2_11 = SLICE(tmp2_tmp1_18, word32, 32) (alias)
	Mem7[0x6FF0<32>:word32] = tmp1_6
	SCZ_8 = cond(tmp1_6)
	C_10 = SCZ_8 & 4<32> (alias)
	Mem12[0x6FF4<32>:word32] = tmp2_11
	SCZ_13 = cond(tmp2_11)
	C_14 = SCZ_13 & 4<32> (alias)
	S_15 = SCZ_13 & 1<32> (alias)
	Z_16 = SCZ_13 & 2<32> (alias)
	return
";
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
            m.Assign(tmp2, m.ISub(m.ISub(m.Mem32(m.Word32(0x6FF4)), edx), this.CF));
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
    def ax:word16
    def bx:word16
    def dx:word16
l1:
    n10 = bx + 2<16>
    n11 = Mem6[n10:word16]
    n19 = bx + 6<16>
    n20 = Mem6[n19:word16]
    n24 = bx + 8<16>
    n25 = Mem6[n24:word16]
    n36 = SEQ(dx, ax)
    n37 = SEQ(0<16>, n11)
    n38 = n36 + n37
    n41 = SEQ(n25, n20)
    n42 = n38 + n41
    n21 = SLICE(n42, word16, 0)
    n29 = SLICE(n42, word16, 16)
    CZ_30 = cond(n42)
    CZ_22 = cond(n21)
    C_28 = CZ_22 & 1<32>
    return
ProcedureBuilder_exit:
    use ax:n21
    use dx:n29
    use CZ:CZ_30
";
        #endregion

        RunTest(sExp, m =>
        {
            m.Assign(ax, m.IAdd(ax, m.Mem16(m.IAdd(bx, 2))));
            m.Assign(SCZ, m.Cond(SCZ.DataType, ax));
            //m.Alias(CF, m.Slice(PrimitiveType.Bool, SCZ, 1));
            m.Assign(dx, m.IAdd(dx, CF));

            m.Assign(ax, m.IAdd(ax, m.Mem16(m.IAdd(bx, 6))));
            m.Assign(SCZ, m.Cond(SCZ.DataType, ax));
            //m.Alias(CF, m.Slice(PrimitiveType.Bool, SCZO, 1));
            m.Assign(dx, m.IAdd(m.IAdd(dx, m.Mem16(m.IAdd(bx, 8))), CF));
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
    def ax:word16
    def cx:word16
    def dx:word16
    def bx:word16
l1:
    n22 = SEQ(dx, ax)
    n23 = SEQ(bx, cx)
    n24 = n22 + n23
    n9 = SLICE(n24, word16, 0)
    n14 = SLICE(n24, word16, 16)
    dx_17 = n14 + bx
    CZ_15 = cond(n24)
    CZ_10 = cond(n9)
    C_13 = CZ_10 & 1<32>
    return
ProcedureBuilder_exit:
    use ax:n9
    use dx:dx_17
    use CZ:CZ_15
";
        #endregion
        RunTest(sExp, m =>
        {
            m.Assign(ax, m.IAdd(ax, cx));
            m.Assign(SCZ, m.Cond(SCZ.DataType, ax));
            m.Assign(dx, m.IAdd(dx, CF));
            m.Assign(SCZ, m.Cond(SCZ.DataType, dx));
            m.Assign(dx, m.IAdd(dx, bx));
            m.Return();
        });
    }

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
    n13 = rdx - 3<64>
    CZ_9 = cond(ax_8)
    C_10 = SLICE(CZ_9, bool, 1)
    rdx_14 = n13 -64 C_10
    return
ProcedureBuilder_exit:
    use ax:ax_8
    use rdx:rdx_14
    use CZ:CZ_9
";
        #endregion
        RunTest(sExp, m =>
            {
                m.Assign(ax, m.IAdd(ax, ax));
                m.Assign(SCZ, m.Cond(SCZ.DataType, ax));
                m.Assign(CF, m.Slice(SCZ, PrimitiveType.Bool, 1));
                m.Assign(rdx, m.ISub(m.ISub(rdx, 3), CF));
                m.Return();
            });
    }


    [Test]
    public void LarwZeroExtendSmallerAugend()
    {
        var sExp =
        #region Expected
@"ProcedureBuilder_entry:
    def a:byte
l1:
    b_a_9 = a *u16 6<8>
    b_21 = SLICE(b_a_9, byte, 8)
    n27 = SLICE(b_a_9, byte, 0)
    n28 = SEQ(0<8>, n27)
    n30 = n28 + n29
    n19 = SLICE(n30, byte, 8)
    n12 = SLICE(n30, byte, 0)
    CZ_13 = cond(n12)
    a_10 = SLICE(b_a_9, byte, 0)
    C_18 = CZ_13 & 1<32>
    return
ProcedureBuilder_exit:
    use b:b_21
    use a:n19
    use DPH:n19
    use DPL:n12
    use CZ:CZ_13
"; string old =@"l1:
	a_2 = r7
	b_3 = 6<8>
	b_a_4 = a_2 *u16 b_3
	a_5 = SLICE(b_a_4, byte, 0) (alias)
	a_a_13 = CONVERT(a_5, byte, uint16) + 0x14<16>
	a_6 = SLICE(a_a_13, byte, 0) (alias)
	a_11 = SLICE(a_a_13, byte, 8) (alias)
	SCZ_7 = cond(a_6)
	C_10 = SCZ_7 & 4<32> (alias)
	DPL_8 = a_6
	a_9 = 0<8>
	DPH_12 = a_11
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
            m.Assign(A, m.IAdd(m.IAdd(A, 0), CF));
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
    def dx_ax:word32
l1:
    n12 = -dx_ax
    dx_15 = SLICE(n12, word16, 16)
    ax_13 = SLICE(n12, word16, 0)
    return
ProcedureBuilder_exit:
    use dx:dx_15
    use ax:ax_13
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
        });
    }

    [Test]
    public void LarwNegateLong_c_set_before_negation()
    {
        var sExp =
        #region Expected
@"ProcedureBuilder_entry:
    def dx:word16
    def ax:word16
l1:
    ax_14 = -ax
    dx_10 = -dx
    n16 = dx_10 - 0<16>
    C_13 = ax != 0<16>
    dx_17 = n16 - C_13
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
    ax_14 = -ax
    C_13 = ax != 0<16>
    dx_17 = n16 - C_13
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

            m.Assign(dx, m.Word16(0));
            m.Assign(CF, m.Ne0(dx));
            m.Assign(dx, m.Neg(dx));
            m.Assign(CF, m.Ne0(ax));
            m.Assign(ax, m.Neg(ax));
            m.Assign(dx, m.ISubB(dx, m.Word16(0), CF));

            m.Return();
        });
    }

    [Test]
    public void Larw_LongShiftRight()
    {
        var sExp =
        #region Expected
@"l1:
	bx_2 = dx
	ax_5 = ax >>u cl
	dx_ax_13 = SEQ(dx, ax)
	dx_ax_14 = dx_ax_13 >>u cl
	ax_10 = SLICE(dx_ax_14, word16, 0) (alias)
	dx_6 = SLICE(dx_ax_14, word16, 16) (alias)
	cl_7 = -cl
	cl_8 = cl_7 + 0x10<8>
	bx_9 = bx_2 << cl_8
	Mem11[0x1234<16>:word16] = ax_10
	Mem12[0x1236<16>:word16] = dx_6
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
@"l1:
	r5_r4_10 = SEQ(r5, r4)
	r5_r4_11 = -r5_r4_10
	r4_2 = SLICE(r5_r4_11, word32, 0)
	r5_4 = -r5
	r9_5 = CONVERT(r4_2 <u 0<32>, bool, word32)
	r2_6 = 0xFFFFFFFF<32>
	r5_7 = SLICE(r5_r4_11, word32, 32)
	Mem8[0x123400<32>:word32] = r4_2
	Mem9[0x123404<32>:word32] = r5_7
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
@"l1:
	d1_d0_8 = SEQ(d1, d0)
	d1_d0_9 = -d1_d0_8
	d0_2 = SLICE(d1_d0_9, word32, 0)
	SCZ_3 = cond(d0_2)
	C_4 = SCZ_3 & 4<32>
	d1_6 = SLICE(d1_d0_9, word32, 32)
	SCZ_7 = cond(d1_6)
";
        #endregion

        RunTest(sExpected, m =>
        {
            var d0 = m.Reg32("d0", 0);
            var d1 = m.Reg32("d1", 1);

            m.Assign(d0, m.Neg(d0));
            m.Assign(SCZ, m.Cond(SCZ.DataType, d0));
            m.Assign(CF, m.And(SCZ, 4));
            m.Assign(d1, m.ISub(m.Neg(d1), CF));
            m.Assign(SCZ, m.Cond(SCZ.DataType, d1));

            m.Return();
        });
    }

}
