using NUnit.Framework.Internal;
using Reko.Arch.Zilog;
using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Analysis;
using Reko.Extras.SeaOfNodes.Nodes;
using System.ComponentModel.Design;
using System.Runtime.CompilerServices;
using X86Registers = Reko.Arch.X86.Registers;
using Z80Registers = Reko.Arch.Zilog.Z80.Registers;

namespace Reko.Extras.SeaOfNodes.UnitTests.Analysis;

[Ignore("Not quite read yet")]
[TestFixture]
public class ProjectionPropagatorTests
{
    private IProcessorArchitecture? arch;

    [SetUp]
    public void Setup()
    {
        this.arch = null;
    }

    private void Given_X86_16_Arch()
    {
        this.arch = new Reko.Arch.X86.X86ArchitectureReal(new ServiceContainer(), "x86-real-16", new Dictionary<string, object>());
    }

    private void Given_X86_64_Arch()
    {
        this.arch = new Reko.Arch.X86.X86ArchitectureFlat64(new ServiceContainer(), "x86-protected-64", new Dictionary<string, object>());
    }

    private void Given_Z80_Arch()
    {
        this.arch = new Z80Architecture(new ServiceContainer(), "z80", []);
    }   

    private void RunTest(string sExp, Action<ProcedureBuilder> builder, [CallerMemberName] string testName = "")
    {
        var ath = new AnalysisTestHelper(arch);
        ath.RunTest(sExp, builder, DoTest, false, testName);
    }

    private ProcedureNode DoTest(NodeAnalysisContext ctx, ProcedureNode startNode, AnalysisTestHelper ath)
    {
        var prpr = new ProjectionPropagator(ctx);
        var (newNode, _) = prpr.Transform(startNode);
        return newNode;
    }

    /// <summary>
    /// Fuse together subregisters that compose into a larger register.
    /// </summary>
    [Test]
    public void Prjpr_Simple_Subregisters()
    {
        var sExp =
        #region Expected
@"SsaProcedureBuilder_entry:
	def hl
l1:
	h = SLICE(hl, byte, 8)
	l = SLICE(hl, byte, 0)
	de_1 = hl
	return
SsaProcedureBuilder_exit:
";
        #endregion

        Given_Z80_Arch();

        RunTest(sExp, m =>
        {

            var h = m.Frame.EnsureRegister(Z80Registers.h);
            var l = m.Frame.EnsureRegister(Z80Registers.l);
            var de_1 = m.Frame.EnsureRegister(Z80Registers.de);

            m.Def(h);
            m.Def(l);
            m.Assign(de_1, m.Seq(h, l));
            m.Return();
        });
    }


    /// <summary>
    /// Fuse together subregisters that compose into a larger register.
    /// </summary>
    [Test]
    public void Prjpr_Simple_Subregisters_slices()
    {
        var sExp =
        #region Expected
@"SsaProcedureBuilder_entry:
l1:
	def hl
	l = SLICE(hl, ui8, 0)
	h = SLICE(hl, ui8, 8)
	de_1 = hl
	return
SsaProcedureBuilder_exit:
";
        #endregion

        Given_Z80_Arch();
        RunTest(sExp, m =>
        {
            var hl = m.Frame.EnsureRegister(Z80Registers.hl);
            var h = m.Frame.EnsureRegister(Z80Registers.h);
            var l = m.Frame.EnsureRegister(Z80Registers.l);
            var de_1 = m.Frame.EnsureRegister(Z80Registers.de);

            m.Def(hl);
            m.Assign(l, m.Slice(hl, 0, 8));
            m.Assign(h, m.Slice(hl, 8, 8));
            m.Assign(de_1, m.Seq(h, l));
            m.Return();
        });
    }


