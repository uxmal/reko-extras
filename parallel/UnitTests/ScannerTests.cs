using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelScan.UnitTests
{
    [TestFixture]
    public class ScannerTests
    {
        private static async Task<Cfg> ScanProgramAsync(Address addr, Assembler m)
        {
            var s = new Scanner(m.Complete());
            var arch = new TestArchitecture();
            var sym = new ImageSymbol(arch, addr);
            var cfg = await s.ScanAsync(new[] { sym });
            return cfg;
        }

        [Test]
        public async Task Scanner_Entry()
        {
            var addr = Address.Ptr32(0);
            var m = new Assembler(addr);
            m.Ret();

            Cfg cfg = await ScanProgramAsync(addr, m);
            Assert.AreEqual(1, cfg.F.Count);
        }

        [Test]
        public async Task Scanner_JumpEntry()
        {
            var addr = Address.Ptr32(0);
            var m = new Assembler(addr);
            m.Mov();
            m.Mov();
            m.Jmp(5);
            m.Mov();
            m.Ret();

            Cfg cfg = await ScanProgramAsync(addr, m);
            Assert.AreEqual(1, cfg.F.Count);
            Assert.AreEqual(2, cfg.B.Count); 
        }

    }
}
