using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelScan
{
    public class MachineInstruction
    {
        private static readonly MachineOperand[] EmptyOperands = Array.Empty<MachineOperand>();

        public MachineInstruction(InstrClass iclass, Mnemonic mnemonic)
        {
            this.InstrClass = iclass;
            this.Mnemonic = mnemonic;
        }

        public InstrClass InstrClass { get; set; }
        public Mnemonic Mnemonic { get; }
        public Address Address { get; set; } = default!;

        public MachineOperand[] Operands { get; set; } = EmptyOperands;
        public int Length { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Mnemonic.ToString());
            if (Operands.Length > 0)
            {
                var sep = " ";
                foreach (var op in Operands)
                {
                    sb.Append(sep);
                    sep = ",";
                    sb.Append(op.ToString());
                }
            }
            return sb.ToString();
        }
    }

    public class MachineOperand
    {
    }

    public class ImmediateOperand : MachineOperand
    {
    }

    public class AddressOperand : MachineOperand
    {
        public Address Address { get; }

        private AddressOperand(Address address)
        {
            this.Address = address;
        }

        public static AddressOperand Ptr16(ushort uaddr)
        {
            return new AddressOperand(Address.Ptr32(uaddr));
        }

        public override string ToString()
        {
            return Address.ToString();
        }
    }
}
