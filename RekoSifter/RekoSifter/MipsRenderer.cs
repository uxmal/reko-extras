using Reko.Arch.Mips;
using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Machine;
using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Text;

namespace RekoSifter
{
    public class MipsRenderer : InstrRenderer
    {
        public override string RenderAsObjdump(MachineInstruction i)
        {
            var instr = (MipsInstruction)i;
            var sb = new StringBuilder();
            sb.Append(instr.Mnemonic);
            var rawRegisterNames = coprocInstrs.Contains(instr.Mnemonic);
            sb.Append("\t");
            var sep = "";
            foreach (var op in instr.Operands)
            {
                sb.Append(sep);
                sep = ",";
                switch (op)
                {
                    case RegisterOperand reg:
                        RenderRegister(reg.Register, rawRegisterNames, sb);
                        break;
                    case ImmediateOperand imm:
                        RenderImmediate(imm.Value, sb);
                        break;
                    case IndirectOperand indirect:
                        sb.Append(indirect.Offset);
                        sb.Append('(');
                        RenderRegister(indirect.Base, false, sb);
                        sb.Append(')');
                        break;
                    case AddressOperand addressOperand:
                        long addr = (int)addressOperand.Address.ToLinear();
                        sb.Append($"0x{addr:x16}");
                        break;
                    default:
                        sb.AppendFormat("[{0}]", op.GetType().Name);
                        break;
                }
            }
            return sb.ToString();
        }

        private static readonly HashSet<Mnemonic> coprocInstrs = new HashSet<Mnemonic>
        {
            Mnemonic.ldc1,
            Mnemonic.ldc2,
            Mnemonic.lwc1,
            Mnemonic.lwc2,
            Mnemonic.sdc1,
            Mnemonic.sdc2,
            Mnemonic.swc2,
        };

        private static readonly Dictionary<string, string> abiNames = new Dictionary<string, string>()
        {
            { "r0", "zero" },
            { "r1", "at" },
            { "r2", "v0" },
            { "r3", "v1" },
            { "r4", "a0" },
            { "r5", "a1" },
            { "r6", "a2" },
            { "r7", "a3" },
            { "r8", "t0" },
            { "r9", "t1" },
            { "r10", "t2" },
            { "r11", "t3" },
            { "r12", "t4" },
            { "r13", "t5" },
            { "r14", "t6" },
            { "r15", "t7" },
            { "r16", "s0" },
            { "r17", "s1" },
            { "r18", "s2" },
            { "r19", "s3" },
            { "r20", "s4" },
            { "r21", "s5" },
            { "r22", "s6" },
            { "r23", "s7" },
            { "r24", "t8" },
            { "r25", "t9" },
            { "r26", "k0" },
            { "r27", "k1" },
            { "r28", "gp" },
            { "r29", "k2" },
            { "r30", "s8" },
        };

        private void RenderRegister(RegisterStorage register, bool rawRegisterNames, StringBuilder sb)
        {
            string abiName;
            if (rawRegisterNames)
            {
                if (register.Name[0] == 'f')
                    abiName = $"${register.Name}";
                else 
                    abiName = $"${register.Number}";
            }
            else
            {
                if (!abiNames.TryGetValue(register.Name, out abiName!))
                    abiName = register.Name;
            }
            sb.Append(abiName);
        }

        private void RenderImmediate(Constant value, StringBuilder sb)
        {
            if (value.DataType is PrimitiveType pt && pt.Domain == Domain.SignedInt)
            {
                sb.Append(value.ToInt32());
            }
            else
            {
                sb.Append($"0x{value.ToUInt32():x}");
            }
        }
    }
}
