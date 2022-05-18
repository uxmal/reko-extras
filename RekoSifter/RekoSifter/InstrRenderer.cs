 using Reko.Core.Machine;
using System;
using System.Collections.Generic;
using System.Text;

namespace RekoSifter
{
    public class InstrRenderer
    {
        /// <summary>
        /// Adjust the objdump output for comparison
        /// </summary>
        /// <param name="objdump"></param>
        /// <returns></returns>
        public virtual string AdjustObjdump(string objdump)
        {
            return objdump;
        }

        /// <summary>
        /// Render the Reko machine instruction <paramref name="i"/> the
        /// way objdump does.
        /// </summary>
        public virtual string RenderAsObjdump(MachineInstruction i)
        {
            return i.ToString();
        }

        /// <summary>
        /// Render the Reko machine instruction <paramref name="i"/> the
        /// way LLVM does.
        /// </summary>
        public virtual string RenderAsLlvm(MachineInstruction i)
        {
            return i.ToString();
        }

        public static InstrRenderer Create(string archName)
        {
            switch (archName)
            {
            case "arm-64":
                return new AArch64Renderer();
            case "x86-realmode-16":
            case "x86-protected-16":
            case "x86-protected-32":
            case "x86-protected-64":
                return new X86Renderer();
            case "ppc-be-32":
            case "ppc-le-32":
            case "ppc-be-64":
            case "ppc-le-64":
                return new PowerPcRenderer();
            case "m68k":
                return new M68kRenderer();
            case "mips-be-32":
            case "mips-le-32":
            case "mips-be-64":
            case "mips-le-64":
                return new MipsRenderer();
            case "rl78":
                return new Rl78Renderer();
            case "sparc32":
            case "sparc64":
                return new SparcRenderer();
            default:
                return new InstrRenderer();
            }
        }
    }
}
