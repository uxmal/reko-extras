using Reko.Core.Machine;
using System;
using System.Collections.Generic;
using System.Text;

namespace RekoSifter
{
    public class PowerPcRenderer : InstrRenderer
    {
        public override string RenderAsObjdump(MachineInstruction i)
        {
            return i.ToString();
        }

        public override string RenderAsLlvm(MachineInstruction i)
        {
            return i.ToString();
        }
    }
}