    /// <summary>
    /// Fuse together subregisters that compose into a larger register,
    /// used in two branches.
    /// </summary>
    [Test]
    public void Prjpr_Simple_Subregisters_TwoBranches()
    {
        var sExp =
        #region Expected
@"SsaProcedureBuilder_entry:
	def hl
l1:
	h = SLICE(hl, byte, 8)
	l = SLICE(hl, byte, 0)
	branch a == 0<8> m2azero
m1anotzero:
	de_1 = hl
	goto m3done
m2azero:
	de_2 = hl
m3done:
	de_3 = PHI((de_1, m1anotzero), (de_2, m2azero))
	return
SsaProcedureBuilder_exit:
";
        #endregion

        Given_Z80_Arch();
        RunTest(sExp, m =>
        {
            var a = m.Frame.EnsureRegister(Z80Registers.a);
            var h = m.Frame.EnsureRegister(Z80Registers.h);
            var l = m.Frame.EnsureRegister(Z80Registers.l);
            var de_1 = m.Frame.EnsureRegister(Z80Registers.de);
            var de_2 = m.Frame.EnsureRegister(Z80Registers.de);
            var de_3 = m.Frame.EnsureRegister(Z80Registers.de);

            m.Def(h);
            m.Def(l);
            m.BranchIf(m.Eq0(a), "m2azero");

            m.Label("m1anotzero");
            m.Assign(de_1, m.Seq(h, l));
            m.Goto("m3done");

            m.Label("m2azero");
            m.Assign(de_2, m.Seq(h, l));

            m.Label("m3done");
            m.Phi(de_3, (de_1, "m1anotzero"), (de_2, "m2azero"));
            m.Return();
        });
    }


    /// <summary>
    /// Fuse together a register pair.
    /// </summary>
    [Test]
    public void Prjpr_Simple_RegisterPair()
    {
        var sExp =
        #region Expected
@"SsaProcedureBuilder_entry:
	def hl_de
l1:
	hl = SLICE(hl_de, word16, 16)
	de = SLICE(hl_de, word16, 0)
	hl_de_1 = hl_de
	return
SsaProcedureBuilder_exit:
";
        #endregion

        Given_Z80_Arch();
        RunTest(sExp, m =>
        {
            var hl = m.Frame.EnsureRegister(Z80Registers.hl);
            var de = m.Frame.EnsureRegister(Z80Registers.de);
            var hl_de = m.Frame.EnsureSequence(PrimitiveType.Word32, hl.Storage, de.Storage);

            m.Def(hl);
            m.Def(de);
            m.Assign(hl_de, m.Seq(hl, de));
            m.Return();
        });
    }

    [Test]
    public void Prjpr_Phi_Subregisters()
    {
        var sExp =
        #region Expected
@"SsaProcedureBuilder_entry:
	def hl
l1:
	h = SLICE(hl, byte, 8)
	l = SLICE(hl, byte, 0)
	hl_8 = hl (alias)
loop:
	hl_9 = PHI((hl_8, l1), (hl_1, loop))
	h_4 = SLICE(hl_9, byte, 8) (alias)
	l_5 = SLICE(hl_9, byte, 0) (alias)
	hl_1 = hl_9 << 1<8>
	h_2 = SLICE(hl_1, byte, 8)
	l_3 = SLICE(hl_1, byte, 0)
	goto loop
xit:
	return
SsaProcedureBuilder_exit:
";
        #endregion

        Given_Z80_Arch();
        RunTest(sExp, m =>
        {
            var h = m.Frame.EnsureRegister(Z80Registers.h);
            var l = m.Frame.EnsureRegister(Z80Registers.l);
            var hl_1 = m.Frame.EnsureRegister(Z80Registers.de);
            var h_2 = m.Frame.EnsureRegister(Z80Registers.h);
            var l_3 = m.Frame.EnsureRegister(Z80Registers.l);
            var h_4 = m.Frame.EnsureRegister(Z80Registers.h);
            var l_5 = m.Frame.EnsureRegister(Z80Registers.l);

            m.Def(h);
            m.Def(l);

            m.Label("loop");
            m.Phi(h_4, (h, "l1"), (h_2, "loop"));
            m.Phi(l_5, (l, "l1"), (l_3, "loop"));
            m.Assign(hl_1, m.Shl(m.Seq(h_4, l_5), 1));
            m.Assign(h_2, m.Slice(hl_1, PrimitiveType.Byte, 8));
            m.Assign(l_3, m.Slice(hl_1, PrimitiveType.Byte, 0));
            m.Goto("loop");

            m.Label("xit");
            m.Return();
        });
    }

