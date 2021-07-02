using Reko.Core;
using Reko.Core.Machine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;

namespace ParallelScan
{
    /// <summary>
    /// This worker class performs flow-control based scan of a procedure.
    /// </summary>
    public class ProcedureWorker : IProcedureWorker
    {
        private static readonly TraceSwitch trace = new (nameof(ProcedureWorker), "Trace progress of workers")
        {
            Level = TraceLevel.Verbose
        };

        /// <summary>
        /// A worker can be in the following states, with transitions as follows:
        /// StateWorking
        ///     StateSuspending if the worker queue is empty, but there are pending call returns.
        ///     StateFinishing  if the worker queue is empty, and there are no pending call returns.
        ///     StateQueueBusy  if a work item is being added or removed from the queue.
        /// StateFinishing
        ///                     This is a terminal state, the procedureworker will soon be deleted.
        /// StateQueueBusy
        ///     StateWorking    An item was added to the work queue.
        ///     
        /// </summary>
        private const int StateFinishing = -1;      // This instance is closing down and releasing all resources.
        private const int StateWorking = 0;         // This instance is working, other threads are free to enqueue work.
        private const int StateQueueBusy = 1;       // Some other thread is enqueueing work.
        private const int StateSuspending = 2;      // Instance about to be suspended.
        private const int StateSuspended = 3;       // Instance is waiting for a called worker to return.

        private int state;
        private int pendingCalls;
        private readonly IProcessorArchitecture arch;
        private readonly Address addrProc;
        private readonly Scanner scanner;
        private readonly PriorityQueue<WorkItem, Priority> workqueue;
        private Dictionary<Address, IProcedureWorker> callsWaitingForReturn;

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
            this.callsWaitingForReturn = new();
            this.state = StateWorking;
            if (scheduler.TryRegisterProcedure(addrProc))
            {
                workqueue.Enqueue(MakeWorkItem(arch, addrProc), Priority.Linear);
            }
        }

        public Address ProcedureAddress => addrProc;

        public int State => state;

        public void Process()
        {
            Verbose("Entering Processing() method on thread {0}", Thread.CurrentThread.ManagedThreadId);
            try
            {
                do
                {
                    ProcessWorkQueue();
                    // Can't find more work to do. Are we waiting for callee procedures
                    // to return?
                    if (this.pendingCalls > 0 && TrySuspending())
                    {
                        // This ends the worker thread, but this instance is still in the scanner
                        // task queue.
                        return;
                    }
                } while (!TryFinishing());
                scanner.WorkerFinished(addrProc);
                Verbose("Procedure completely processed");
            }
            catch (Exception ex)
            {
                scanner.TaskFailed(addrProc, ex);
            }
        }

        private void ProcessWorkQueue()
        {
            while (TryDequeueWorkitem(out var item))
            {
                Verbose("  Processing {0}", item.BlockStart);
                // Ensure that only this thread is processing the block at item.BlockStart.
                if (!scanner.TryRegisterBlockStart(item.BlockStart, addrProc))
                    continue;
                var lastInstr = ParseLinear(item);
                if (lastInstr is null)
                {
                    // Current block is garbage, discard it and any predecessors
                    if (this.scanner.TryRegisterBadBlock(item.BlockStart))
                    {
                        //$TODO; start eating predecessors.
                    }
                }
                else
                {
                    var edges = RegisterBlockEnd(item, lastInstr);
                    ProcessEdges(edges, lastInstr);
                }
            }
        }

        private WorkItem MakeWorkItem(IProcessorArchitecture arch, Address addr)
        {
            var rdr = scanner.CreateReader(arch, addr);
            var dasm = arch.CreateDisassembler(rdr).GetEnumerator();
            return new WorkItem(arch, addr, dasm);
        }

        /// <summary>
        /// Try to get a work item from the work item queue.
        /// </summary>
        /// <param name="item">The returned work item.</param>
        /// <returns>True if a work item was found in the queue, false if not.
        /// </returns>
        private bool TryDequeueWorkitem([MaybeNullWhen(false)] out WorkItem item)
        {
            // We can dequeue items only if no-one else is busy on the lock.
            for (; ; )
            {
                var oldState = Interlocked.CompareExchange(ref this.state, StateQueueBusy, StateWorking);
                if (oldState == StateWorking)
                {
                    bool result = workqueue.TryDequeue(out item);
                    this.state = StateWorking;
                    return result;
                }
                Debug.Assert(oldState != StateFinishing);
            }
        }

