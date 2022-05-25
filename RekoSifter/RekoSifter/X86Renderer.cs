using Reko.Arch.X86;
using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Machine;
using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace RekoSifter
{
    public class X86Renderer : InstrRenderer
    {
        private readonly string[] prefixes = new string[]
        {
            "addr32 ",
            "bnd ",
            "ds ",
            "es ",
            "gs ",
            "notrack ",
            "data16 ",
            "rex.WRXB ",
            "rex.WXB ",
            "rex.WX ",
            "rex.B ",
            "rex.RB ",
            "rex.RX ",
            "rex.RXB ",
            "rex.WB ",
            "rex.WR ",
            "rex.WRB ",
            "rex.WRX ",
            "rex.XB ",
            "rex.W ",
            "rex.R ",
            "rex.X ",
            "rex ",
            "rep ",
            "repz ",
            "repnz "
        };

        public override string AdjustObjdump(string objdump)
        {
            var s = objdump;
            s = Regex.Replace(s, "\\s+", " ");

            var had_prefix = true;
            while (had_prefix)
            {
                had_prefix = false;
                foreach (var p in prefixes)
                {
                    if (s.StartsWith(p))
                    {
                        s = s.Substring(p.Length);
                        had_prefix = true;
                    }
                }
            }
            var m = Regex.Matches(s, ",([0-9]+)");
            if(m.Count > 0)
            {
                var cap = m[0].Groups[1].Captures[0];
                var offs = cap.Index;
                var ival = int.Parse(cap.Value);
                s = s.Substring(0, offs) + "0x" + Convert.ToString(ival, 16);
            }
            return s;
        }

        private void RenderMnemonic(X86Instruction instr, StringBuilder sb)
        {
            bool IsMovabs(X86Instruction instr)
            {
                if (instr.Operands[1] is not MemoryOperand mem)
                    return false;
                return ((mem.Base is null || mem.Base == RegisterStorage.None) &&
                        (mem.Index is null || mem.Index == RegisterStorage.None));
            }

            var mnemStr = instr.Mnemonic switch
            {
                Mnemonic.cmovc => "cmovb",
                Mnemonic.cmovnc => "cmovae",
                Mnemonic.cmovnz => "cmovne",
                Mnemonic.cmovpe => "cmovn",
                Mnemonic.cmovpo => "cmovnp",
                Mnemonic.cmovz => "cmove",
                Mnemonic.jc => "jb",
                Mnemonic.jnc => "jae",
                Mnemonic.jnz => "jne",
                Mnemonic.jpe => "jp",
                Mnemonic.jpo => "jnp",
                Mnemonic.jz => "je",
                Mnemonic.mov => IsMovabs(instr) ? "movabs" : "mov",
                Mnemonic.setc => "setb",
                Mnemonic.setnc => "setae",
                Mnemonic.setnz => "setne",
                Mnemonic.setpe => "setn",
                Mnemonic.setpo => "setnp",
                Mnemonic.setz => "sete",

                _ => instr.Mnemonic.ToString()
            };
            sb.Append(mnemStr);
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
            RenderMnemonic(instr, sb);
            var sep = " ";
            for (int iop = 0; iop < instr.Operands.Length; ++iop)
            {
                var op = instr.Operands[iop];
                sb.Append(sep);
                sep = ",";
                switch (op)
                {
                case RegisterStorage rop:
                    sb.Append(rop.Name);
                    if (iop == 0)
                    {
                        if (instr.OpMask != 0)
                        {
                            RenderOpmask(instr.OpMask, sb);
                        }
                        if (instr.MergingMode != 0)
                        {
                            sb.Append("{z}");
                        }
                    }
                    break;
                case ImmediateOperand imm:
                    RenderObjdumpConstant(instr, imm.Value, instr.dataWidth, false, sb);
                    break;
                case MemoryOperand mem:
                    RenderObjdumpMemoryOperand(instr, mem, sb);
                    break;
                case AddressOperand addr:
                    sb.AppendFormat("0x{0}", addr.Address.ToString().ToLower());
                    break;
                case FpuOperand fpu:
                    if (fpu.StNumber == 0)
                        sb.Append("st");
                    else 
                        sb.AppendFormat("st({0})", fpu.StNumber);
                    break;
                default:
                    sb.AppendFormat("[{0}]", op.GetType().Name);
                    break;
                }
            }
            return sb.ToString();
        }

        private void RenderOpmask(int opMask, StringBuilder sb)
        {
            sb.Append("{k");
            sb.Append(opMask);
            sb.Append('}');
        }

        public override string RenderAsLlvm(MachineInstruction i)
        {
            return i.ToString();
        }

        private void RenderObjdumpConstant(X86Instruction instr, Constant c, DataType dt, bool renderPlusSign, StringBuilder sb)
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
            else if (c.DataType.BitSize == 64 || instr.Mnemonic == Mnemonic.push)
            {
                offset = c.ToInt64();
            }
            else
            {
                offset = (long)c.ToUInt32();
            }

            string fmt = dt.Size switch
            {
                1 => "0x{0:x}",
                2 => "0x{0:x}",
                4 => "0x{0:x}",
                8 => "0x{0:x}",
                _ => "@@@[{0:x}:w{1}]",
            };
            sb.AppendFormat(fmt, offset, c.DataType.BitSize);
        }

        private void RenderObjdumpMemoryOperand(X86Instruction instr, MemoryOperand mem, StringBuilder sb)
        {
            bool hasBase = mem.Base != null && mem.Base != RegisterStorage.None;
            bool hasIndex = mem.Index is not null && mem.Index != RegisterStorage.None;
            if (hasBase || hasIndex)
            {
                if (NeedsMemorySizePrefix(instr.Mnemonic))
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
                if (hasBase)
                {
                    sb.Append(mem.Base!.Name);
                    if (hasIndex)
                    {
                        RenderIndexRegister(mem.Index!, mem.Scale, sb);
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
                        var offset = mem.Offset;
                        if (mem.Base == Registers.rip)
                        {
                            sb.AppendFormat("+0x{0:x}", (ulong) offset.ToInt64());
                        }
                        else
                        {
                            RenderObjdumpConstant(instr, offset, mem.Base.DataType, true, sb);
                        }
                    }
                }
                else if (hasIndex)
                {
                    sb.Append(mem.Index!.Name);
                    if (mem.Scale >= 1)
                    {
                        sb.AppendFormat("*{0}", mem.Scale);
                    }
                    if (mem.Offset != null && mem.Offset.IsValid)
                    {
                        RenderObjdumpConstant(instr, mem.Offset, mem.Index.DataType, true, sb);
                    }
                }
                sb.Append("]");
                if (instr.Broadcast)
                {
                    sb.Append("{1to");
                    sb.Append((uint) (instr.Operands[0].Width.BitSize / mem.Width.BitSize));
                    sb.Append('}');
                }
            }
            else
            {
                sb.Append(mem.SegOverride is not null &&  mem.SegOverride != RegisterStorage.None 
                    ? mem.SegOverride
                    : mem.DefaultSegment);
                sb.AppendFormat(":0x{0:x}", mem.Offset!.ToUInt64());
            }
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
            return !instrs_NoSizePrefix.Contains(mnemonic);
        }

        private static HashSet<Mnemonic> instrs_NoSizePrefix = new HashSet<Mnemonic>
        {
            Mnemonic.fldenv,
            Mnemonic.lea,
            Mnemonic.vlddqu,
            Mnemonic.xrstor,
            Mnemonic.xrstor64,
            Mnemonic.xsave,
            Mnemonic.xsave64,
            Mnemonic.xsaveopt,
            Mnemonic.xsaveopt64,
        };
    }
}