    // Only fuse slices if they are adjacent. If the fused slices
    // don't cover the underlying storage, we must emit a new slice.
    [Test]
    public void Prjpr_Adjacent_Slices()
    {
        var sExp =
        #region Expected
@"SsaProcedureBuilder_entry:
l1:
	def rcx
	t1 = SLICE(rcx, byte, 12)
	t2 = SLICE(rcx, byte, 4)
	cx_3 = SLICE(rcx, word16, 4)
	return
SsaProcedureBuilder_exit:
";
        #endregion
        Given_X86_64_Arch();
        RunTest(sExp, m =>
        {
            var rcx = m.Frame.EnsureRegister(X86Registers.rcx);
            var cx_3 = m.Frame.EnsureRegister(X86Registers.cx);
            var t1 = m.Temp(PrimitiveType.Byte, "t1");
            var t2 = m.Temp(PrimitiveType.Byte, "t2");

            // The slices below are adjacent.
            m.Assign(t1, m.Slice(rcx, PrimitiveType.Byte, 12));
            m.Assign(t2, m.Slice(rcx, PrimitiveType.Byte, 4));
            m.Assign(cx_3, m.Seq(t1, t2));
            m.Return();
        });
    }

    // Only fuse slices if they are adjacent. If the fused slices
    // don't cover the underlying storage, we must emit a new slice.
    [Test]
    public void Prjpr_4_adjacent_Slices()
    {
        var sExp =
        #region Expected
@"SsaProcedureBuilder_entry:
l1:
	def rcx
	t1 = SLICE(rcx, byte, 28)
	t2 = SLICE(rcx, byte, 20)
	t3 = SLICE(rcx, byte, 12)
	t4 = SLICE(rcx, byte, 4)
	ecx_3 = SLICE(rcx, word32, 4)
	return
SsaProcedureBuilder_exit:
";
        #endregion
        Given_X86_64_Arch();
        RunTest(sExp, m =>
        {
            var rcx = m.Frame.EnsureRegister(X86Registers.rcx);
            var ecx_3 = m.Frame.EnsureRegister(X86Registers.cx);
            var t1 = m.Temp(PrimitiveType.Byte, "t1");
            var t2 = m.Temp(PrimitiveType.Byte, "t2");
            var t3 = m.Temp(PrimitiveType.Byte, "t3");
            var t4 = m.Temp(PrimitiveType.Byte, "t4");

            // The slices below are adjacent.
            m.Assign(t1, m.Slice(rcx, PrimitiveType.Byte, 28));
            m.Assign(t2, m.Slice(rcx, PrimitiveType.Byte, 20));
            m.Assign(t3, m.Slice(rcx, PrimitiveType.Byte, 12));
            m.Assign(t4, m.Slice(rcx, PrimitiveType.Byte, 4));
            m.Assign(ecx_3, m.Seq(t1, t2, t3, t4));
            m.Return();
        });
    }

    // Only fuse slices if they are adjacent.
    [Test]
    public void Prjpr_Nonadjacent_Slices()
    {
        var sExp =
        #region Expected
@"SsaProcedureBuilder_entry:
l1:
	def rcx
	t1 = SLICE(rcx, byte, 32)
	t2 = SLICE(rcx, byte, 0)
	cx_3 = SEQ(t1, t2)
	return
SsaProcedureBuilder_exit:
";
        #endregion
        Given_X86_64_Arch();
        RunTest(sExp, m =>
        {
            var rcx = m.Frame.EnsureRegister(X86Registers.rcx);
            var cx_3 = m.Frame.EnsureRegister(X86Registers.cx);
            var t1 = m.Temp(PrimitiveType.Byte, "t1");
            var t2 = m.Temp(PrimitiveType.Byte, "t2");

            // The slices below are not adjacent.
            m.Assign(t1, m.Slice(rcx, PrimitiveType.Byte, 32));
            m.Assign(t2, m.Slice(rcx, PrimitiveType.Byte, 0));
            m.Assign(cx_3, m.Seq(t1, t2));
            m.Return();
        });
    }

    [Test]
    public void Prjpr_Call_To_Sequence()
    {
        var sExp =
        #region Expected
@"SsaProcedureBuilder_entry:
l1:
	call <invalid> (retsize: 2;)
		defs: hl:hl_4
	h_1 = SLICE(hl_4, byte, 8) (alias)
	l_2 = SLICE(hl_4, byte, 0) (alias)
	Mem2[0x1234<16>:cui16] = hl_4
	return
SsaProcedureBuilder_exit:
";
        #endregion

        Given_Z80_Arch();
        RunTest(sExp, m =>
        {
            var h_1 = m.Frame.EnsureRegister(Z80Registers.h);
            var l_2 = m.Frame.EnsureRegister(Z80Registers.l);
            m.Call("dont_care", 2,
                [],
                [ h_1, l_2 ]);
            m.MStore(m.Word16(0x1234), m.Seq(h_1, l_2));
            m.Return();
        });
    }

