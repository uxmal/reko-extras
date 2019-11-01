using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Reko.Arch.X86;
using Reko.Core;
using Reko.Core.Machine;

namespace RekoSifter
{
    public class Sifter
    {
        private string[] args;
        private MemoryArea mem;
        private IProcessorArchitecture arch;
        private EndianImageReader rdr;
        private IEnumerable<MachineInstruction> dasm;

        public Sifter(string[] args)
        {
            this.args = args;
            this.mem = new MemoryArea(Address.Ptr32(0x00100000), new byte[100]);
            this.arch = new X86ArchitectureFlat32("x86");
            this.rdr = arch.CreateImageReader(mem, 0);
            this.dasm = arch.CreateDisassembler(rdr);
        }

        public void Sift()
        {
            var stack = mem.Bytes;
            int iLastByte = 0;
            int lastLen = 0;
            while (iLastByte >= 0)
            {
                var instr = Dasm();
                RenderLine(instr);
                if (instr.Length != lastLen)
                {
                    // Length changed, moved marker.
                    iLastByte = instr.Length - 1;
                    lastLen = instr.Length;
                }
                var val = stack[iLastByte] + 1;
                while (val >= 0x100)
                {
                    stack[iLastByte] = 0;
                    --iLastByte;
                    if (iLastByte < 0)
                        return;
                    val = stack[iLastByte] + 1;
                }
                stack[iLastByte] = (byte)val;
            }
        }

        private MachineInstruction Dasm()
        {
            rdr.Offset = 0;
            try
            {
                var instr = dasm.First();
                return instr;
            }
            catch
            {
                //$TODO: emit some kind of unit test.
                return null;
            }
        }

        private void RenderLine(MachineInstruction instr)
        {
            var sb = new StringBuilder();
            var sInstr = instr != null
                ? instr.ToString()
                : "*** ERROR ***";
            sb.AppendFormat("{0,-40}", sInstr);
            var bytes = mem.Bytes;
            for (int i = 0; i < instr.Length; ++i)
            {
                sb.AppendFormat(" {0:X2}", (uint)bytes[i]);
            }
            Console.WriteLine(sb.ToString());
        }
    }
}