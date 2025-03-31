using Reko.Core;
using Reko.Core.Machine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.blocksoup;

public readonly struct MachineInstructionEx : IAddressable
{
    public MachineInstructionEx(MachineInstruction instr)
    {
        Instruction = instr;
    }

    public readonly MachineInstruction Instruction { get; }
    public readonly Address Address => Instruction.Address;
    public readonly override string ToString()
    {
        return $"{Address:X8}: {Instruction}";
    }
}