    [Test]
    public void Prjpr_Call_To_Sequence_Multiple_Uses()
    {
        var sExp =
        #region Expected
@"SsaProcedureBuilder_entry:
l1:
	call <invalid> (retsize: 2;)
		defs: hl:hl_5
	h_1 = SLICE(hl_5, byte, 8) (alias)
	l_2 = SLICE(hl_5, byte, 0) (alias)
	Mem2[0x1234<16>:cui16] = hl_5
	Mem3[0x1236<16>:cui16] = hl_5
	return
SsaProcedureBuilder_exit:
";
        #endregion

        Given_Z80_Arch();
        RunTest(sExp, m =>
        {
            var h_1 = m.Frame.EnsureRegister(Z80Registers.h);
            var l_2 = m.Frame.EnsureRegister(Z80Registers.l);
            m.Call("dont_care", 2,
                [],
                [ h_1, l_2 ]);
            m.MStore(m.Word16(0x1234), m.Seq(h_1, l_2));
            m.MStore(m.Word16(0x1236), m.Seq(h_1, l_2));
            m.Return();
        });
    }

    [Test]
    public void Prjpr_Fuse_RegisterSequence()
    {
        var sExp =
        #region Expected
@"SsaProcedureBuilder_entry:
l1:
	def ds
	es_bx_1 = Mem5[ds:0x1234<16>:ptr32]
	es_2 = SLICE(es_bx_1, selector, 16) (alias)
	bx_3 = SLICE(es_bx_1, word16, 0) (alias)
	Mem6[es_bx_1 + 4<i32>:byte] = 3<8>
	return
SsaProcedureBuilder_exit:
";
        #endregion

        Given_X86_16_Arch();
        RunTest(sExp, m =>
        {
            var ds = m.Frame.EnsureRegister(X86Registers.ds);
            var es_bx_1 = m.Frame.EnsureSequence(PrimitiveType.Ptr32, X86Registers.es, X86Registers.bx);
            var es_2 = m.Frame.EnsureRegister(X86Registers.es);
            var bx_3 = m.Frame.EnsureRegister(X86Registers.bx);
            var ax_4 = m.Frame.EnsureRegister(X86Registers.ax);

            m.Assign(es_bx_1, m.SegMem(PrimitiveType.Ptr32, ds, m.Word16(0x1234)));
            m.Alias(es_2, m.Slice(es_bx_1, es_2.DataType, 16));
            m.Alias(bx_3, m.Slice(es_bx_1, bx_3.DataType, 0));
            m.SStore(es_2, m.IAddS(bx_3, 4), m.Byte(3));
            m.Return();
        });
    }

    /// <summary>
    /// We must be careful not to fuse register sequences if they're used elsewhere.
    /// </summary>
    [Test]
    [Ignore("Needs more thought and possibly a deeper analysis")]
    public void Prjpr_Defs_UsedElsewhere()
    {
        var sExp =
        #region Expected
@"SsaProcedureBuilder_entry:
l1:
	def ds
	def si
	def di
	ax_1 = Mem5[ds:0x1234<16>:ptr32]
	es_2 = SLICE(es_bx_1, selector, 16) (alias)
	bx_3 = SLICE(es_bx_1, word16, 0) (alias)
	Mem6[es_bx_1 + 4:byte] = 0x03
	return
SsaProcedureBuilder_exit:
";
        #endregion

        Given_X86_16_Arch();
        RunTest(sExp, m =>
        {
            var ds = m.Frame.EnsureRegister(X86Registers.ds);
            var si = m.Frame.EnsureRegister(X86Registers.si);
            var di = m.Frame.EnsureRegister(X86Registers.di);
            var ax_1 = m.Frame.EnsureRegister(X86Registers.di);
            var bx_2 = m.Frame.EnsureRegister(X86Registers.di);

            m.Def(ds);
            m.Def(si);
            m.Def(di);
            m.Assign(ax_1, m.SegMem16(ds, si));
            m.Assign(bx_2, m.SegMem16(ds, di));
            m.Return();
        });
    }

