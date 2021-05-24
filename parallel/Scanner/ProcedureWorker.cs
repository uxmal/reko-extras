using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;

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
        private PriorityQueue<WorkItem, Priority> workqueue;
        private readonly object queueLock;
        private readonly HashSet<Address> visited;
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
            this.queueLock = new();
            this.visited = new();
            if (scheduler.TryRegisterProcedure(addrProc))
            {
                workqueue.Enqueue(MakeWorkItem(arch, addrProc), Priority.Linear);
            }
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
                while (TryDequeueWorkitem(out var item))
                {
                    Verbose("  Processing {0}", item.BlockStart);
                    if (!scanner.TryRegisterBlockStart(item.BlockStart, addrProc))
                        continue;
                    var lastInstr = ParseLinear(item);
                    if (lastInstr is null)
                    {
                        // Current block is garbage, discard it and any predecessors
                    }
                    else
                    {
                        var edges = RegisterBlockEnd(item, lastInstr);
                        foreach (var edge in edges)
                        {
                            ProcessEdge(edge);
                        }
                    }
                }
                var queue = DestroyQueue();
                scanner.TaskCompleted(addrProc);
                Verbose("Procedure completed.");
            }
            catch (Exception ex)
            {
                scanner.TaskFailed(addrProc, ex);
            }
            finally
            {
            }
        }

        public bool TryDequeueWorkitem([MaybeNullWhen(false)] out WorkItem item)
        {
            lock (queueLock)
            {
                return workqueue.TryDequeue(out item);
            }
        }

        public bool TryEnqueueWorkitem(WorkItem item, Priority priority)
        {
            lock (queueLock)
            {
                var queue = this.workqueue;
                if (queue is null)
                    return false;
                queue.Enqueue(item, priority);
                return true;
            }
        }

        private PriorityQueue<WorkItem, Priority> DestroyQueue()
        {
            lock (queueLock)
            {
                return Interlocked.Exchange(ref workqueue, null!);
            }
        }

        private void ProcessEdge(CfgEdge edge)
        {
            switch (edge.Type)
            {
            case EdgeType.DirectJump:
                scanner.RegisterEdge(edge);
                var wi = MakeWorkItem(edge.Architecture, edge.To);
                TryEnqueueWorkitem(wi, Priority.Linear);
                return;
            }
            throw new NotImplementedException();
        }

        private List<CfgEdge> RegisterBlockEnd(WorkItem item, MachineInstruction instr)
        {
            var block = scanner.TryRegisterBlock(item.BlockStart, instr.Address - item.BlockStart + instr.Length, item.Instructions.ToArray());
            if (block is null)
                throw new InvalidOperationException();
            var edges = new List<CfgEdge>();
            if (!scanner.TryRegisterBlockEnd(item.BlockStart, instr.Address))
            {
                // Another thread has already reached addrEnd. Now we have to reconcile the result.
                Verbose("  Block end already present at {0}", instr.Address);
                scanner.SplitBlock(block, item.Architecture, instr.Address);
                return edges;
            }

            // We're the first thread to reach addrEnd, which means we get to create the out edges.
            if (instr.InstrClass.HasFlag(InstrClass.Transfer))
            {
                var addrTarget = DetermineTargetAddress(instr);
                if (addrTarget is not null)
                {
                    if (instr.InstrClass.HasFlag(InstrClass.Conditional))
                    {
                        var addrFallthrough = instr.Address + instr.Length; //$TODO: delay

                        edges.Add(new CfgEdge(EdgeType.DirectJump, item.Architecture, item.BlockStart, addrFallthrough));
                    }
                    edges.Add(new CfgEdge(EdgeType.DirectJump, item.Architecture, item.BlockStart, addrTarget));
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

        private MachineInstruction? ParseLinear(WorkItem item)
        {
            while (item.Disassembler.MoveNext())
            {
                var instr = item.Disassembler.Current;
                item.Instructions.Add(instr);
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

        public class WorkItem
        {
            public WorkItem(IProcessorArchitecture arch, Address blockStart, IEnumerator<MachineInstruction> dasm)
            {
                this.Architecture = arch;
                this.BlockStart = blockStart;
                this.Disassembler = dasm;
                this.Instructions = new();
            }

            public IProcessorArchitecture Architecture { get; }
            public Address BlockStart { get; }
            public IEnumerator<MachineInstruction> Disassembler { get; }
            public List<MachineInstruction> Instructions { get;  }
        }
    }
}
