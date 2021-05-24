using System;
using System.Collections;
using System.Collections.Generic;

namespace ParallelScan.UnitTests
{
    public class TestDisassembler : IEnumerable<MachineInstruction>
    {
        private readonly ImageReader rdr;

        public TestDisassembler(ImageReader rdr)
        {
            this.rdr = rdr;
        }

        public IEnumerator<MachineInstruction> GetEnumerator()
        {
            Address addr = rdr.Address;
            while (rdr.TryReadByte(out byte opcode))
            {
                MachineInstruction instr;
                switch ((opcode >> 4) & 0xF)
                {
                case 0: // nop
                    instr = new MachineInstruction(InstrClass.Padding | InstrClass.Linear, Mnemonic.nop);
                    break;
                case 1: // ALU
                    instr = new MachineInstruction(InstrClass.Linear, Mnemonic.alu);
                    break;
                case 2: // a jump
                    if (!rdr.TryReadBeUInt16(out ushort uAddr))
                        instr = new MachineInstruction(InstrClass.Invalid, Mnemonic.Invalid);
                    else if ((opcode & 0xF) != 0)
                        instr = new MachineInstruction(InstrClass.Transfer | InstrClass.Conditional, Mnemonic.bra);
                    else
                        instr = new MachineInstruction(InstrClass.Transfer, Mnemonic.jmp);
                    instr.Operands = new MachineOperand[] { AddressOperand.Ptr16(uAddr) };
                    break;
                case 3: // a call.
                    if (!rdr.TryReadBeUInt16(out uAddr))
                        instr = new MachineInstruction(InstrClass.Invalid, Mnemonic.Invalid);
                    else 
                        instr = new MachineInstruction(InstrClass.Transfer | InstrClass.Call , Mnemonic.call);
                    instr.Operands = new MachineOperand[] { AddressOperand.Ptr16(uAddr) };
                    break;
                case 4: // a jump with delay slot
                    if (!rdr.TryReadBeUInt16(out uAddr))
                        instr = new MachineInstruction(InstrClass.Invalid, Mnemonic.Invalid);
                    else if ((opcode & 0xF) != 0)
                        instr = new MachineInstruction(InstrClass.Transfer | InstrClass.Conditional  | InstrClass.Delay, Mnemonic.braD);
                    else
                        instr = new MachineInstruction(InstrClass.Transfer, Mnemonic.jmpD);
                    instr.Operands = new MachineOperand[] { AddressOperand.Ptr16(uAddr) };
                    break;
                case 5: // a call with delay slot.
                    if (!rdr.TryReadBeUInt16(out uAddr))
                        instr = new MachineInstruction(InstrClass.Invalid, Mnemonic.Invalid);
                    else
                        instr = new MachineInstruction(InstrClass.Transfer | InstrClass.Call | InstrClass.Delay, Mnemonic.callD);
                    instr.Operands = new MachineOperand[] { AddressOperand.Ptr16(uAddr) };
                    break;
                case 6: // return
                    instr = new MachineInstruction(InstrClass.Transfer | InstrClass.Return, Mnemonic.ret);
                    break;
                default:
                    instr = new MachineInstruction(InstrClass.Invalid , Mnemonic.Invalid);
                    break;
                }
                var addrNew = rdr.Address;
                instr.Address = addr;
                instr.Length = (int)(addrNew - addr);
                yield return instr;
                addr = addrNew;
            } 
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}