        /// <summary>
        /// This method is called by other <see cref="ProcedureWorker"/>s, running in separate threads.
        /// </summary>
        /// <param name="item">Item to enqueue.</param>
        /// <param name="priority">Item priority.</param>
        /// <returns>True if the item was successfully added to the queue, false if this <see cref="ProcedureWorker"/>
        /// is dying.
        /// </returns>
        public bool TryEnqueueWorkitem(WorkItem item, Priority priority)
        {
            for (; ; )
            {
                var oldstate = Interlocked.CompareExchange(ref this.state, StateQueueBusy, StateWorking);
                if (oldstate == StateWorking)
                {
                    // Noone else is spinning on this.state, so enqueue the work item.
                    var queue = this.workqueue;
                    queue.Enqueue(item, priority);
                    // The following is safe, because this instance set it to StateEnqueueing and noone else
                    // can mutate the state while it is set to StateEnqueueing.
                    this.state = StateWorking;
                    return true;
                }
                if (oldstate == StateFinishing)
                    return false;
                // If we reach this point, some other thread is enqueueing, so spin in the spinlock.
            }
        }

        /// <summary>
        /// This instance calls this method to see if it is safe to shut down the thread.
        /// </summary>
        private bool TryFinishing()
        {
            // We can only finish if no other thread is trying to add more work.
            return Interlocked.CompareExchange(ref this.state, StateFinishing, StateWorking) == StateWorking;
        }

        /// <summary>
        /// Attempt to go to sleep if the instance is waiting for another worker.
        /// </summary>
        private bool TrySuspending()
        {
            Verbose("Suspending (because there are {0} pending calls)...", pendingCalls);
            // We can sleep if no other thread is trying to add more work.
            if (Interlocked.CompareExchange(ref this.state, StateSuspending, StateWorking) != StateWorking)
                return false;
            scanner.SuspendWorker(this);
            this.state = StateSuspended;
            Verbose("...Suspended, waiting for pending calls to return ");
            return true;
        }

        private void ProcessEdges(List<CfgEdge> edges, MachineInstruction lastInstr)
        {
            foreach (var edge in edges)
            {
                switch (edge.Type)
                {
                case EdgeType.DirectJump:
                    scanner.RegisterEdge(edge);
                    var wi = MakeWorkItem(edge.Architecture, edge.To);
                    TryEnqueueWorkitem(wi, Priority.Linear);
                    break;
                case EdgeType.Call:
                    ProcessCall(edge, lastInstr);
                    break;
                case EdgeType.Return:
                    ProcessReturn(edge, lastInstr);
                    break;
                default:
                   throw new NotImplementedException();
                }
            }
        }

        private void ProcessCall(CfgEdge edge, MachineInstruction callInstr)
        {
            scanner.RegisterEdge(edge);
            var addrProc = edge.To;
            var retStatus = scanner.ProcedureReturnStatus(addrProc);
            var addrNext = callInstr.Address + callInstr.Length; //$TODO: delay slot.
            switch (retStatus)
            {
            case ProcedureReturn.Returns:
                // We know the called procedure returns, so we can proceed with
                // the instruction after the called, at address `addrNext`.
                var fallthruEdge = new CfgEdge(EdgeType.FallThrough, arch, edge.From, addrNext);
                scanner.RegisterEdge(fallthruEdge);
                Verbose("  falling through after call (at {0}) to {1}", callInstr.Address, addrNext);
                var wi = MakeWorkItem(this.arch, addrNext);
                TryEnqueueWorkitem(wi, Priority.Linear);    // This cannot fail.
                return;
            case ProcedureReturn.Diverges:
                return;
            }
            Verbose("  enqueueing at call to {0}", edge.To);

            // We have to wait until a return status is known. Find a worker and register interest 
            //$TODO: self recursive, mutual recursive.
            while (scanner.TryStartProcedureWorker(arch, edge.To, out var calleeWorker))
            {
                Interlocked.Increment(ref this.pendingCalls);
                if (calleeWorker!.TryEnqueueCaller(this, addrNext))
                {
                    Verbose("  enqueued caller {0}, waiting for {1} to complete", this.ProcedureAddress, callInstr);
                    return;
                }
                // We reach here if we couldn't enqueue work on the calleeWorker because it is terminating.
                Interlocked.Decrement(ref this.pendingCalls);
            }
        }

