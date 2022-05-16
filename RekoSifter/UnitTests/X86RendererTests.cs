using NUnit.Framework;
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
            Assert.AreEqual(sExp, sObjdump);
        }

        [Test]
        public void X86R_O_weirdSIB()
        {
            AssertObjdump64("add    BYTE PTR [rax*1+0x5080000],al", "00 04 05 00 00 08 05");
        }
    }
}
