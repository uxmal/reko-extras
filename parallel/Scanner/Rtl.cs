using System;
using System.Collections.Generic;

namespace ParallelScan
{
    class RtlCluster
    {
        public RtlCluster(Address addr, int length, InstrClass iclass, params RtlInstruction[] instrs)
        {
            this.Address = addr;
            this.Length = length;
            this.InstrClass = iclass;
            this.Instructions = instrs;
        }

        public Address Address { get; }
        public int Length { get; }
        public InstrClass InstrClass { get; }
        public RtlInstruction[] Instructions { get; }
    }

    [Flags]
    public enum InstrClass
    {
        Invalid,
        Linear,
        Transfer,

        Delay =         0x08,
        Padding =       0x10,
        Conditional =   0x20,
        Call = 0x40,
        Return = 0x80,
    }

    public abstract class RtlInstruction
    {
    }

    public class RtlGoto : RtlInstruction
    {
        public Expression Target { get; set; }

        public RtlGoto(Expression target)
        {
            this.Target = target;
        }
    }

    public class RtlAssignment : RtlInstruction { }


}