        private void ProcessReturn(CfgEdge edge, MachineInstruction instr)
        {
            scanner.SetProcedureStatus(this.addrProc, ProcedureReturn.Returns);
            var emptyCalls = new Dictionary<Address, IProcedureWorker>();
            var calls = Interlocked.Exchange(ref this.callsWaitingForReturn, emptyCalls);
            Verbose("  Return found {0} suspended calls", calls.Count);
            foreach (var (addrFallthrough, caller) in calls)
            {
                Verbose("    waking caller {0}", caller.ProcedureAddress);
                caller.Wake(addrFallthrough);
            }
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
            var iclass = (InstrClass)instr.InstructionClass;
            if (iclass.HasFlag(InstrClass.Transfer))
            {
                var addrTarget = DetermineTargetAddress(instr);
                if (addrTarget is not null)
                {
                    if (iclass.HasFlag(InstrClass.Call))
                    {
                        edges.Add(new CfgEdge(EdgeType.Call, item.Architecture, item.BlockStart, addrTarget));
                        return edges;
                    }
                    if (iclass.HasFlag(InstrClass.Conditional))
                    {
                        var addrFallthrough = instr.Address + instr.Length; //$TODO: delay
                        edges.Add(new CfgEdge(EdgeType.DirectJump, item.Architecture, item.BlockStart, addrFallthrough));
                    }
                    edges.Add(new CfgEdge(EdgeType.DirectJump, item.Architecture, item.BlockStart, addrTarget));
                    return edges;
                }
                if (iclass.HasFlag(InstrClass.Return))
                {
                    scanner.SetProcedureStatus(this.addrProc, ProcedureReturn.Returns);
                    edges.Add(new CfgEdge(EdgeType.Return, this.arch, item.BlockStart, this.addrProc));
                    return edges;
                }
            }
            throw new NotImplementedException($"Unhandled: {iclass}, {instr}");
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

        /// <summary>
        /// Process a sequence of linear instructions, i.e. those that do not 
        /// affect control flow.
        /// </summary>
        /// <param name="item">Current work item.</param>
        /// <returns>A CFI that terminates the sequence of linear instructions,
        /// or null if the disassembler ran out of instructions (this typically 
        /// indicates the current sequence is diverging and invalid.</returns>
        private MachineInstruction? ParseLinear(WorkItem item)
        {
            Verbose("  starting ParseLinear at {0}", item.BlockStart);
            while (item.Disassembler.MoveNext())
            {
                var instr = item.Disassembler.Current;
                if (!instr.IsValid)
                {
                    Verbose("  encountered an invalid instruction at {0}", instr.Address);
                    return null;
                }
                item.Instructions.Add(instr);
                if ((InstrClass) instr.InstructionClass != InstrClass.Linear)
                {
                    Verbose("  stopping ParseLinear at {0} {1}", instr.Address, instr.ToString());
                    return instr;
                }
            }
            Verbose("  *** ParseLinear diverged!");
            return null;
        }


        public bool TryEnqueueCaller(IProcedureWorker procedureWorker, Address addrFallthrough)
        {
            lock (callsWaitingForReturn)
            {
                callsWaitingForReturn.Add(addrFallthrough, procedureWorker);
            }
            return true;
        }

        [Conditional("DEBUG")]
        private void Verbose(string message, params object[] args)
        {
            if (!trace.TraceVerbose)
                return;
            var threadId = Thread.CurrentThread.ManagedThreadId;
            Debug.Print("{0}({1,2}): {2}", addrProc, threadId, string.Format(message, args));
        }

        public void Wake(Address addrFallthrough)
        {
            Verbose("Woken up at {0}", addrFallthrough);
            for (; ; )
            {
                var oldState = Interlocked.Exchange(ref state, StateWorking);
                if (oldState == StateSuspended)
                    break;
            }
            Interlocked.Decrement(ref this.pendingCalls);
            var item = MakeWorkItem(this.arch, addrFallthrough);
            TryEnqueueWorkitem(item, Priority.DirectJump);
            scanner.WakeWorker(this);
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