    [Test]
    public void Prprj_ReturnedRegisterPair()
    {
        string sExp =
        #region Expected
@"SsaProcedureBuilder_entry:
	def ds
l1:
	es_ax_1 = Mem4[ds:0x1234<16>:word32]
	ax_2 = SLICE(es_ax_1, word16, 0) (alias)
	es_3 = SLICE(es_ax_1, word16, 16) (alias)
	return
SsaProcedureBuilder_exit:
	use ax_2
	use es_3
";
        #endregion
        Given_X86_16_Arch();
        RunTest(sExp, m =>
        {
            var ds = m.Frame.EnsureRegister(X86Registers.ds);
            var es_ax_1 = m.Frame.EnsureSequence(PrimitiveType.Word32, X86Registers.ds, X86Registers.ax);
            var ax_2 = m.Frame.EnsureRegister(X86Registers.ax);
            var es_3 = m.Frame.EnsureRegister(X86Registers.es);

            m.Assign(es_ax_1, m.SegMem(PrimitiveType.Word32, ds, m.Word16(0x1234)));
            m.Return();
        });
    }

    [Test]
    public void Prjpr_Phi_StackVars()
    {
        var sExp =
        #region Expected
@"SsaProcedureBuilder_entry:
m1:
	wLoc06_1 = Mem7[0x1200<16>:word16]
	wLoc08_1 = Mem8[0x1202<16>:word16]
	dwLoc08_13 = SEQ(wLoc08_1, wLoc06_1) (alias)
	branch Mem9[0x1100<16>:byte] == 0<8> m3
m2:
	wLoc06_2 = Mem10[0x1204<16>:word16]
	wLoc08_2 = Mem11[0x1206<16>:word16]
	dwLoc08_14 = SEQ(wLoc08_2, wLoc06_2) (alias)
m3:
	dwLoc08_15 = PHI((dwLoc08_13, m1), (dwLoc08_14, m2))
	wLoc08_3 = SLICE(dwLoc08_15, word16, 0) (alias)
	wLoc06_3 = SLICE(dwLoc08_15, word16, 16) (alias)
	ptrLoc06_1 = dwLoc08_15
	return
SsaProcedureBuilder_exit:
";
        #endregion

        Given_X86_16_Arch();
        RunTest(sExp, m =>
        {
            var wLoc06_1 = m.Local16("wLoc06_1", -6);
            var wLoc06_2 = m.Local16("wLoc06_2", -6);
            var wLoc06_3 = m.Local16("wLoc06_3", -6);

            var wLoc08_1 = m.Local16("wLoc08_1", -8);
            var wLoc08_2 = m.Local16("wLoc08_2", -8);
            var wLoc08_3 = m.Local16("wLoc08_3", -8);
            var ptrLoc06_1 = m.Local32("ptrLoc06_1", -6);

            m.Label("m1");
            m.Assign(wLoc06_1, m.Mem16(m.Word16(0x1200)));
            m.Assign(wLoc08_1, m.Mem16(m.Word16(0x1202)));
            m.BranchIf(m.Eq0(m.Mem8(m.Word16(0x1100))), "m3");

            m.Label("m2");
            m.Assign(wLoc06_2, m.Mem16(m.Word16(0x1204)));
            m.Assign(wLoc08_2, m.Mem16(m.Word16(0x1206)));

            m.Label("m3");
            m.Phi(wLoc08_3, (wLoc08_1, "m1"), (wLoc08_2, "m2"));
            m.Phi(wLoc06_3, (wLoc06_1, "m1"), (wLoc06_2, "m2"));
            m.Assign(ptrLoc06_1, m.Seq(wLoc08_3, wLoc06_3));
            m.Return();
        });
    }

