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
        /// A worker can be in one the following states, with transitions as follows:
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
        private Dictionary<Address, (Address, IProcedureWorker)> callsWaitingForReturn;

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
            if (scheduler.TryRegisterProcedure(arch, addrProc))
            {
                workqueue.Enqueue(MakeWorkItem(arch, addrProc), Priority.Linear);
            }
        }

        public Address ProcedureAddress => addrProc;

        public int State => state;

        /// <summary>
        /// Process the items in the work queue until no more are available.
        /// </summary>
        public void Process()
        {
            Verbose("Entering Process() method on thread {0}", Thread.CurrentThread.ManagedThreadId);
            try
            {
                do
                {
                    ProcessWorkQueue();
                    // Can't find more work to do. Are we waiting for callee procedures
                    // to return?
                    if (this.pendingCalls > 0 && TrySuspending())
                    {
                        // Returning here ends the worker thread, but this ProcedureWorker
                        // instance is still in the scanner task queue.
                        return;
                    }
                } while (!TryFinishing());
                scanner.WorkerFinished(addrProc);
                Verbose("Procedure {0} completely processed", addrProc);
            }
            catch (Exception ex)
            {
                scanner.TaskFailed(addrProc, ex);
            }
        }

        /// <summary>
        /// Processes the items in the work queue.
        /// </summary>
        /// <remarks>
        /// Other threads may add new work items to the queue while the worker status is 
        /// <see cref="StateWorking"/>.
        /// </remarks>
        private void ProcessWorkQueue()
        {
            while (TryDequeueWorkitem(out var item))
            {
                // Ensure that only this thread is processing the block at item.BlockStart.
                if (!scanner.TryRegisterBlockStart(item.BlockStart, addrProc))
                    continue;
                Verbose("  Processing block {0}", item.BlockStart);
                var lastInstr = ParseLinear(item);
                if (lastInstr is null)
                {
                    MarkBadBlock(item.BlockStart);
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
                Debug.Assert(oldState != StateFinishing, "Only this worker is allowed to finish the thread");
            }
        }

        /// <summary>
        /// Attempts to enqueue a work item on this <see cref="ProcedureWorker"/>
        /// unless it is shutting down or has shut down. This method is called
        /// by other <see cref="ProcedureWorker"/>s, running in separate threads.
        /// </summary>
        /// <param name="item">Item to enqueue.</param>
        /// <param name="priority">Item priority.</param>
        /// <returns>True if the item was successfully added to the queue, false
        /// if this <see cref="ProcedureWorker"/> is shutting down or is shut
        /// down.
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
                    // can mutate the state while it is set to StateQueueBusy.
                    this.state = StateWorking;
                    return true;
                }
                if (oldstate == StateFinishing)
                    return false;
                // If we reach this point, some other thread is enqueueing, so spin in the spinlock.
            }
        }

        /// <summary>
        /// Check if it is safe to shut down the thread. We can only finish if
        /// no other thread is trying to add more work.
        /// </summary>
        private bool TryFinishing()
        {
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
                    ProcessReturn(lastInstr);
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
            var addrNext = callInstr.Address + callInstr.Length; //$TODO: delay slot.
            if (IsCalleeReturnStatusKnown(edge, callInstr, addrProc, addrNext))
                return;
            Verbose("  callee return status unknown, trying to start worker for '{0}'", callInstr);

            // We have to wait until a return status is known. Find a worker and register interest 
            //$TODO: self recursive, mutual recursive.
            while (scanner.TryStartProcedureWorker(arch, edge.To, out var calleeWorker))
            {
                if (calleeWorker!.TryEnqueueCaller(this, callInstr.Address, addrNext))
                {
                    // We reach here if we couldn't enqueue work on the calleeWorker because it is terminating.
                    // It might even have determined its return status.
                    if (IsCalleeReturnStatusKnown(edge, callInstr, addrProc, addrNext))
                        return;
                    var pendingCalls = Interlocked.Increment(ref this.pendingCalls);
                    Verbose("  enqueued caller {0}, waiting for '{1}' to complete, now {2}", this.ProcedureAddress, callInstr, pendingCalls);
                    return;
                }
                Interlocked.Decrement(ref this.pendingCalls);
                // We reach here if we couldn't enqueue work on the calleeWorker because it is terminating.
                // It might even have determined its return status.
                if (IsCalleeReturnStatusKnown(edge, callInstr, addrProc, addrNext))
                    return;
            }
        }

        private bool IsCalleeReturnStatusKnown(CfgEdge edge, MachineInstruction callInstr, Address addrProc, Address addrNext)
        {
            var retStatus = scanner.GetProcedureReturnStatus(addrProc);
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
                return true;
            case ProcedureReturn.Diverges:
                // We know for sure that the procedure diverges, so stop working.
                return true;
            }
            return false;
        }

        /// <summary>
        /// The worker has proved that this procedure returns, and can wake up
        /// any other procedure workers that are waiting on it.
        /// </summary>
        /// <param name="instr">The return instruction in question.</param>
        private void ProcessReturn(MachineInstruction instr)
        {
            scanner.SetProcedureReturnStatus(this.addrProc, ProcedureReturn.Returns);
            var emptyCalls = new Dictionary<Address, (Address, IProcedureWorker)>();
            var calls = Interlocked.Exchange(ref this.callsWaitingForReturn, emptyCalls);
            Verbose("  ProcessReturn: found {0} suspended calls", calls.Count);
            foreach (var (addrFallthrough, (addrCaller, caller)) in calls)
            {
                Verbose("    waking caller {0}", caller.ProcedureAddress);
                caller.Wake(addrCaller, addrFallthrough);
            }
        }

        private List<CfgEdge> RegisterBlockEnd(WorkItem item, MachineInstruction instr)
        {
            var edges = new List<CfgEdge>();

            // If we're at a delay slot, make sure we eat the delayed instruction too.
            if (instr.InstructionClass.HasFlag(InstrClass.Delay))
            {
                if (!item.Disassembler.MoveNext() ||
                    !item.Disassembler.Current.InstructionClass.HasFlag(InstrClass.Linear))
                {
                    //$TODO: we don't support transfer instructions in delay slots,
                    // although some architectures do.
                    MarkBadBlock(item.BlockStart);
                    return edges;
                }
                item.Instructions.Add(item.Disassembler.Current);
            }
            var block = scanner.TryRegisterBlock(item.BlockStart, instr.Address - item.BlockStart + instr.Length, item.Instructions.ToArray());
            if (block is null)
                throw new InvalidOperationException("");
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
                        var addrFallthrough = instr.Address + instr.Length;
                        edges.Add(new CfgEdge(EdgeType.DirectJump, item.Architecture, item.BlockStart, addrFallthrough));
                    }
                    edges.Add(new CfgEdge(EdgeType.DirectJump, item.Architecture, item.BlockStart, addrTarget));
                    return edges;
                }
                //$TODO: backwalk.

                if (iclass.HasFlag(InstrClass.Return))
                {
                    scanner.SetProcedureReturnStatus(this.addrProc, ProcedureReturn.Returns);
                    edges.Add(new CfgEdge(EdgeType.Return, this.arch, item.BlockStart, this.addrProc));
                    return edges;
                }
            }
            throw new NotImplementedException($"Unhandled: {iclass}, {instr}");
        }

        private void MarkBadBlock(Address blockStart)
        {
            // Current block is garbage, discard it and any predecessors
            if (this.scanner.TryRegisterBadBlock(blockStart))
            {
                //$TODO; start eating predecessors.
            }
        }

        private static Address? DetermineTargetAddress(MachineInstruction instr)
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
                if (instr.InstructionClass != InstrClass.Linear)
                {
                    Verbose("  stopping ParseLinear at {0} {1}", instr.Address, instr.ToString());
                    return instr;
                }
            }
            Verbose("  *** ParseLinear diverged!");
            return null;
        }


        public bool TryEnqueueCaller(IProcedureWorker procedureWorker, Address addrCall, Address addrFallthrough)
        {
            if (this.state == StateFinishing)
                return false;
            lock (callsWaitingForReturn)
            {
                callsWaitingForReturn.Add(addrFallthrough, (addrCall, procedureWorker));
            }
            return true;
        }

        [Conditional("DEBUG")]
        private void Verbose(string message, params object[] args)
        {
            if (!trace.TraceVerbose)
                return;
            var threadId = Thread.CurrentThread.ManagedThreadId;
            //Debug.Print("{0}({1,2}): {2}", addrProc, threadId, string.Format(message, args));
            Console.WriteLine("{0}({1,2}): {2}", addrProc, threadId, string.Format(message, args));
        }

        /// <summary>
        /// Wakes up a thread if it was suspended.
        /// </summary>
        /// <remarks>
        /// Note that this method is called on a <see cref="ProcedureWorker"/> 
        /// from another procedure worker.</remarks>
        /// <param name="addrFallthrough"></param>
        public void Wake(Address addrCall, Address addrFallthrough)
        {
            // Create the edge from the call to the instruction following 
            // the call.
            var fallthruEdge = new CfgEdge(EdgeType.FallThrough, arch, addrCall, addrFallthrough);
            scanner.RegisterEdge(fallthruEdge);
            var item = MakeWorkItem(this.arch, addrFallthrough);
            Verbose("Wake: Try to wake up {0} at {1}", this.addrProc, addrFallthrough);
            for (;;)
            {
                var oldState = Interlocked.CompareExchange(ref state, StateQueueBusy, StateSuspended);
                if (oldState == StateSuspended)
                {
                    Verbose("Wake:   was suspended, now enqueueing fallthrough {0}", addrFallthrough);
                    this.workqueue.Enqueue(item, Priority.DirectJump);
                    this.state = StateWorking;
                    Interlocked.Decrement(ref this.pendingCalls);
                    scanner.WakeWorker(this);
                    return;
                }
                if (Interlocked.CompareExchange(ref state, StateQueueBusy, StateWorking) == StateWorking)
                {
                    Verbose("Wake:   was already awoken, now enqueueing fallthrough {0}", addrFallthrough);
                    this.workqueue.Enqueue(item, Priority.DirectJump);
                    this.state = StateWorking;
                    Interlocked.Decrement(ref this.pendingCalls);
                    return;
                }
                if (oldState != StateSuspending && oldState != StateQueueBusy)
                    throw new NotImplementedException($"Was in state {oldState}");
            }
            throw new NotImplementedException($"Was in state {this.state}");
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
