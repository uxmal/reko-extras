using Reko.Arch.X86;
using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Machine;
using System;
using System.Collections.Generic;
using System.Text;

namespace RekoSifter
{
    public class X86Renderer : InstrRenderer
    {
        private void RenderMnemonic(Mnemonic m, StringBuilder sb)
        {
            var mnemStr = m switch
            {
                Mnemonic.jz => "je",
                Mnemonic.jc => "jb",
                Mnemonic.jnc => "jae",
                Mnemonic.jnz => "jne",
                Mnemonic.jpe => "jp",
                Mnemonic.jpo => "jnp",
                _ => m.ToString()
            };
            sb.AppendFormat("{0,-6}", mnemStr);
        }

        /// <summary>
        /// Render a Reko <see cref="MachineInstruction"/> so that it looks like 
        /// the output of objdump.
        /// </summary>
        /// <param name="i">Reko machine instruction to render</param>
        /// <returns>A string containing the rendering of the instruction.</returns>
        public override string RenderAsObjdump(MachineInstruction i)
        {
            var sb = new StringBuilder();
            var instr = (X86Instruction)i;
            RenderMnemonic(instr.Mnemonic, sb);
            var sep = " ";
            foreach (var op in instr.Operands)
            {
                sb.Append(sep);
                sep = ",";
                switch (op)
                {
                    case RegisterStorage rop:
                        sb.Append(rop.Name);
                        break;
                    case ImmediateOperand imm:
                        RenderObjdumpConstant(imm.Value, false, sb);
                        break;
                    case MemoryOperand mem:
                        RenderObjdumpMemoryOperand(instr.Mnemonic, mem, sb);
                        break;
                    case AddressOperand addr:
                        sb.AppendFormat("0x{0}", addr.Address.ToString().ToLower());
                        break;
                    case FpuOperand fpu:
                        sb.AppendFormat("st({0})", fpu.StNumber);
                        break;
                    default:
                        sb.AppendFormat("[{0}]", op.GetType().Name);
                        break;
                }
            }
            return sb.ToString();
        }

        public override string RenderAsLlvm(MachineInstruction i)
        {
            return i.ToString();
        }

        private void RenderObjdumpConstant(Constant c, bool renderPlusSign, StringBuilder sb)
        {
            long offset;
            if (renderPlusSign)
            {
                offset = c.ToInt32();
                if (offset < 0)
                {
                    sb.Append("-");
                    offset = -c.ToInt64();
                }
                else
                {
                    sb.Append("+");
                    offset = c.ToInt64();
                }
            }
            else if (c.DataType.BitSize == 64)
            {
                offset = c.ToInt64();
            }
            else
            {
                offset = (long)c.ToUInt32();
            }

            string fmt = c.DataType.Size switch
            {
                1 => "0x{0:x}",
                2 => "0x{0:x}",
                4 => "0x{0:x}",
                8 => "0x{0:x}",
                _ => "@@@[{0:x}:w{1}]",
            };
            sb.AppendFormat(fmt, offset, c.DataType.BitSize);
        }

        private void RenderObjdumpMemoryOperand(Mnemonic mnemonic, MemoryOperand mem, StringBuilder sb)
        {
            if (NeedsMemorySizePrefix(mnemonic))
            {
                switch (mem.Width.Size)
                {
                case 1: sb.Append("BYTE PTR "); break;
                case 2: sb.Append("WORD PTR "); break;
                case 4: sb.Append("DWORD PTR "); break;
                case 6: sb.Append("FWORD PTR "); break;
                case 8: sb.Append("QWORD PTR "); break;
                case 10: sb.Append("TBYTE PTR "); break;
                case 16: sb.Append("XMMWORD PTR "); break;
                case 32: sb.Append("YMMWORD PTR "); break;
                case 64: sb.Append("ZMMWORD PTR "); break;
                default: sb.AppendFormat("[SIZE {0} PTR] ", mem.Width.Size); break;
                }
            }
            sb.AppendFormat("{0}[", mem.SegOverride != null && mem.SegOverride != RegisterStorage.None
                ? $"{mem.SegOverride}:"
                : "");
            if (mem.Base != null && mem.Base != RegisterStorage.None)
            {
                sb.Append(mem.Base.Name);
                if ((mem.Index != null && mem.Index != RegisterStorage.None))
                {
                    RenderIndexRegister(mem.Index, mem.Scale, sb);
                } 
                else if (mem.Scale != 0)
                {
                    //$BUG: 32-bit?
                    RenderIndexRegister(
                        mem.Base.Width.BitSize == 64 ? Registers.riz : Registers.eiz,
                        mem.Scale, 
                        sb);
                }
                if (mem.Offset != null && mem.Offset.IsValid)
                {
                    RenderObjdumpConstant(mem.Offset, true, sb);
                }
            }
            else if (mem.Index is not null && mem.Index != RegisterStorage.None)
            {
                sb.Append(mem.Index.Name);
                if (mem.Scale >= 1)
                {
                    sb.AppendFormat("*{0}", mem.Scale);
                }
                if (mem.Offset != null && mem.Offset.IsValid)
                {
                    RenderObjdumpConstant(mem.Offset, true, sb);
                }
            }
            else
            {
                sb.Append(mem.Offset);
            }
            sb.Append("]");
        }

        private static void RenderIndexRegister(RegisterStorage indexReg, int scale, StringBuilder sb)
        {
            sb.Append("+");
            sb.Append(indexReg.Name);
            if (scale >= 1)
            {
                sb.AppendFormat("*{0}", scale);
            }
        }

        private bool NeedsMemorySizePrefix(Mnemonic mnemonic)
        {
            return mnemonic != Mnemonic.lea;
        }
    }
}