    [Test]
    public void Prjpr_Phi_StackVars_AvoidSlices()
    {
        var sExp =
        #region Expected
@"SsaProcedureBuilder_entry:
m1:
	ptrLoc06_1 = Mem8[0x1200<16>:word32]
	wLoc06_1 = SLICE(ptrLoc06_1, word16, 0) (alias)
	wLoc08_1 = SLICE(ptrLoc06_1, word16, 16) (alias)
	branch Mem9[0x1100<16>:byte] == 0<8> m3
m2:
	wLoc06_2 = Mem10[0x1204<16>:word16]
	wLoc08_2 = Mem11[0x1206<16>:word16]
	dwLoc08_13 = SEQ(wLoc08_2, wLoc06_2) (alias)
m3:
	dwLoc08_14 = PHI((ptrLoc06_1, m1), (dwLoc08_13, m2))
	wLoc08_3 = SLICE(dwLoc08_14, word16, 0) (alias)
	wLoc06_3 = SLICE(dwLoc08_14, word16, 16) (alias)
	ptrLoc06_2 = dwLoc08_14
	return
SsaProcedureBuilder_exit:
";
        #endregion

        Given_X86_16_Arch();
        RunTest(sExp, m =>
        {
            var wLoc06_1 = m.Local16("wLoc06_1", -6);
            var wLoc06_2 = m.Local16("wLoc06_2", -6);
            var wLoc06_3 = m.Local16("wLoc06_3", -6);

            var wLoc08_1 = m.Local16("wLoc08_1", -8);
            var wLoc08_2 = m.Local16("wLoc08_2", -8);
            var wLoc08_3 = m.Local16("wLoc08_3", -8);
            var ptrLoc06_1 = m.Local32("ptrLoc06_1", -6);
            var ptrLoc06_2 = m.Local32("ptrLoc06_2", -6);

            m.Label("m1");
            m.Assign(ptrLoc06_1, m.Mem32(m.Word16(0x1200)));
            m.Alias(wLoc06_1, m.Slice(ptrLoc06_1, PrimitiveType.Word16, 0));
            m.Alias(wLoc08_1, m.Slice(ptrLoc06_1, PrimitiveType.Word16, 16));
            m.BranchIf(m.Eq0(m.Mem8(m.Word16(0x1100))), "m3");

            m.Label("m2");
            m.Assign(wLoc06_2, m.Mem16(m.Word16(0x1204)));
            m.Assign(wLoc08_2, m.Mem16(m.Word16(0x1206)));

            m.Label("m3");
            m.Phi(wLoc08_3, (wLoc08_1, "m1"), (wLoc08_2, "m2"));
            m.Phi(wLoc06_3, (wLoc06_1, "m1"), (wLoc06_2, "m2"));
            m.Assign(ptrLoc06_2, m.Seq(wLoc08_3, wLoc06_3));
            m.Return();
        });
    }

    [Test]
    public void Prjpr_Shrd_sequence()
    {
        var sExp =
        #region Expected
            "@@@";
        #endregion

        RunTest(sExp, m =>
        {
            var eax = m.Reg32("eax", 0);
            var edx = m.Reg32("edx", 2);
            var cl = m.Reg8("cl", 1);
            var tmp = m.Temp(PrimitiveType.Word64, "v3");
            var psw = RegisterStorage.Reg16("psw", 42);
            var SCZO = m.Frame.EnsureFlagGroup(new FlagGroupStorage(psw, 0xF, "SCZO"));

            m.Assign(tmp, m.Seq(edx, eax));
            m.Assign(tmp, m.Shr(tmp, cl));
            m.Assign(eax, m.Slice(tmp, PrimitiveType.Word32, 0));
            m.Assign(edx, m.Shr(edx, cl));
            m.Assign(SCZO, m.Cond(SCZO.DataType, edx));
            m.Return();
        });
    }

    [Test]
    public void Prjpr_long_Shrd_sequence()
    {
        var sExp =
        #region Expected
            "@@@";
        #endregion

        RunTest(sExp, m =>
        {
            var eax = m.Reg32("eax", 0);
            var edx = m.Reg32("edx", 2);
            var ebx = m.Reg32("ebx", 3);
            var cl = m.Reg8("cl", 1);
            var tmp = m.Temp(PrimitiveType.Word64, "v3");
            var psw = RegisterStorage.Reg16("psw", 42);
            var SCZO = m.Frame.EnsureFlagGroup(new FlagGroupStorage(psw, 0xF, "SCZO"));

            m.Assign(tmp, m.Seq(edx, eax));
            m.Assign(tmp, m.Shr(tmp, cl));
            m.Assign(eax, m.Slice(tmp, PrimitiveType.Word32, 0));
            m.Assign(tmp, m.Seq(ebx, edx));
            m.Assign(tmp, m.Shr(tmp, cl));
            m.Assign(edx, m.Slice(tmp, PrimitiveType.Word32, 0));
            m.Assign(ebx, m.Shr(ebx, cl));
            m.Assign(SCZO, m.Cond(SCZO.DataType, ebx));
            m.Return();
        });
    }

}
