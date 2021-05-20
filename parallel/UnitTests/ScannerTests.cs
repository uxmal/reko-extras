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

        private void AssertCfg(string sExp, Cfg cfg)
        {
            var sb = new StringBuilder();
            foreach (var de in cfg.B.OrderBy(b => b.Key))
            {
                if (cfg.F.ContainsKey(de.Key))
                {
                    sb.AppendFormat("proc fn{0}", de.Key);
                    sb.AppendLine();
                }
                sb.AppendFormat("l{0}:", de.Key);
                sb.AppendLine();
                sb.AppendFormat("    // size: {0}", de.Value.Size);
                sb.AppendLine();
                if (cfg.E.TryGetValue(de.Key, out var edges))
                {
                    if (edges.Count <= 2)
                    {
                        sb.AppendFormat("    // succ: {0}", string.Join(" ", edges));
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.AppendLine("    // succ:");
                        foreach (var e in edges)
                        {
                            sb.AppendFormat("    //   {0}", e);
                            sb.AppendLine();
                        };
                    }
                }
            }
            var sActual = sb.ToString();
            if (sExp != sActual)
            {
                Console.WriteLine(sActual);
                Assert.AreEqual(sExp, sActual);
            }
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
        public async Task Scanner_Jump()
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

        [Test]
        public async Task Scanner_Branch()
        {
            var addr = Address.Ptr32(0);
            var m = new Assembler(addr);
            m.Mov();
            m.Mov();
            m.Branch(3, "done");

            m.Mov();

            m.Label("done");
            m.Mov();
            m.Ret();

            Cfg cfg = await ScanProgramAsync(addr, m);

            var sExp =
            #region Expected
@"proc fn00000000
l00000000:
    // size: 5
    // succ: DirectJump: 00000000 -> 00000005 DirectJump: 00000000 -> 00000006
l00000005:
    // size: 1
    // succ: DirectJump: 00000005 -> 00000006
l00000006:
    // size: 2
";
            #endregion

            AssertCfg(sExp, cfg);
        }
    }
}
