using Reko.Arch.Arm.AArch64;
using Reko.Core;
using Reko.Core.Machine;
using System.Text;

namespace RekoSifter
{
    public class AArch64Renderer : InstrRenderer
    {
        public override string RenderAsObjdump(MachineInstruction i)
        {
            var instr = (AArch64Instruction)i;
            var sb = new StringBuilder();
            if (instr.Mnemonic == Mnemonic.b && instr.Operands[0] is ConditionOperand cop)
            {
                sb.Append($"b.{cop.Condition.ToString().ToLower()}");
            }
            else
            {
                sb.Append(instr.Mnemonic.ToString());
            }
            var sep = "\t";
            foreach (var op in instr.Operands)
            {
                sb.Append(sep);
                sep = ", ";
                switch (op)
                {
                case RegisterStorage reg:
                    sb.Append(reg.Name);
                    break;
                case ImmediateOperand imm:
                    sb.AppendFormat("0x{0:x}", imm.Value.ToUInt64());
                    break;
                case AddressOperand addr:
                    sb.AppendFormat("0x{0:x}", addr.Address.ToLinear());
                    break;
                case MemoryOperand mem:
                    sb.Append('[');
                    sb.Append(mem.Base!.Name);
                    if (mem.Offset != null && !mem.Offset.IsZero)
                    {
                        sb.AppendFormat(", #{0}", mem.Offset.ToInt32());
                    }
                    sb.Append(']');
                    if (mem.PreIndex)
                    {
                        sb.Append('!');
                    }
                    break;
                default:
                    sb.Append(op);
                    break;
                }
            }
            return sb.ToString();
        }
    }
}