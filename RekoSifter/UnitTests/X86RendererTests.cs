using NUnit.Framework;
using NUnit.Framework.Legacy;
using Reko.Arch.X86;
using Reko.Core;
using Reko.Core.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RekoSifter.UnitTests
{
    [TestFixture]
    public class X86RendererTests
    {
        private readonly X86ArchitectureFlat64 arch;

        public X86RendererTests()
        {
            this.arch = new X86ArchitectureFlat64(null!, "x86-protected-64", new());
        }

        private void AssertObjdump64(string sExp, string hexString)
        {
            var bytes = BytePattern.FromHexBytes(hexString);
            var mem = new ByteMemoryArea(Address.Ptr64(0), bytes);
            var dasm = arch.CreateDisassemblerImpl(mem.CreateLeReader(0));
            var renderer = new X86Renderer();
            var sObjdump = renderer.RenderAsObjdump(dasm.First());
            ClassicAssert.AreEqual(sExp, sObjdump);
        }

        [Test]
        public void X86R_O_weirdSIB()
        {
            AssertObjdump64("add BYTE PTR [rax*1+0x5080000],al", "00 04 05 00 00 08 05");
        }

        [Test(Description = "LEA doesn't need a WORD PTR prefix")]
        public void X86R_O_Lea()
        {
            AssertObjdump64("lea esi,[rsi+riz*1+0x0]", "8D B4 26 00 00 00 00");
        }

        [Test]
        public void X86R_o_jmp_48_ptr()
        {
            AssertObjdump64("jmp FWORD PTR [rdi*4+0x0]", "FF 2C BD 00 00 00 00");
        }

        [Test]
        public void X86R_o_push_imm()
        {
            AssertObjdump64("push 0xffffffffffffff90", "6A 90");
        }

        [Test]
        public void X86R_o_jc()
        {
            AssertObjdump64("jb 0xffffffffffffff82", "72 80");
        }

        [Test]
        public void X86R_o_vsqrtpd()
        {
            AssertObjdump64("vsqrtpd ymm6{k7},YMMWORD PTR [rcx]", "62 F1 FD 2F 51 31");
        }

        [Test]
        public void X86R_O_short_immdiate()
        {
            AssertObjdump64("in al,0xcd", "E4 CD");
        }

        [Test]
        public void X86R_O_EVEX_mergingmode_z()
        {
            AssertObjdump64("vmovaps ymm6{k7}{z},ymm5", "62 F1 7C AF 28 F5");
        }

        [Test]
        public void X86R_O_long_rip_displacement()
        {
            AssertObjdump64("and dl,BYTE PTR [rip+0xfffffffff0b4cf00]", "22 15 00 CF B4 F0");
        }

        [Test]
        public void X86R_O_movabs()
        {
            AssertObjdump64("movabs ds:0x2001a378c88f00c7,eax", "40 A3 C7 00 8F C8 78 A3 01 20");
        }
    }
}
