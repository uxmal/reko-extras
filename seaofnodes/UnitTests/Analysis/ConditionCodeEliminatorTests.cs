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

using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Intrinsics;
using Reko.Core.Services;
using Reko.Core.Types;
using Reko.Extras.SeaOfNodes.Analysis;
using Reko.Extras.SeaOfNodes.Nodes;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using ProgramDataFlow = Reko.Analysis.ProgramDataFlow;

namespace Reko.Extras.SeaOfNodes.UnitTests.Analysis
{
    [TestFixture]
	public class ConditionCodeEliminatorTests
	{
        private ServiceContainer sc;
        private FakeArchitecture arch;
        private Program program;
        private ProgramDataFlow programFlow;
        private ProcedureBuilder m;

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
            programFlow = new ProgramDataFlow();
        }

        [TearDown]
        public void Dispose()
        {
            sc?.Dispose();
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
            IEventListener listener = new FakeDecompilerEventListener();
            var ctx = new NodeAnalysisContext(program, peep, listener);

            var writer = new StringWriter();
            string sActual;
            try
            {
                var larw = new LongAddRewriter(ctx);
                var larwGraph = larw.Transform(graph);

                var cce = new ConditionCodeEliminator(ctx);
                var cceGraph = cce.Transform(larwGraph);

                writer.WriteLine();
                new NodeGraphRenderer().Render(graph, writer, includeOutputRefs);
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

        private Expression RorC(Expression expr, Expression count, Expression carry)
        {
            return m.Fn(CommonOps.RorC.MakeInstance(expr.DataType, count.DataType), expr, count, carry);
        }

        private Identifier Flags(ProcedureBuilder m, string name, uint grf)
        {
            var f = new FlagGroupStorage(arch.Status, grf, name);
            return m.Frame.EnsureFlagGroup(f);
        }

		[Test]
		public void CceEqId()
		{
            string sExpected =
            #region Expected
@"
ProcedureBuilder_entry:
    def r:word32
foo:
    v9 = r == 0<32>
    if (v9) goto foo
l1:
    return
ProcedureBuilder_exit:
";
            #endregion

            RunTest(sExpected, m =>
            {
                Identifier r = m.Reg32("r", 0);
                Identifier z = Flags(m, "z", 1);  // is a condition code.
                Identifier y = Flags(m, "y", 2);  // is a condition code.

                m.Label("foo");
                m.Assign(z, m.Cond(z.DataType, r));
                m.Assign(y, z);
                m.BranchIf(m.Test(ConditionCode.EQ, y), "foo");

                m.Return();
            });
			//Assert.AreEqual("branch r == 0<32> foo", stmBr.Instruction.ToString());
		}

        [Test]
        public void CceSetnz()
        {
            var sExp =
            #region Expected
@"
ProcedureBuilder_entry:
    def r:word32
    def Mem11
l1:
    f_10 = r != 0<32>
    Mem13[0x123400<32>:word32] = f_10
    v8 = r - 0<32>
    Z_9 = cond(v8)
    return
ProcedureBuilder_exit:
    use f:f_10
    use C:Z_9
    use Mem:Mem_13
";
            #endregion

            RunTest(sExp, m =>
            {
                Identifier r = m.Reg32("r", 0);
                Identifier Z = Flags(m, "Z", 1);
                Identifier f = m.Reg32("f", 2);

                m.Assign(Z, m.Cond(Z.DataType, m.ISub(r, 0)));
                m.Assign(f, m.Test(ConditionCode.NE, Z));
                m.MStore(m.Word32(0x123400), f);
                m.Return();
            });
        }

        [Test]
		public void Cce_SignedIntComparisonFromConditionCode()
        {
            string sExpected =
            #region Expected
                @"
ProcedureBuilder_entry:
    def a:word16
    def b:word16
m1:
    v11 = a < b
    if (v11) goto m1
l1:
    return
ProcedureBuilder_exit:
";
            #endregion

            RunTest(sExpected, m =>
            {
                Identifier a = m.Reg16("a", 0);
                Identifier b = m.Reg16("b", 1);
                Identifier CZ = m.Flags("CZ");
                m.Label("m1");
                m.Assign(CZ, m.Cond(CZ.DataType, m.ISub(a, b)));
                m.BranchIf(m.Test(ConditionCode.LT, CZ), "m1");
                m.Return();
            });
        }

        [Test]
		public void Cce_RealComparisonFromConditionCode()
		{
            string sExpected =
            #region Expected
                @"
ProcedureBuilder_entry:
    def a:word16
    def b:word16
m1:
    v9 = a -f b
    v11 = v9 <f 0.0
    CZ_10 = cond(v9)
    if (v11) goto m1
l1:
    return
ProcedureBuilder_exit:
";
            #endregion

            RunTest(sExpected, m =>
            {
                Identifier a = m.Reg16("a", 0);
                Identifier b = m.Reg16("b", 1);
                Identifier CZ = m.Flags("CZ");
                m.Label("m1");
                m.Assign(CZ, m.Cond(CZ.DataType, m.FSub(a, b)));
                m.BranchIf(m.Test(ConditionCode.LT, CZ), "m1");

                m.Return();
            });
        }

        [Test]
        public void Cce_TypeReferenceComparisonFromConditionCode()
        {
            string sExpected =
            #region Expected
                @"
ProcedureBuilder_entry:
    def a:word16
    def b:word16
l1:
    v8 = a + b
    v10 = v8 < 0<16>
    c_11 = CONVERT(v10, bool, W16)
    CZ_9 = cond(v8)
    return
ProcedureBuilder_exit:
    use c:c_11
    use CZ:CZ_9
";
            #endregion

            RunTest(sExpected, m =>
            {
                var w16 = new TypeReference("W16", PrimitiveType.Word16);
                Identifier a = m.Reg16("a", 0);
                Identifier b = m.Reg16("b", 1);
                Identifier c = m.Reg16("c", 2);
                Identifier CZ = m.Flags("CZ");
                m.Assign(CZ, m.Cond(CZ.DataType, m.IAdd(a, b)));
                m.Assign(c, m.Convert(m.Test(ConditionCode.LT, CZ), PrimitiveType.Bool, w16));
                m.Return();
            });
        }

        [Test]
        public void CceAddAdcPattern()
        {
            string sExpected =
            #region Expected
    @"
ProcedureBuilder_entry:
    def Mem16
    def r3_r1:word64
    def r4_r2:word64
l1:
    v35 = r3_r1 + r4_r2
    r1_8 = SLICE(v35, word32, 0)
    Mem18[0x444400<32>:word32] = r1_8
    r3_15 = SLICE(v35, word32, 32)
    Mem20[0x444404<32>:word32] = r3_15
    SZC_9 = cond(r1_8)
    C_14 = SZC_9 & 1<32>
    ZS_25 = SZC_9 & 6<32>
    CZS_26 = C_14 | ZS_25
    return
ProcedureBuilder_exit:
    use r1:r1_8
    use r3:r3_15
    use CZS:CZS_26
    use Mem:Mem_20
";
            #endregion

            RunTest(sExpected, m =>
            {
                var r1 = m.Reg32("r1", 1);
                var r2 = m.Reg32("r2", 2);
                var r3 = m.Reg32("r3", 3);
                var r4 = m.Reg32("r4", 4);
                var flags = RegisterStorage.Reg32("flags", 0x0A);
                var SCZ = m.Frame.EnsureFlagGroup(new FlagGroupStorage(flags, 0x7, "SZC"));
                var C = m.Frame.EnsureFlagGroup(new FlagGroupStorage(flags, 0x1, "C"));

                m.Assign(r1, m.IAdd(r1, r2));
                m.Assign(SCZ, m.Cond(SCZ.DataType, r1));
                m.Assign(r3, m.IAddC(r3, r4, C));
                m.MStore(m.Word32(0x0444400), r1);
                m.MStore(m.Word32(0x0444404), r3);
                m.Return();
            });
        }

        [Test]
        public void CceShrRcrPattern()
        {
            string sExpected =
            #region Expected
    @"
ProcedureBuilder_entry:
    def Mem15
    def r1_r2:word64
l1:
    v28 = r1_r2 >>u 1<8>
    r2_13 = SLICE(v28, word32, 0)
    Mem17[0x3000<32>:word32] = r2_13
    r1_8 = SLICE(v28, word32, 32)
    Mem19[0x3004<32>:word32] = r1_8
    C_14 = cond(v28)
    C_9 = cond(r1_8)
    return
ProcedureBuilder_exit:
    use r1:r1_8
    use r2:r2_13
    use C:C_14
    use Mem:Mem_19
";
            #endregion

            RunTest(sExpected, m =>
            {
                var C = m.Flags("C");
                var r1 = m.Reg32("r1", 1);
                var r2 = m.Reg32("r2", 2);

                m.Assign(r1, m.Shr(r1, 1));
                m.Assign(C, m.Cond(C.DataType, r1));
                m.Assign(r2, m.Fn(
                    CommonOps.RorC.MakeInstance(r2.DataType, PrimitiveType.Byte),
                    r2, m.Byte(1), C));
                m.Assign(C, m.Cond(C.DataType, r2));
                m.MStore(m.Word32(0x3000), r2);
                m.MStore(m.Word32(0x3004), r1);
                m.Return();
            });
        }

        [Test]
        public void CceShlRclPattern()
        {
            string sExpected =
            #region Expected
                @"
ProcedureBuilder_entry:
    def Mem15
    def r2_r1:word64
l1:
    v28 = r2_r1 << 1<8>
    r1_8 = SLICE(v28, word32, 0)
    Mem17[0x3000<32>:word32] = r1_8
    r2_13 = SLICE(v28, word32, 32)
    Mem19[0x3004<32>:word32] = r2_13
    C_14 = cond(v28)
    C_9 = cond(r1_8)
    return
ProcedureBuilder_exit:
    use r1:r1_8
    use r2:r2_13
    use C:C_14
    use Mem:Mem_19
";
            #endregion

            RunTest(sExpected, m =>
            {
                var C = m.Flags("C");
                var r1 = m.Reg32("r1", 1);
                var r2 = m.Reg32("r2", 2);

                m.Assign(r1, m.Shl(r1, 1));
                m.Assign(C, m.Cond(C.DataType, r1));
                m.Assign(r2, m.Fn(
                    CommonOps.RolC.MakeInstance(r2.DataType, PrimitiveType.Byte),
                    r2, m.Byte(1), C));
                m.Assign(C, m.Cond(C.DataType, r2));
                m.MStore(m.Word32(0x3000), r1);
                m.MStore(m.Word32(0x3004), r2);
                m.Return();
            });
        }

        [Test]
        public void CceUnsignedRange()
        {
            var sExp =
            #region Expected
@"
ProcedureBuilder_entry:
    def r2:word32
    def Mem18
l1:
    r1_10 = r2 - 2<32>
    v16 = r1_10 >u 5<32>
    if (v16) goto mElse
mDo:
    Mem20[00123400:word32] = r2
mElse:
    Mem_22 = PHI(Mem18, Mem_20)
    return
ProcedureBuilder_exit:
    use Mem:Mem_22
";
            #endregion

            RunTest(sExp, m =>
            {
                var r1 = m.Reg32("r1", 1);
                var r2 = m.Reg32("r2", 2);
                var SCZ = m.Flags("SCZ"); 
                var CZ = m.Flags("CZ");

                m.Assign(r1, m.ISub(r2, 2));
                m.Assign(SCZ, m.Cond(SCZ.DataType, m.ISub(r1, 5)));
                m.BranchIf(m.Test(ConditionCode.UGT, CZ), "mElse");

                m.Label("mDo");
                m.MStore(m.Ptr32(0x00123400), r2);

                m.Label("mElse");
                m.Return();
            });
        }

        [Test(Description = "Handle x86-style test/jbe sequence")]
        public void CceTestBe()
        {
            string sExpected =
            #region Expected
               @"
ProcedureBuilder_entry:
    def r1:word32
l1:
    v9 = r1 & r1
    v15 = v9 <=u 0<32>
    ZSO_10 = cond(v9)
    Z_13 = ZSO_10 & 2<32>
    CZ_14 = 0<32> | Z_13
    if (v15) goto yay
nay:
    return 0<32>
yay:
    return 1<32>
ProcedureBuilder_exit:
";
            #endregion
            RunTest(sExpected, m =>
            {
                var SZO = m.Flags("SZO");
                var C = m.Flags("C");
                var CZ = m.Flags("CZ");
                var r1 = m.Reg32("r1", 1);

                m.Assign(SZO, m.Cond(SZO.DataType, m.And(r1, r1)));
                m.Assign(C, 0);
                m.BranchIf(m.Test(ConditionCode.ULE, CZ), "yay");
                m.Label("nay");
                m.Return(m.Word32(0));
                m.Label("yay");
                m.Return(m.Word32(1));
            });

            // Assert.AreEqual("branch r1 <=u 0<32> yay", block.Statements[^1].Instruction.ToString());
        }

        [Test]
        public void CceUInt64()
        {
            var sExp =
            #region Expected
@"
ProcedureBuilder_entry:
    def rax:word64
    def Mem16
l1:
    rdx_10 = rax - 1<64>
    v14 = rdx_10 >u 0x1FFFFFFFFFFFFFFE<64>
    if (v14) goto mElse
mDo:
    Mem18[00123400:word64] = 0x1FFFFFFFFFFFFFFE<64>
mElse:
    Mem_20 = PHI(Mem16, Mem_18)
    return
ProcedureBuilder_exit:
    use Mem:Mem_20
";
            #endregion

            RunTest(sExp, m =>
            {
                var rdx = m.Reg64("rdx", 2);
                var rax = m.Reg64("rax", 0);
                var CZ = m.Flags("CZ");

                m.Assign(rdx, m.ISub(rax, 1));
                m.Assign(rax, m.Word64(0x1FFFFFFFFFFFFFFE));
                m.Assign(CZ, m.Cond(CZ.DataType, m.ISub(rdx, rax)));
                m.BranchIf(m.Test(ConditionCode.UGT, CZ), "mElse");

                m.Label("mDo");
                m.MStore(m.Ptr32(0x00123400), rax);

                m.Label("mElse");
                m.Return();
            });
        }

        [Test]
        public void CceRorcWithIntermediateCopy()
        {
            var sExp =
            #region Expected
@"
ProcedureBuilder_entry:
    def Mem27
    def h:byte
    def l:byte
    def c:byte
m1Loop:
    h_7 = PHI(h, a_12)
    l_14 = PHI(l, a_17)
    v25 = c_19 != 1<8>
    c_19 = PHI(c, c_21)
    a_8 = h_7 | h_7
    v40 = SEQ(a_8, l_14)
    v41 = v40 >>u 1<8>
    a_12 = SLICE(v41, byte, 8)
    a_17 = SLICE(v41, byte, 0)
    c_21 = c_19 - 1<8>
    CZS_9 = cond(a_8)
    C_18 = cond(v41)
    C_13 = cond(a_12)
    ZSO_22 = cond(c_21)
    Z_24 = ZSO_22 & 2<32>
    if (v25) goto m1Loop
m2Done:
    Mem30[0x1000<32>:byte] = a_17
    Mem32[0x1001<32>:byte] = a_12
    return
ProcedureBuilder_exit:
    use h:a_12
    use l:a_17
    use Mem:Mem_32
";
            #endregion
            RunTest(sExp, m =>
            {
                var sp = m.Reg16("sp", 6);
                var a = m.Reg8("a", 0);
                var c = m.Reg8("c", 1);
                var h = m.Reg8("h", 2);
                var l = m.Reg8("l", 3);
                var C = m.Flags("C");
                var Z = m.Flags("Z");
                var SZC = m.Flags("SZC");
                var SZO = m.Flags("SZO");

                m.Label("m1Loop");
                m.Assign(a, h);
                m.Assign(a, m.Or(a, a));
                m.Assign(SZC, m.Cond(SZC.DataType, a));
                m.Assign(C, 0);
                m.Assign(a, m.Shr(a, m.Byte(1)));
                m.Assign(C, m.Cond(C.DataType, a));
                m.Assign(h, a);
                m.Assign(a, l);
                m.Assign(a, m.Fn(CommonOps.RorC.MakeInstance(a.DataType, PrimitiveType.Byte),
                    a, m.Byte(1), C));
                m.Assign(C, m.Cond(C.DataType, a));
                m.Assign(l, a);
                m.Assign(c, m.ISub(c, 1));
                m.Assign(SZO, m.Cond(SZO.DataType, c));
                m.BranchIf(m.Test(ConditionCode.NE, Z), "m1Loop");

                m.Label("m2Done");
                m.MStore(m.Word32(0x1000), l);
                m.MStore(m.Word32(0x1001), h);
                m.Return();
            });
        }

        [Test]
        public void CceUnorderedComparison()
        {
            var sExp =
            #region Expected
@"
ProcedureBuilder_entry:
    def rArg0:real80
    def rArg1:real80
l1:
    v21 = isunordered(rArg0, rArg1)
    v12 = !v21
    if (v12) goto m3Done
m1isNan:
    r0_16 = 0<32>
    return 0<32>
m3Done:
    r0_14 = 1<32>
    return 1<32>
ProcedureBuilder_exit:
    r0_18 = PHI(r0_16, r0_14)
    use r0:r0_18
";
            #endregion

            RunTest(sExp, m =>
            {
                var r0 = m.Reg32("r0", 0);
                var f0 = m.Frame.EnsureFpuStackVariable(0, PrimitiveType.Real80);
                var f1 = m.Frame.EnsureFpuStackVariable(1, PrimitiveType.Real80);
                var C = m.Flags("C");

                m.Assign(C, m.Cond(C.DataType, m.FSub(f0, f1)));
                m.BranchIf(m.Test(ConditionCode.NOT_NAN, C), "m3Done");
                m.Label("m1isNan");
                m.Assign(r0, 0);
                m.Return(r0);
                m.Label("m3Done");
                m.Assign(r0, 1);
                m.Return(r0);
            });
        }

        [Test]
        public void CceMultibitCcFromPhiNode()
        {
            var sExp =
            #region Expected
@"
ProcedureBuilder_entry:
    def r0:word32
    def r2:word32
    def Mem17
l1:
    v11 = r0 <= r2
    if (v11) goto m1
m0:
    r0_15 = r0 + r2
    CZ_16 = cond(r0_15)
    v42 = r0_15 >u 0<32>
    v46 = r0_15 <=u 0<32>
    v50 = r0_15 == 0<32>
    goto m2
m1:
    r0_13 = r2 - r0
    CZ_14 = cond(r0_13)
    v43 = r2 >u r0
    v47 = r2 <=u r0
    v51 = r2 == r0
m2:
    CZ_20 = PHI(CZ_16, CZ_14)
    v21 = PHI(v42, v43)
    v25 = PHI(v46, v47)
    v31 = PHI(v50, v51)
    v22 = CONVERT(v21, bool, int8)
    Mem23[0x123400<32>:int8] = v22
    v26 = CONVERT(v25, bool, int8)
    Mem27[0x123402<32>:int8] = v26
    v32 = CONVERT(v31, bool, int8)
    Mem33[0x123404<32>:int8] = v32
    Z_30 = CZ_20 & 2<32>
    C_36 = CZ_20 & 1<32>
    CZ_37 = Z_30 | C_36
    return
ProcedureBuilder_exit:
    use CZ:CZ_37
    use Mem:Mem_33
";
            #endregion
            RunTest(sExp, m =>
            {
                var r0 = m.Reg32("r0", 0);
                var r2 = m.Reg32("r2", 2);
                var CZ = m.Flags("CZ");
                var Z = m.Flags("Z");
                m.BranchIf(m.Le(r0, r2), "m1");

                m.Label("m0");
                m.Assign(r0, m.IAdd(r0, r2));
                m.Assign(CZ, m.Cond(CZ.DataType, r0));
                m.Goto("m2");

                m.Label("m1");
                m.Assign(r0, m.ISub(r2, r0));
                m.Assign(CZ, m.Cond(CZ.DataType, r0));

                m.Label("m2");
                //m.Assign(tmp, m.Convert(m.Test(ConditionCode.UGT, CZ), PrimitiveType.Bool, PrimitiveType.SByte));
                m.MStore(m.Word32(0x00123400), m.Convert(m.Test(ConditionCode.UGT, CZ), PrimitiveType.Bool, PrimitiveType.SByte));
                m.MStore(m.Word32(0x00123402), m.Convert(m.Test(ConditionCode.ULE, CZ), PrimitiveType.Bool, PrimitiveType.SByte));
                m.MStore(m.Word32(0x00123404), m.Convert(m.Test(ConditionCode.EQ, Z), PrimitiveType.Bool, PrimitiveType.SByte));
                m.Return();
            });
        }

        [Test]
        public void CceShlRcl_Through_Alias()
        {
            var sExp =
            #region Expected
@"
ProcedureBuilder_entry:
    def Mem20
    def r0_r1:word32
l1:
    v36 = r0_r1 << 1<i16>
    r0_15 = SLICE(v36, word16, 16)
    Mem22[0x1234<16>:word16] = r0_15
    r1_8 = SLICE(v36, word16, 0)
    Mem24[0x1236<16>:word16] = r1_8
    r0_10 = SLICE(r0_r1, word16, 16)
    v17 = r0_10 & 0x8000<16>
    C_19 = v17 != 0<16>
    NZVC_9 = cond(r1_8)
    ZSO_29 = NZVC_9 & 0xE<16>
    CZSO_30 = C_19 |1 ZSO_29
    C_14 = NZVC_9 & 1<16>
    return
ProcedureBuilder_exit:
    use r0:r0_15
    use r1:r1_8
    use CZSO:CZSO_30
    use Mem:Mem_24
";
            #endregion
            RunTest(sExp, m =>
            {
                var r1 = m.Reg16("r1", 1);
                var r0 = m.Reg16("r0", 0);
                var psw = RegisterStorage.Reg16("psw", 2);
                var C = m.Frame.EnsureFlagGroup(new FlagGroupStorage(psw, 1, "C"));
                var NZVC = m.Frame.EnsureFlagGroup(new FlagGroupStorage(psw, 0xF, "NZVC"));
                var tmp = m.Frame.CreateTemporary("tmp", PrimitiveType.Word16);
                m.Assign(r1, m.Shl(r1, m.Int16(1)));
                m.Assign(NZVC, m.Cond(NZVC.DataType, r1));
                m.Assign(tmp, r0);
                m.Assign(r0, m.Fn(
                    CommonOps.RolC.MakeInstance(r0.DataType, PrimitiveType.Int16), 
                    r0, m.Int16(1), C));
                m.Assign(C, m.Ne0(m.And(tmp, m.Word16(0x8000))));
                m.MStore(m.Word16(0x1234), r0);
                m.MStore(m.Word16(0x1236), r1);
                m.Return();
            });
        }

        [Test]
        public void CceRorcViaAliases()
        {
            var sExp =
            #region Expected
@"
ProcedureBuilder_entry:
    def h_l_b_c:word32
l1:
    v52 = h_l_b_c >>u 1<8>
    v46 = SLICE(v52, word24, 8)
    b_22 = SLICE(v46, byte, 0)
    c_1_29 = SLICE(v52, byte, 0)
    v40 = SLICE(v46, word16, 8)
    h_1_8 = SLICE(v40, byte, 8)
    l_1_15 = SLICE(v40, byte, 0)
    CZS_30 = cond(v52)
    CZS_23 = cond(v46)
    CZS_16 = cond(v40)
    CZS_9 = cond(h_1_8)
    C_28 = CZS_23 & 1<32>
    C_21 = CZS_16 & 1<32>
    C_14 = CZS_9 & 1<32>
    return
ProcedureBuilder_exit:
    use b:b_22
    use c_1:c_1_29
    use h_1:h_1_8
    use l_1:l_1_15
    use CZS:CZS_30
";
            #endregion
            RunTest(sExp, m =>
            {
                var h = m.Reg8("h", 1);
                var h_1 = m.Reg8("h_1", 1);
                var l = m.Reg8("l", 2);
                var l_1 = m.Reg8("l_1", 2);
                var b = m.Reg8("b", 3);
                var b_1 = m.Reg8("b", 3);
                var c = m.Reg8("c", 4);
                var c_1 = m.Reg8("c_1", 4);
                var flags = RegisterStorage.Reg32("flags", 42);
                var szc = new FlagGroupStorage(flags, 7, "SZC");
                var cy = new FlagGroupStorage(flags, 1, "C");
                var SZC = m.Flags("SZC");
                var C  = m.Flags("C");
                var Bool = PrimitiveType.Bool;

                m.Assign(h_1, m.Shr(h, 1));
                m.Assign(SZC, m.Cond(SZC.DataType, h_1));

                m.Assign(l_1, RorC(l, m.Byte(1), C));
                m.Assign(SZC, m.Cond(SZC.DataType, l_1));

                m.Assign(b_1, RorC(b, m.Byte(1), C));
                m.Assign(SZC, m.Cond(SZC.DataType, b_1));

                m.Assign(c_1, RorC(c, m.Byte(1), C));
                m.Assign(SZC, m.Cond(SZC.DataType, c_1));

                m.Return();

                //h_4 = h_3 >>u 1
                //SZXC_5 = cond(h_4)
                //C_7 = SLICE(SZXC_5, bool, 0) (alias)
                //l_8 = __rcr(l_6, 0x01, C_7)
                //SZXC_9 = cond(l_8)
                //C_11 = SLICE(SZXC_9, bool, 0) (alias)
                //b_12 = __rcr(b_10, 0x01, C_11)
                //SZXC_13 = cond(b_12)
                //C_15 = SLICE(SZXC_13, bool, 0) (alias)
                //c_16 = __rcr(c_14, 0x01, C_15)
                //SZXC_17 = cond(c_16)
            });
        }

        [Test]
        public void CceAdd16to32()
        {
            var sExp =
            #region Expected
@"
ProcedureBuilder_entry:
    def Mem7
    def sp:word16
    def dx_ax:word32
l1:
    v10 = sp + 2<16>
    v11 = Mem7[v10:word16]
    v21 = sp + 6<16>
    v22 = Mem7[v21:word16]
    v27 = sp + 8<16>
    v28 = Mem7[v27:word16]
    v41 = SEQ(0<16>, v11)
    v42 = dx_ax + v41
    v45 = SEQ(v28, v22)
    v46 = v42 + v45
    ax_23 = SLICE(v46, word16, 0)
    dx_31 = SLICE(v46, word16, 16)
    CZSO_32 = cond(v46)
    ax_12 = SLICE(v42, word16, 0)
    dx_19 = SLICE(v42, word16, 16)
    CZSO_24 = cond(ax_23)
    CZSO_13 = cond(ax_12)
    C_30 = CZSO_24 & 1<32>
    C_18 = CZSO_13 & 1<32>
    return
ProcedureBuilder_exit:
    use ax:ax_23
    use dx:dx_31
    use CZSO:CZSO_32
    use Mem:Mem7
"
;
            #endregion

            RunTest(sExp, m =>
            {
                var flags = RegisterStorage.Reg32("flags", 42);
                var sczo = new FlagGroupStorage(flags, 0xF, "SZCO");
                var cy = new FlagGroupStorage(flags, 1, "C");
                var sp = m.Reg16("sp", 5);
                var ax = m.Reg16("ax", 0);
                var dx = m.Reg16("dx", 2);
                var SCZO = m.Flags("SCZO");
                var C = m.Flags("C");

                m.Assign(ax, m.IAdd(ax, m.Mem16(m.IAdd(sp, 2))));
                m.Assign(SCZO, m.Cond(SCZO.DataType, ax));
                m.Assign(dx, m.IAddC(dx, m.Word16(0), C));

                m.Assign(ax, m.IAdd(ax, m.Mem16(m.IAdd(sp, 6))));
                m.Assign(SCZO, m.Cond(SCZO.DataType, ax));
                m.Assign(dx, m.IAddC(dx, m.Mem16(m.IAdd(sp, 8)), C));
                m.Assign(SCZO, m.Cond(SCZO.DataType, dx));
                m.Return();
            });
        }

        [Test]
        public void CceLongShiftInLoop()
        {
            var sExp =
            #region Expected
@"
ProcedureBuilder_entry:
    def cx:word16
    def Mem32
    def ax:word16
    def dx:word16
l1:
    v10 = cx == 0<16>
    if (v10) goto m1Done
m0Loop:
    ax_12 = PHI(ax, ax_14)
    dx_16 = PHI(dx, dx_25)
    cx_28 = cx_26 - 1<16>
    v30 = cx_28 != 0<16>
    cx_26 = PHI(cx, cx_28)
    v47 = SEQ(dx_16, ax_12)
    v48 = v47 << 1<8>
    ax_14 = SLICE(v48, word16, 0)
    dx_25 = SLICE(v48, word16, 16)
    SCZO_15 = cond(ax_14)
    C_24 = SCZO_15 & 1<16>
    if (v30) goto m0Loop
m1Done:
    ax_36 = PHI(ax, ax_14)
    dx_40 = PHI(dx, dx_25)
    Mem38[0x123400<32>:word16] = ax_36
    Mem42[0x123402<32>:word16] = dx_40
    return
ProcedureBuilder_exit:
    use ax:ax_36
    use dx:dx_40
    use Mem:Mem_42
";
            #endregion
            RunTest(sExp, m =>
            {
                var ax = m.Reg16("ax", 0);
                var cx = m.Reg16("cx", 1);
                var dx = m.Reg16("dx", 2);
                var psw = RegisterStorage.Reg16("psw", 2);
                var CF = m.Frame.EnsureFlagGroup(new FlagGroupStorage(psw, 1, "C"));
                var SCZO = m.Frame.EnsureFlagGroup(new FlagGroupStorage(psw, 0xF, "SCZO"));
                var tmp = m.Frame.CreateTemporary(PrimitiveType.Bool);

                m.BranchIf(m.Eq0(cx), "m1Done");

                m.Label("m0Loop");
                m.Assign(ax, m.Shl(ax, 1));
                m.Assign(SCZO, m.Cond(SCZO.DataType, ax));
                m.Assign(tmp, m.Ne0(m.And(dx, 0x8000)));
                m.Assign(dx, m.Fn(
                    CommonOps.RolC.MakeInstance(dx.DataType, PrimitiveType.Byte),
                    dx, m.Byte(1), CF));
                m.Assign(CF, tmp);
                m.Assign(cx, m.ISub(cx, 1));
                m.BranchIf(m.Ne0(cx), "m0Loop");

                m.Label("m1Done");
                m.MStore(m.Word32(0x00123400), ax);
                m.MStore(m.Word32(0x00123402), dx);
                m.Return();
            });
        }

        [Test]
        public void CceForceFlagAlive()
        {
            var sExp =
            #region Expected
@"
ProcedureBuilder_entry:
    def Mem16
    def dx_ax:word32
l1:
    v32 = dx_ax >> 1<16>
    ax_15 = SLICE(v32, word16, 0)
    Mem18[0x1234<16>:word16] = ax_15
    dx_8 = SLICE(v32, word16, 16)
    Mem20[0x1236<16>:word16] = dx_8
    SCZO_9 = cond(dx_8)
    C_14 = SCZO_9 & 1<16>
    ZSO_25 = SCZO_9 & 0xE<16>
    CZSO_26 = C_14 | ZSO_25
    return
ProcedureBuilder_exit:
    use ax:ax_15
    use dx:dx_8
    use CZSO:CZSO_26
    use Mem:Mem_20
";
            #endregion

            RunTest(sExp, m =>
            {
                var ax = m.Reg16("ax", 0);
                var dx = m.Reg16("dx", 2);
                var psw = RegisterStorage.Reg16("psw", 2);
                var CF = m.Frame.EnsureFlagGroup(new FlagGroupStorage(psw, 1, "C"));
                var SF = m.Frame.EnsureFlagGroup(new FlagGroupStorage(psw, 8, "S"));
                var SCZO = m.Frame.EnsureFlagGroup(new FlagGroupStorage(psw, 0xF, "SCZO"));

                m.Assign(dx, m.Sar(dx, 1));
                m.Assign(SCZO, m.Cond(SCZO.DataType, dx));
                m.Assign(ax, RorC(ax, m.Word16(1), CF));
                m.MStore(m.Word16(0x1234), ax);
                m.MStore(m.Word16(0x1236), dx);
                m.Return();
            });
        }

        [Test]
        public void CceGithub1168()
        {
            var sExp =
            #region Expected
@"
ProcedureBuilder_entry:
    def r2:word32
    def Mem24
    def ctr:word32
m1:
    r2_13 = r2 & 0x7F<32>
    SCZO_14 = cond(r2_13)
    v46 = r2_13 == 0<32>
m2C:
    r2_15 = PHI(r2_13, r2_17, r2_38)
    r2_17 = r2_15 >>u 1<8>
    v19 = r2_17 == 0<32>
    SCZO_21 = PHI(SCZO_14, SCZO_21, SCZO_39)
    ctr_30 = PHI(ctr, ctr_30, ctr_33)
    v22 = PHI(v46, v22, v47)
    if (v19) goto m2C
m3:
    if (v22) goto m80
m4:
    r2_26 = Mem24[0x123400<32>:word32]
    r2_28 = r2_26 & 0x7F<32>
    SCZO_29 = cond(r2_28)
    v49 = r2_28 == 0<32>
m80:
    r2_38 = PHI(r2_17, r2_28)
    SCZO_39 = PHI(SCZO_21, SCZO_29)
    v47 = PHI(v22, v49)
    ctr_33 = ctr_30 - 1<i32>
    v35 = ctr_33 != 0<32>
    if (v35) goto m2C
m9:
    return
ProcedureBuilder_exit:
    use Mem:Mem24
";
            #endregion

            RunTest(sExp, m =>
            {
                var r2 = m.Reg32("r2", 2);
                var ctr = m.Reg32("ctr", 12);
                var psw = RegisterStorage.Reg16("psw", 2);
                var SCZO = m.Frame.EnsureFlagGroup(new FlagGroupStorage(psw, 0xF, "SCZO"));

                m.Label("m1");
                m.Assign(r2, m.And(r2, 0x7F));
                m.Assign(SCZO, m.Cond(SCZO.DataType, r2));

                m.Label("m2C");
                m.Assign(r2, m.Shr(r2, 1));
                m.BranchIf(m.Eq0(r2), "m2C");

                m.Label("m3");
                m.BranchIf(m.Test(ConditionCode.EQ, SCZO), "m80");

                m.Label("m4");
                m.Assign(r2, m.Mem32(m.Word32(0x00123400)));
                m.Assign(r2, m.And(r2, 0x7F));
                m.Assign(SCZO, m.Cond(SCZO.DataType, r2));

                m.Label("m80");
                m.Assign(ctr, m.ISubS(ctr, 1));
                m.BranchIf(m.Ne0(ctr), "m2C");
                m.Label("m9");
                m.Return();
            });
        }

        [Test]
        [Ignore("This type of code doesn't seem to be generated anymore")]
        public void CceShrConvert()
        {
            var sExp =
            #region Expected
@"// ProcedureBuilder
// Return size: 0
define ProcedureBuilder
ProcedureBuilder_entry:
	def r2
	// succ:  l1
l1:
	r3_4 = (r2 & 1<32>) != 0<32> ? 1<32> : 0<32>
	return r3_4
	// succ:  ProcedureBuilder_exit
ProcedureBuilder_exit:

";
            #endregion

            RunTest(sExp, m =>
            {
                var r2 = m.Reg32("r2", 2);
                var r3 = m.Reg32("r3", 3);
                var psw = RegisterStorage.Reg32("psw", 4);
                var C = m.Frame.EnsureFlagGroup(m.Architecture.CarryFlag!);

                m.Assign(r2, m.Shr(r2, 1));
                m.Assign(C, m.Cond(C.DataType, r2));
                m.Assign(r3, C);
                m.Return(r3);
            });
        }

        [Test]
        public void CceRegression00939()
        {
            var sExp =
            #region Expected
@"
ProcedureBuilder_entry:
    def eax:word32
foo:
    eax_7 = PHI(eax, eax_8)
    eax_8 = eax_7 | eax_7
    v13 = eax_8 <= 0<32>
    SZ_9 = cond(eax_8)
    SZO_12 = 0<32> | SZ_9
    if (v13) goto foo
l1:
    return
ProcedureBuilder_exit:
";
            #endregion
            RunTest(sExp, m =>
            {
                var eax = m.Reg32("eax", 1);
                var psw = RegisterStorage.Reg32("psw", 4);
                var SZ = m.Frame.EnsureFlagGroup(new FlagGroupStorage(psw, 0x0C, "SZ"));
                var SZO = m.Frame.EnsureFlagGroup(new FlagGroupStorage(psw, 0x0E, "SZO"));
                var O = m.Frame.EnsureFlagGroup(new FlagGroupStorage(psw, 0x02, "O"));
                var C = m.Frame.EnsureFlagGroup(m.Architecture.CarryFlag);
                m.Label("foo");
                m.Assign(eax, m.Or(eax, eax));
                m.Assign(SZ, m.Cond(SZ.DataType, eax));
                m.Assign(O, 0);
                m.Assign(C, 0);
                m.BranchIf(m.Test(ConditionCode.LE, SZO), "foo");
                m.Return();
            });
        }

        [Test(Description = "Treat condition codes as simple bit flags if they're not the result of Cond pseudo-expressions")]
        public void CceSingleBitTest_noCond_true()
        {
            var sExp =
            #region Expected
@"
ProcedureBuilder_entry:
foo:
    eax_8 = 0x123400<32>()
    v10 = eax_8 & 1<32>
    C_12 = v10 != 0<32>
    if (C_12) goto foo
    // succ: l1, foo
l1:
    return
    // succ: ProcedureBuilder_exit
ProcedureBuilder_exit:
";
            #endregion
            RunTest(sExp, m =>
            {
                var eax = m.Reg32("eax", 1);
                var psw = RegisterStorage.Reg32("psw", 4);
                var C = m.Frame.EnsureFlagGroup(m.Architecture.CarryFlag!);
                var tmp = m.Frame.CreateTemporary(PrimitiveType.Bool);
                m.Label("foo");
                m.Assign(eax, m.Fn(m.Word32(0x00123400)));
                m.Assign(C, m.Ne0(m.And(eax, 1)));
                m.Assign(eax, m.Fn(CommonOps.Ror, eax, m.Byte(1)));
                m.BranchIf(m.Test(ConditionCode.ULT, C), "foo");
                m.Return();
            });
        }

        [Test(Description = "Treat condition codes as simple bit flags if they're not the result of Cond pseudo-expressions")]
        public void CceSingleBitTest_noCond_false()
        {
            var sExp =
            #region Expected
@"
ProcedureBuilder_entry:
foo:
    eax_8 = 0x123400<32>()
    v10 = eax_8 & 1<32>
    C_12 = v10 != 0<32>
    if (C_12) goto foo
    // succ: l1, foo
l1:
    return
    // succ: ProcedureBuilder_exit
ProcedureBuilder_exit:
";
            #endregion
            RunTest(sExp, m =>
            {
                var eax = m.Reg32("eax", 1);
                var psw = RegisterStorage.Reg32("psw", 4);
                var C = m.Frame.EnsureFlagGroup(m.Architecture.CarryFlag!);
                var tmp = m.Frame.CreateTemporary(PrimitiveType.Bool);

                m.Label("foo");
                m.Assign(eax, m.Fn(m.Word32(0x00123400)));
                m.Assign(C, m.Ne0(m.And(eax, 1)));
                m.Assign(eax, m.Fn(CommonOps.Ror, eax, m.Byte(1)));
                m.BranchIf(m.Test(ConditionCode.UGE, C), "foo");
                m.Return();
            });
        }

        [Test]
        public void CceRegression_Avr32()
        {
            var sExpected =
            #region Expected
@"
ProcedureBuilder_entry:
    def r9:word32
    def r7:word32
    def Mem14
l1:
    v12 = r9 >=u r7
    r9_13 = CONVERT(v12, bool, word32)
    Mem16[0x123400<32>:word32] = r9_13
    v8 = r9 - r7
    VNZC_9 = cond(v8)
    C_11 = VNZC_9 & 1<32>
    ZSO_20 = VNZC_9 & 0xE<32>
    CZSO_21 = C_11 | ZSO_20
    return
ProcedureBuilder_exit:
    use r9:r9_13
    use CZSO:CZSO_21
    use Mem:Mem_16
";
            #endregion

            RunTest(sExpected, m =>
            {
                var psw = RegisterStorage.Reg32("psw", 42);
                var VNZC = m.Frame.EnsureFlagGroup(new FlagGroupStorage(psw, 0xF, "VNZC"));
                var C = m.Frame.EnsureFlagGroup(new FlagGroupStorage(psw, 1, "C"));
                var r7 = m.Reg32("r7", 7);
                var r9 = m.Reg32("r9", 9);

                m.Assign(VNZC, m.Cond(VNZC.DataType, m.ISub(r9, r7)));
                m.Assign(
                    r9,
                    m.Convert(
                        m.Test(ConditionCode.UGE, C),
                        PrimitiveType.Bool,
                        PrimitiveType.Word32));
                m.MStore(m.Word32(0x123400), r9);
                m.Return();
            });
        }

        [Test]
        [Ignore("")]
        public void CceX86ShlRcl()
        {
            var sExpected =
            #region Expected
     "@@@";
            #endregion

            RunTest(sExpected, m =>
            {
                RegisterStorage reg_ax = new RegisterStorage("ax", 0, 0, PrimitiveType.Word16);
                RegisterStorage reg_cx = new RegisterStorage("cx", 1, 0, PrimitiveType.Word16);
                RegisterStorage reg_dx = new RegisterStorage("dx", 2, 0, PrimitiveType.Word16);
                RegisterStorage reg_bp = new RegisterStorage("bp", 5, 0, PrimitiveType.Word16);
                RegisterStorage reg_si = new RegisterStorage("si", 6, 0, PrimitiveType.Word16);
                RegisterStorage reg_di = new RegisterStorage("di", 7, 0, PrimitiveType.Word16);
                RegisterStorage reg_eflags = new RegisterStorage("eflags", 40, 0, PrimitiveType.Word32);
                FlagGroupStorage grf_C = new FlagGroupStorage(reg_eflags, 0x2, "C");
                FlagGroupStorage grf_CZ = new FlagGroupStorage(reg_eflags, 0x6, "CZ");
                FlagGroupStorage grf_SCZO = new FlagGroupStorage(reg_eflags, 0x17, "SCZO");
                Identifier ax_110 = m.Register(reg_ax);
                Identifier ax_111 = m.Register(reg_ax);
                Identifier cx_93 = m.Register(reg_cx);
                Identifier dx_117 = m.Register(reg_dx);
                Identifier dx_115 = m.Register(reg_dx);
                Identifier bp = m.Register(reg_bp);
                Identifier si_119 = m.Register(reg_si);
                Identifier si_121 = m.Register(reg_si);
                Identifier di_123 = m.Register(reg_di);
                Identifier di_124 = m.Register(reg_di);
                Identifier C = m.Flags("C");
                Identifier CZ = m.Flags("CZ");
                Identifier SCZO = m.Flags("SCZO");
                Identifier sp = m.Reg16("sp", 5);
                Identifier v21_114 = m.Temp(PrimitiveType.Bool, "v21_114");
                Identifier v22_118 = m.Temp(PrimitiveType.Bool, "v22_118");
                Identifier v23_122 = m.Temp(PrimitiveType.Bool, "v23_122");
                Identifier v33_220 = m.Temp(PrimitiveType.Int32, "v33");

                m.Label("fn0800_8BD8_entry");
                m.Assign(ax_110, m.Mem16(m.Word16(0x1234)));
                m.Assign(dx_115, m.Mem16(m.Word16(0x1236)));
                m.Assign(v33_220, m.Shl(m.Seq(dx_115, ax_110), m.Byte(0x1)));
                m.Assign(ax_111, m.Slice(v33_220, PrimitiveType.Word16, 0));
                m.Assign(SCZO, m.Cond(SCZO.DataType, ax_111));
                m.Assign(C, m.And(SCZO, m.Word32(0x2)));
                m.Assign(v21_114, m.And(SCZO, m.Word32(0x2)));
                m.Assign(C, m.Ne(m.And(dx_115, m.Word16(0x8000)), m.Word16(0x0)));
                m.Assign(dx_117, m.Slice(v33_220, PrimitiveType.Word16, 16));
                m.Assign(v22_118, C);
                m.Assign(C, m.Ne(m.And(si_119, m.Word16(0x8000)), m.Word16(0x0)));
                m.Assign(si_121, m.Fn("__rcl", si_119, m.Byte(0x1), C));
                m.Assign(v23_122, C);
                m.Assign(di_124, m.Fn("__rcl", di_123, m.Byte(0x1), C));
                m.Assign(SCZO, m.Cond(SCZO.DataType, m.ISub(di_124, cx_93)));
                m.Assign(C, m.And(SCZO, m.Word32(0x2)));
                m.Assign(CZ, m.And(SCZO, m.Word32(0x6)));
            });
        }
        
        [Test]
        public void CceLongShift()
        {
            var sExpected =
            #region Expected
@"
ProcedureBuilder_entry:
    def Mem6
    def ss:selector
    def bp:word16
l1:
    v10 = bp - 2<i16>
    v11 = ss:v10
    ax_12 = Mem6[v11:word16]
    v14 = bp - 4<i16>
    v15 = ss:v14
    dx_16 = Mem6[v15:word16]
    v26 = bp - 2<i16>
    v27 = ss:v26
    v41 = SEQ(ax_12, dx_16)
    v42 = v41 << 1<16>
    ax_24 = SLICE(v42, word16, 16)
    Mem28[v27:word16] = ax_24
    v30 = bp - 4<i16>
    v31 = ss:v30
    dx_18 = SLICE(v42, word16, 0)
    Mem32[v31:word16] = dx_18
    SCZO_19 = cond(dx_18)
    C_21 = SCZO_19 & 2<32>
    CS_37 = SCZO_19 & 0x15<32>
    CZS_38 = C_21 | CS_37
    return
ProcedureBuilder_exit:
    use ax:ax_24
    use dx:dx_18
    use CZS:CZS_38
    use Mem:Mem_32
";
            #endregion
            RunTest(sExpected, m =>
            {
                RegisterStorage reg_ax = new RegisterStorage("ax", 0, 0, PrimitiveType.Word16);
                RegisterStorage reg_dx = new RegisterStorage("dx", 2, 0, PrimitiveType.Word16);
                RegisterStorage reg_bp = new RegisterStorage("bp", 5, 0, PrimitiveType.Word16);
                RegisterStorage reg_ss = new RegisterStorage("ss", 34, 0, PrimitiveType.SegmentSelector);
                RegisterStorage reg_eflags = new RegisterStorage("eflags", 40, 0, PrimitiveType.Word32);
                FlagGroupStorage grf_SCZO = new FlagGroupStorage(reg_eflags, 0x17, "SCZO");
                FlagGroupStorage grf_C = new FlagGroupStorage(reg_eflags, 0x2, "C");
                Identifier bp_8 = m.Frame.EnsureRegister(reg_bp);
                Identifier ss = m.Frame.EnsureRegister(reg_ss);
                Identifier ax_25 = m.Frame.EnsureRegister(reg_ax);
                Identifier dx_26 = m.Frame.EnsureRegister(reg_dx);
                Identifier dx_27 = m.Frame.EnsureRegister(reg_dx);
                Identifier SCZO_28 = m.Frame.EnsureFlagGroup(grf_SCZO);
                Identifier C_29 = m.Frame.EnsureFlagGroup(grf_C);
                Identifier v15_30 = m.Frame.CreateTemporary("v15", 15, PrimitiveType.Bool);
                Identifier ax_31 = m.Frame.EnsureRegister(reg_ax);

                m.Assign(ax_25, m.SegMem(PrimitiveType.Word16, ss, m.ISub(bp_8, Constant.Create(PrimitiveType.Int16, 0x2))));
                m.Assign(dx_26, m.SegMem(PrimitiveType.Word16, ss, m.ISub(bp_8, Constant.Create(PrimitiveType.Int16, 0x4))));
                m.Assign(dx_27, m.Shl(dx_26, m.Word16(0x1)));
                m.Assign(SCZO_28, m.Cond(SCZO_28.DataType, dx_27));
                m.Assign(C_29, m.And(SCZO_28, m.Word32(0x2)));
                m.Assign(v15_30, C_29);
                m.Assign(ax_31, m.Fn(
                    CommonOps.RolC
                    .MakeInstance(
                        PrimitiveType.Word16,
                        PrimitiveType.Byte),
                    ax_25, 
                    m.Byte(0x1),
                    v15_30));
                m.MStore(m.SegPtr(ss, m.ISub(bp_8, Constant.Create(PrimitiveType.Int16, 0x2))), ax_31);
                m.MStore(m.SegPtr(ss, m.ISub(bp_8, Constant.Create(PrimitiveType.Int16, 0x4))), dx_27);
                m.Return();
            });
        }

        [Test]
        public void CceSubc()
        {
            var sExpected =
            #region Expected    
                @"
ProcedureBuilder_entry:
    def ax:word16
l1:
    v12 = ax <u 4<16>
    v14 = CONVERT(v12, bool, word16)
    bx_15 = 0<16> - v14
    v8 = ax - 4<16>
    CZSO_9 = cond(v8)
    C_11 = CZSO_9 & 1<32>
    ZSO_19 = CZSO_9 & 0xE<32>
    CZSO_20 = C_11 | ZSO_19
    return
ProcedureBuilder_exit:
    use bx:bx_15
    use CZSO:CZSO_20
";
            #endregion

            RunTest(sExpected, m =>
            {
                var eflags = RegisterStorage.Reg32("eflags", 42);
                var Cflag = new FlagGroupStorage(eflags, 0x2, "C");
                var SCZOflags = new FlagGroupStorage(eflags, 0x17, "SCZO");

                var ax = m.Register(RegisterStorage.Reg16("ax", 0));
                var bx = m.Register(RegisterStorage.Reg16("bx", 3));
                var SCZO_1 = m.Flags("SCZO");
                var C_2 = m.Flags("C");
                var bx_3 = m.Register(RegisterStorage.Reg16("bx", 3));

                m.Assign(SCZO_1, m.Cond(SCZO_1.DataType, m.ISub(ax, 4)));
                m.Assign(bx_3, m.ISubC(bx, bx, C_2));
                m.Return();
            });
        }

        [Test]
        public void CceExplicitComparison()
        {
            string sExpected =
                @"
ProcedureBuilder_entry:
    def r1:word32
    def r2:word32
l1:
    v12 = r1 >=u r2
    if (v12) goto skip
m1:
skip:
    return
ProcedureBuilder_exit:
";
            RunTest(sExpected, m =>
            {
                var r1 = m.Reg32("r1", 1);
                var r2 = m.Reg32("r2", 2);
                var C = m.Flags("C");

                m.Assign(C, m.Cond(C.DataType, m.Ult(r1, r2)));
                m.BranchIf(m.Test(ConditionCode.EQ, C), "skip");
                m.Label("m1");
                m.Assign(r1, r2);
                m.Label("skip");
                m.Return();
            });
        }

    }
}
