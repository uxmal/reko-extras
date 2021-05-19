using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ParallelScan
{
    public class ProcedureWorker
    {
        private static readonly TraceSwitch trace = new TraceSwitch(nameof(ProcedureWorker), "Trace progress of workers")
        {
            Level = TraceLevel.Verbose
        };

        private readonly IProcessorArchitecture arch;
        private readonly Address addrProc;
        private readonly PriorityQueue<WorkItem, Priority> workqueue;
        private readonly Scanner scanner;

        public enum Priority
        {
            DirectJump,
            IndirectJump,
            Call,
            Linear,
        }

        public ProcedureWorker(IProcessorArchitecture arch, Address addrProc, Scanner scheduler)
        {
            this.arch = arch;
            this.addrProc = addrProc;
            this.scanner = scheduler;
            this.workqueue = new();
            workqueue.Enqueue(MakeWorkItem(arch, addrProc), Priority.Linear);
            scheduler.TryRegisterProcedure(addrProc);
            scheduler.TryRegisterBlockStart(addrProc);
        }

        private WorkItem MakeWorkItem(IProcessorArchitecture arch, Address addrProc)
        {
            var rdr = scanner.CreateReader(arch, addrProc);
            var dasm = arch.CreateDisassembler(rdr).GetEnumerator();
            return new WorkItem(arch, addrProc, dasm);
        }

        public void Process()
        {
            Verbose("Entering Processing() method");
            try
            {
                while (workqueue.TryDequeue(out var item))
                {
                    scanner.TryRegisterBlockStart(item.BlockStart);
                    var lastInstr = ParseLinear(item);
                    if (lastInstr is null)
                    {
                        // Current block is garbage, discard it and any predecessors
                    }
                    else
                    {
                        var edges = RegisterBlockEnd(item, lastInstr);
                        if (edges.Count == 0)
                        {
                            // Someone got there before us.
                            SplitBlock(item);
                        }
                        foreach (var edge in edges)
                        {
                            ProcessEdge(edge);
                        }
                    }
                }
                scanner.TaskCompleted(addrProc);
                Verbose("Procedure completed.");
            }

            catch (Exception ex)
            {
                scanner.TaskFailed(addrProc, ex);
            }
        }

        private void ProcessEdge(CfgEdge edge)
        {
            switch (edge.Type)
            {
            case EdgeType.DirectJump:
                scanner.RegisterEdge(edge);
                var wi = MakeWorkItem(edge.Architecture, edge.To);
                this.workqueue.Enqueue(wi, Priority.Linear);
                return;
            }
            throw new NotImplementedException();
        }

        private List<CfgEdge> RegisterBlockEnd(WorkItem item, MachineInstruction instr)
        {
            var edges = new List<CfgEdge>();
            if (!scanner.TryRegisterBlockEnd(instr.Address))
                return edges;

            // We know the full extent of a block
            scanner.RegisterBlock(item.BlockStart, instr.Address, instr.Length);
            var addrTarget = DetermineTargetAddress(instr);
            if (instr.InstrClass.HasFlag(InstrClass.Transfer))
            {
                if (addrTarget is not null)
                {
                    // Simple jump.
                    edges.Add(new CfgEdge(EdgeType.DirectJump, item.Architecture, instr.Address, addrTarget));
                    return edges;
                }
                if (instr.InstrClass.HasFlag(InstrClass.Return))
                {
                    //$TODO: mark current procedure as returning.
                    return edges;
                }
            }
            throw new NotImplementedException();
        }

        private Address? DetermineTargetAddress(MachineInstruction instr)
        {
            if (instr.Operands.Length > 0)
            {
                return (instr.Operands[^1] as AddressOperand)?.Address;
            }
            else
                return null;
        }

        private void SplitBlock(WorkItem item)
        {
        }

        private MachineInstruction? ParseLinear(WorkItem item)
        {
            while (item.Disassembler.MoveNext())
            {
                var instr = item.Disassembler.Current;
                if (instr.InstrClass != InstrClass.Linear)
                {
                    Verbose("  stopping ParseLinear at {0} {1}", instr.Address, instr.ToString());
                    return instr;
                }
            }
            return null;
        }

        [Conditional("DEBUG")]
        private void Verbose(string message, params object[] args)
        {
            if (!trace.TraceVerbose)
                return;
            Debug.Print("{0}: {1}", addrProc, string.Format(message, args));
        }

        class WorkItem
        {
            public WorkItem(IProcessorArchitecture arch, Address blockStart, IEnumerator<MachineInstruction> dasm)
            {
                this.Architecture = arch;
                this.BlockStart = blockStart;
                this.Disassembler = dasm;
            }

            public IProcessorArchitecture Architecture { get; }
            public Address BlockStart { get; }
            public IEnumerator<MachineInstruction> Disassembler { get; }
        }
    }
}
