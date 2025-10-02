using Reko.Arch.SuperH;
using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Machine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RekoSifter
{
    public class SuperHRenderer : InstrRenderer
    {
        private static readonly Dictionary<Mnemonic, string> mnemonics = new()
        {
            { Mnemonic.cmp_eq, "cmp/eq" },
            { Mnemonic.cmp_ge, "cmp/ge" },
            { Mnemonic.cmp_hi, "cmp/hi" },
            { Mnemonic.cmp_hs, "cmp/hs" },
            { Mnemonic.cmp_str, "cmp/str" },
            { Mnemonic.fcmp_eq, "fcmp/gt" },
            { Mnemonic.fcmp_gt, "fcmp/gt" },
            { Mnemonic.fmov_s, "fmov/s" },
        };

        public override string RenderAsObjdump(MachineInstruction i)
        {
            var instr = (SuperHInstruction) i;
            var opt = new MachineInstructionRendererOptions(
                flags: MachineInstructionRendererFlags.ResolvePcRelativeAddress);
            var str = new Reko.Core.Machine.StringRenderer();
            RenderMnemonic(instr, str);
            var sep = "\t";
            foreach (var op in instr.Operands)
            {
                str.WriteString(sep);
                sep = ",";
                RenderOperand(instr, op, opt, str);
            }
            return str.ToString();
        }

        private static void RenderMnemonic(SuperHInstruction i, StringRenderer str)
        {
            if (!mnemonics.TryGetValue(i.Mnemonic, out string? sMnemonic))
                sMnemonic = i.MnemonicAsString.Replace("_", ".");
            str.WriteMnemonic(sMnemonic);
        }

        private void RenderOperand(MachineInstruction i, MachineOperand op, MachineInstructionRendererOptions opt,  StringRenderer str)
        {
            switch (op)
            {
            case Constant imm:
                if (imm.DataType.BitSize <= 16)
                {
                    str.WriteFormat("#{0}", imm.ToInt32());
                    return;
                }
                break;
            case Address addr:
                str.WriteFormat("0x{0:x16}", (long)(int)addr.Offset);
                return;
            case MemoryOperand mem:
                if (mem.mode == Reko.Arch.SuperH.AddressingMode.PcRelativeDisplacement)
                {
                    long displacement = (int) i.Address.Offset + (int) mem.disp + 4;
                    str.WriteFormat("0x{0:x16}", displacement);
                    return;
                }
                if (mem.mode == AddressingMode.IndirectDisplacement)
                {
                    mem.reg = Registers.r0;
                }
                break;
            }
            op.Render(str, opt);
        }
    }
}
