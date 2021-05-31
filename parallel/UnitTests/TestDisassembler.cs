using Reko.Core;
using Reko.Core.Machine;
using Reko.Core.Memory;
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
                    instr = new TestMachineInstruction(InstrClass.Padding | InstrClass.Linear, Mnemonic.nop);
                    break;
                case 1: // ALU
                    instr = new TestMachineInstruction(InstrClass.Linear, Mnemonic.alu);
                    break;
                case 2: // a jump
                    if (!rdr.TryReadBeUInt16(out ushort uAddr))
                        instr = new TestMachineInstruction(InstrClass.Invalid, Mnemonic.Invalid);
                    else if ((opcode & 0xF) != 0)
                        instr = new TestMachineInstruction(InstrClass.Transfer | InstrClass.Conditional, Mnemonic.bra);
                    else
                        instr = new TestMachineInstruction(InstrClass.Transfer, Mnemonic.jmp);
                    instr.Operands = new MachineOperand[] { AddressOperand.Ptr32(uAddr) };
                    break;
                case 3: // a call.
                    if (!rdr.TryReadBeUInt16(out uAddr))
                        instr = new TestMachineInstruction(InstrClass.Invalid, Mnemonic.Invalid);
                    else 
                        instr = new TestMachineInstruction(InstrClass.Transfer | InstrClass.Call , Mnemonic.call);
                    instr.Operands = new MachineOperand[] { AddressOperand.Ptr32(uAddr) };
                    break;
                case 4: // a jump with delay slot
                    if (!rdr.TryReadBeUInt16(out uAddr))
                        instr = new TestMachineInstruction(InstrClass.Invalid, Mnemonic.Invalid);
                    else if ((opcode & 0xF) != 0)
                        instr = new TestMachineInstruction(InstrClass.Transfer | InstrClass.Conditional  | InstrClass.Delay, Mnemonic.braD);
                    else
                        instr = new TestMachineInstruction(InstrClass.Transfer, Mnemonic.jmpD);
                    instr.Operands = new MachineOperand[] { AddressOperand.Ptr32(uAddr) };
                    break;
                case 5: // a call with delay slot.
                    if (!rdr.TryReadBeUInt16(out uAddr))
                        instr = new TestMachineInstruction(InstrClass.Invalid, Mnemonic.Invalid);
                    else
                        instr = new TestMachineInstruction(InstrClass.Transfer | InstrClass.Call | InstrClass.Delay, Mnemonic.callD);
                    instr.Operands = new MachineOperand[] { AddressOperand.Ptr32(uAddr) };
                    break;
                case 6: // return
                    instr = new TestMachineInstruction(InstrClass.Transfer | InstrClass.Return, Mnemonic.ret);
                    break;
                default:
                    instr = new TestMachineInstruction(InstrClass.Invalid , Mnemonic.Invalid);
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

    public class TestMachineInstruction : MachineInstruction
    {
        public TestMachineInstruction(InstrClass iclass, Mnemonic mnemonic)
        {
            this.InstructionClass = (Reko.Core.InstrClass) iclass;
            this.Mnemonic = mnemonic;
        }

        public Mnemonic Mnemonic { get; set; }

        public override int MnemonicAsInteger => (int)Mnemonic;
        public override string MnemonicAsString => Mnemonic.ToString();

        protected override void DoRender(MachineInstructionRenderer renderer, MachineInstructionRendererOptions options)
        {
            renderer.WriteMnemonic(MnemonicAsString);
            if (Operands.Length == 0)
                return;
            var sep = " ";
            foreach (var op in Operands)
            {
                renderer.WriteString(sep);
                sep = ",";
                base.RenderOperand(op, renderer, options);
            }
        }
    }

    public enum Mnemonic
    {
        nop,
        alu,
        Invalid,
        bra,
        jmp,
        call,
        braD,
        jmpD,
        ret,
        callD
    }
}