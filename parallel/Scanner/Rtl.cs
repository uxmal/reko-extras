using Reko.Core.Expressions;
using Reko.Core;
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
    /*
    [Flags]
    public enum InstrClass
    {
        None = 0,
        Linear = 1,
        Transfer = 2,
        Conditional = 4,
        ConditionalTransfer = 6,
        Call = 8,
        Delay = 16,
        Annul = 32,
        Terminates = 64,
        System = 128,
        Padding = 256,
        Invalid = 512,
        Zero = 1024,
        Return = 2048,
    }*/

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