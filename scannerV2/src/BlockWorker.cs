﻿using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Rtl;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.ScannerV2
{
    public class BlockWorker
    {
        private static readonly TraceSwitch trace = new TraceSwitch(nameof(BlockWorker), "");

        private readonly RecursiveScanner scanner;
        private readonly IStorageBinder binder;
        private readonly ProcessorState state;

        public BlockWorker(
            RecursiveScanner scanner,
            IStorageBinder binder,
            Address Address,
            IEnumerator<RtlInstructionCluster> Trace,
            ProcessorState State)
        {
            this.scanner = scanner;
            this.binder = binder;
            this.Address = Address;
            this.Trace = Trace;
            this.state = State;
        }

        /// <summary>
        /// Address of the start of the current block being parsed.
        /// </summary>
        public Address Address { get; }

        /// <summary>
        /// Trace of RTL instructions.
        /// </summary>
        public IEnumerator<RtlInstructionCluster> Trace { get; }

        /// <summary>
        /// Performs a linear scan, stopping when a CTI is encountered, or we
        /// run off the edge of the world.
        /// </summary>
        /// <param name="work">Work item to use</param>
        /// <returns>
        /// A pair of a completed <see cref="Block"/> and an updated <see cref="ProcessorState"/>.
        /// If parsing runs off the end of memory, the returned block will be marked
        /// as invalid.
        /// </returns>
        public (Block, ProcessorState) ParseBlock()
        {
            var instrs = new List<(Address, RtlInstruction)>();
            var trace = this.Trace;
            var state = this.state;
            var addrLast = this.Address;
            while (trace.MoveNext())
            {
                var cluster = trace.Current;
                addrLast = cluster.Address;
                bool clusterHadControlInstrs = false;
                foreach (var rtl in cluster.Instructions)
                {
                    trace_Verbose("      {0}: {1}", cluster.Address, rtl);
                    switch (rtl)
                    {
                    case RtlAssignment ass:
                        instrs.Add((cluster.Address, rtl));
                        //$TODO: emulate state;
                        continue;
                    case RtlSideEffect side:
                        instrs.Add((cluster.Address, rtl));
                        //$TODO: emulate side effect.
                        continue;
                    case RtlNop _:
                        instrs.Add((cluster.Address, rtl));
                        continue;
                    case RtlInvalid _:
                        instrs.Add((cluster.Address, new RtlInvalid()));
                        var size = addrLast - this.Address + cluster.Length;
                        return (MakeInvalidBlock(instrs, size), state);
                    case RtlBranch branch:
                        //Expand sub-instruction statements in a later pass.
                        if (branch.NextStatementRequiresLabel)
                        {
                            instrs.Add((cluster.Address, rtl));
                            continue;
                        }
                        break;
                    case RtlGoto g:
                    case RtlCall call:
                    case RtlReturn ret:
                        break;
                    default:
                        throw new NotImplementedException($"{rtl.GetType()} - not implemented");
                    }
                    Debug.Assert(rtl.Class.HasFlag(InstrClass.Transfer));
                    return MakeBlock(instrs, state, rtl);
                }
                if (clusterHadControlInstrs)
                {
                    var addrFallthrough = cluster.Address + cluster.Length;
                    var block = scanner.RegisterBlock(
                        this.state.Architecture,
                        this.Address,
                        addrFallthrough - this.Address,
                        addrFallthrough,
                        instrs);
                    return (block, state);
                }
            }
            // Fell off the end, mark as bad.
            return (MakeInvalidBlock(instrs, addrLast - this.Address), state);
        }

        /// <summary>
        /// After reaching a CTI, make a <see cref="Block"/>.
        /// </summary>
        /// <param name="instrs">The instuctions of the resulting <see cref="Block"/>.</param>
        /// <param name="state">The current <see cref="ProcessorState"/>.</param>
        /// <param name="rtlTransfer">The transfer instruction that triggers the creation
        /// of the block.</param>
        /// A pair of a completed <see cref="Block"/> and an updated <see cref="ProcessorState"/>.
        /// If parsing runs off the end of memory, the block reference will be null.
        /// </returns>
        private (Block, ProcessorState) MakeBlock(
            List<(Address, RtlInstruction)> instrs,
            ProcessorState state,
            RtlInstruction rtlTransfer)
        {
            // We are positioned at the CTI.
            var cluster = this.Trace.Current;

            var size = cluster.Address - this.Address + cluster.Length;

            // Make sure we're not heading to hyperspace.
            if (rtlTransfer is RtlTransfer xfer &&
                xfer.Target is Address addrTarget &&
                !scanner.IsExecutableAddress(addrTarget))
            {
                instrs.Add((cluster.Address, new RtlInvalid()));
                return (MakeInvalidBlock(instrs, size), state);
            }

            if (rtlTransfer.Class.HasFlag(InstrClass.Delay))
            {
                if (!StealDelaySlot(rtlTransfer, instrs))
                    return (MakeInvalidBlock(instrs, size), state);
            }
            else
            {
                instrs.Add((cluster.Address, rtlTransfer));
            }
            cluster = this.Trace.Current;
            var addrFallthrough = cluster.Address + cluster.Length;
            var block = scanner.RegisterBlock(
                this.state.Architecture,
                this.Address,
                size,
                addrFallthrough,
                instrs);
            return (block, state);
        }

        private Block MakeInvalidBlock(
            List<(Address, RtlInstruction)> instrs, 
            long size)
        {
            var block = scanner.RegisterBlock(
                this.state.Architecture,
                this.Address,
                size,
                this.Address + size,
                instrs);
            block.IsInvalid = true;
            return block;
        }

        /// <summary>
        /// The <see cref="Trace"/> is positioned on a CTI with a delay slot.
        /// We swap the positions of the delay slot instruction and the CTI
        /// so that the CTI appears last in the block.
        /// </summary>
        /// <param name="rtlTransfer">The CTI instruction.</param>
        /// <param name="instrs"></param>
        /// <returns>False if another CTI was found in the first CTI delay 
        /// slot. Reko currently doesn't handle this rare idiom, although 
        /// SPARC does allow it.
        /// </returns>
        private bool StealDelaySlot(
            RtlInstruction rtlTransfer,
            List<(Address, RtlInstruction)> instrs)
        {
            Expression MkTmp(Address addr, Expression e)
            {
                var tmp = binder.CreateTemporary(e.DataType);
                instrs.Add((addr, new RtlAssignment(tmp, e)));
                return tmp;
            }

            var addrTransfer = Trace.Current.Address;
            if (!Trace.MoveNext())
            {
                // Fell off the end of memory, CTI is an invalid instruction.
                return false;
            }
            var rtlDelayed = Trace.Current;
            if (rtlDelayed.Class.HasFlag(InstrClass.Transfer))
            {
                // Can't deal with transfer functions in delay slots yet.
                return false;
            }
            // If the delay slot instruction is a nop (padding), we 
            // can ignore it.
            if (!rtlDelayed.Class.HasFlag(InstrClass.Padding))
            {
                // Delay slot instruction does real work, so we will insert it
                // before the CTI instruction.
                Expression? tmp = null;
                switch (rtlTransfer)
                {
                case RtlBranch branch:
                    if (branch.Condition is not Constant)
                    {
                        tmp = MkTmp(addrTransfer, branch.Condition);
                        rtlTransfer = new RtlBranch(tmp, (Address)branch.Target, InstrClass.ConditionalTransfer);
                    }
                    break;
                case RtlGoto g:
                    if (g.Target is not Core.Address)
                    {
                        tmp = MkTmp(addrTransfer, g.Target);
                        rtlTransfer = new RtlGoto(tmp, InstrClass.Transfer);
                    }
                    break;
                case RtlReturn ret:
                    break;
                default:
                    throw new NotImplementedException($"{rtlTransfer.GetType().Name} - not implemented.");
                }
                foreach (var instr in rtlDelayed.Instructions)
                {
                    instrs.Add((rtlDelayed.Address, instr));
                }
            }
            instrs.Add((addrTransfer, rtlTransfer));
            return true;
        }

        [Conditional("DEBUG")]
        private void trace_Inform(string format, params object[] args)
        {
            if (trace.TraceInfo)
                Debug.Print(format, args);
        }

        [Conditional("DEBUG")]
        private void trace_Verbose(string format, params object[] args)
        {
            if (trace.TraceVerbose)
                Debug.Print(format, args);
        }
    }
}
