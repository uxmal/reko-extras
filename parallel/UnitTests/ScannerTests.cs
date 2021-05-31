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
                foreach (var instr in de.Value.Instructions)
                {
                    sb.AppendFormat("    {0}", instr);
                    sb.AppendLine();
                }
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
        public async Task Scanner_jump()
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
        public async Task Scanner_branch()
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
    alu
    alu
    bra 00000006
    // succ: DirectJump: 00000000 -> 00000005 DirectJump: 00000000 -> 00000006
l00000005:
    // size: 1
    alu
    // succ: DirectJump: 00000005 -> 00000006
l00000006:
    // size: 2
    alu
    ret
";
            #endregion

            AssertCfg(sExp, cfg);
        }

        [Test]
        public async Task Scanner_loop()
        {
            var addr = Address.Ptr32(0);
            var m = new Assembler(addr);
            m.Mov();                    // 00
            m.Mov();                    // 01
            m.Jmp("while_head");        // 02

            m.Label("loop_body");
            m.Mov();                    // 05

            m.Label("while_head");
            m.Mov();                    // 06
            m.Branch(3, "loop_body");   // 07

            m.Ret();                    // 0A

            Cfg cfg = await ScanProgramAsync(addr, m);

            var sExp =
            #region Expected
@"proc fn00000000
l00000000:
    // size: 5
    alu
    alu
    jmp 00000006
    // succ: DirectJump: 00000000 -> 00000006
l00000005:
    // size: 1
    alu
    // succ: DirectJump: 00000005 -> 00000006
l00000006:
    // size: 4
    alu
    bra 00000005
    // succ: DirectJump: 00000006 -> 0000000A DirectJump: 00000006 -> 00000005
l0000000A:
    // size: 1
    ret
";
            #endregion
            AssertCfg(sExp, cfg);
        }

        [Test]
        public async Task Scanner_call()
        {
            var addr = Address.Ptr32(0);
            var m = new Assembler(addr);
            m.Call("subroutine");
            m.Ret();

            m.Label("subroutine");
            m.Mov();
            m.Ret();

            Cfg cfg = await ScanProgramAsync(addr, m);

            var sExp =
            #region Expected
@"proc fn00000000
l00000000:
    // size: 3
    call 00000004
    // succ: Call: 00000000 -> 00000004
l00000003:
    // size: 1
    ret
proc fn00000004
l00000004:
    // size: 2
    alu
    ret
";
            #endregion
            AssertCfg(sExp, cfg);
        }

        [Test]
        public async Task Scanner_nested_call()
        {
            var addr = Address.Ptr32(0);
            var m = new Assembler(addr);
            m.Call("subroutine1");
            m.Ret();

            m.Label("subroutine2");
            m.Ret();

            m.Label("subroutine1");
            m.Call("subroutine2");
            m.Ret();

            Cfg cfg = await ScanProgramAsync(addr, m);

            var sExp =
            #region Expected
@"proc fn00000000
l00000000:
    // size: 3
    call 00000005
    // succ: Call: 00000000 -> 00000005
l00000003:
    // size: 1
    ret
proc fn00000004
l00000004:
    // size: 1
    ret
proc fn00000005
l00000005:
    // size: 3
    call 00000004
    // succ: Call: 00000005 -> 00000004
l00000008:
    // size: 1
    ret
";
            #endregion
            AssertCfg(sExp, cfg);
        }

        [Test]
        public async Task Scanner_parallel_calls()
        {
            var addr = Address.Ptr32(0);
            var m = new Assembler(addr);
            m.Branch(3, "fork1");
            m.Call("subroutine1");
            m.Jmp("join1");

            m.Label("fork1");
            m.Call("subroutine2");

            m.Label("join1");
            m.Ret();

            m.Label("subroutine2");
            m.Ret();

            m.Label("subroutine1");
            m.Ret();

            Cfg cfg = await ScanProgramAsync(addr, m);

            var sExp =
            #region Expected
@"proc fn00000000
l00000000:
    // size: 3
    bra 00000009
    // succ: DirectJump: 00000000 -> 00000003 DirectJump: 00000000 -> 00000009
l00000003:
    // size: 3
    call 0000000E
    // succ: Call: 00000003 -> 0000000E
l00000006:
    // size: 3
    jmp 0000000C
    // succ: DirectJump: 00000006 -> 0000000C
l00000009:
    // size: 3
    call 0000000D
    // succ: Call: 00000009 -> 0000000D
l0000000C:
    // size: 1
    ret
proc fn0000000D
l0000000D:
    // size: 1
    ret
proc fn0000000E
l0000000E:
    // size: 1
    ret
";
#endregion
            AssertCfg(sExp, cfg);
        }
    }
}