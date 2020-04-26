using Reko.Core.Machine;
using System;
using System.Collections.Generic;
using System.Text;

namespace RekoSifter
{
    public class InstrRenderer
    {
        public virtual string RenderAsObjdump(MachineInstruction i)
        {
            return i.ToString();
        }

        public virtual string RenderAsLlvm(MachineInstruction i)
        {
            return i.ToString();
        }

        public static InstrRenderer Create(string archName)
        {
            switch (archName)
            {
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
                default:
                    return new InstrRenderer();
            }
        }
    }
}
