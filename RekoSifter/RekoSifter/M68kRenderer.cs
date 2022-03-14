using Reko.Arch.M68k;
using Reko.Core;
using Reko.Core.Machine;
using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace RekoSifter
{
    public class M68kRenderer : InstrRenderer
    {
        public override string RenderAsObjdump(MachineInstruction i)
        {
            var instr = (M68kInstruction)i;
            var sb = new StringBuilder();
            sb.Append(instr.Mnemonic.ToString());
            sb.Append(DataSizeSuffix(instr.DataWidth));
            sb.Append("\t");
            string sep = "";
            foreach (var op in instr.Operands)
            {
                sb.Append(sep);
                sep = ",";
                RenderAttOperand(op, sb);
            }
            return sb.ToString();
        }

        private void RenderAttOperand(MachineOperand op, StringBuilder sb)
        {
            // and so on, and so on.....
            switch (op)
            {
                case RegisterStorage reg:
                    sb.Append(RegName(reg));
                    break;
                case M68kImmediateOperand imm:
                    sb.AppendFormat("#{0}", imm.Constant);
                    break;
                case PostIncrementMemoryOperand post:
                    sb.AppendFormat("{0}@+", RegName(post.Register));
                    break;
                case PredecrementMemoryOperand pre:
                    sb.AppendFormat("{0}@-", RegName(pre.Register));
                    break;
                case MemoryOperand mem:
                    sb.Append(RegName(mem.Base));
                    if (mem.Offset != null && mem.Offset.IsValid)
                    {
                        sb.AppendFormat("@({0})", mem.Offset.ToInt32());
                    }
                    else
                    {
                        sb.Append("@");
                    }
                    break;
                case M68kAddressOperand addrOp:
                    sb.AppendFormat("0x{0}", addrOp.Address);
                    break;
                default:
                    sb.AppendFormat("[OPTYPE:{0}]", op.GetType().Name);
                    break;
            }
        }

        private string RegName(RegisterStorage register)
        {
            if (register == Registers.a7)
            {
                return "%sp";
            }
            else if (register == Registers.a6)
            {
                return "%fp";
            }
            else { 
                return $"%{register.Name}";
            }
        }

        private string DataSizeSuffix(PrimitiveType? dataWidth)
        {
            if (dataWidth == null)
                return "";
            if (dataWidth.Domain == Domain.Real)
            {
                switch (dataWidth.BitSize)
                {
                    case 32: return "s";
                    case 64: return "d";
                    case 80: return "x";   //$REVIEW: not quite true?
                    case 96: return "x";
                }
            }
            else
            {
                switch (dataWidth.BitSize)
                {
                    case 8: return "b";
                    case 16: return "w";
                    case 32: return "l";
                    case 64: return "q";
                }
            }
            throw new InvalidOperationException(string.Format("Unsupported data width {0}.", dataWidth.BitSize));
        }
    }
}
