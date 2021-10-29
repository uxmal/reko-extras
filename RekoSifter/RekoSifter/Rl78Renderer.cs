using Reko.Arch.Rl78;
using Reko.Core.Machine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RekoSifter
{
    public class Rl78Renderer : InstrRenderer
    {
        public override string RenderAsObjdump(MachineInstruction i)
        {
            var instr = (Rl78Instruction) i;
            try
            {
                if (instr.Mnemonic == Mnemonic.mov &&
                    instr.Operands.Length == 2 &&
                    instr.Operands[1].ToString().Contains("B6"))
                {
                    instr.ToString();
                }
            } 
            catch { }
            var sb = new StringBuilder();
            sb.Append(instr.MnemonicAsString);
            var sep = "\t";
            foreach (var op in instr.Operands)
            {
                sb.Append(sep);
                sep = ", ";
                switch (op)
                {
                case RegisterOperand reg:
                    sb.Append(reg.Register.Name);
                    break;
                case ImmediateOperand imm:
                    sb.AppendFormat("0x{0:x}", imm.Value.ToUInt32());
                    break;
                case AddressOperand addr:
                    sb.Append("@@@");
                    break;
                case MemoryOperand mem:
                    if (mem.Base is null)
                    {
                        if (mem.Index is null)
                        {
                            sb.AppendFormat("0x{0:x}", (uint) mem.Offset);
                        } else
                        {
                            sb.AppendFormat("{0}[       ]", mem.Offset);
                        }
                    }
                    else
                    {
                        sb.Append("[");
                        sb.Append(mem.Base.Name);
                        if (mem.Offset != 0)
                        {
                            if (mem.Offset > 0)
                            {
                                sb.AppendFormat("+{0}", mem.Offset);
                            }
                            else
                            {
                                sb.AppendFormat("-{0}", -mem.Offset);
                            }
                        }
                        sb.Append("]");
                    }
                    break;
                default:
                    sb.AppendFormat("<{0}>", op.GetType().Name);
                    break;
                }
            }
            return sb.ToString();
        }
    }
}
