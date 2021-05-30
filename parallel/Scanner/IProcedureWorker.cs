using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelScan
{
    public interface IProcedureWorker
    {
        /// <summary>
        /// The entry address of the procedure this instance is working on.
        /// </summary>
        Address ProcedureAddress { get; }

        /// <summary>
        /// Processes the queue of work items until it is exhausted.
        /// </summary>
        void Process();

        /// <summary>
        /// Attempt to enqueue a work item on this worker.
        /// </summary>
        /// <returns>True if the worker accepted the work, false if the worker is quitting.</returns>
        public bool TryEnqueueWorkitem(ProcedureWorker.WorkItem item, ProcedureWorker.Priority priority);

        /// <summary>
        /// Fire and forget notification that procedure called from address <paramref name="addrCallee"/>
        /// has returned.
        /// </summary>
        /// <param name="callInstr">The <see cref="MachineInstruction"/> that was the source of the call.</param>
        /// <param name="addrCallee">The address of the procedure that was called.</param>
        public void NotifyProcedureReturns(MachineInstruction callInstr, Address addrCallee);

        /// <summary>
        /// Tells the <see cref="IProcedureWorker"/> that another worker is wanting to know when a procedure
        /// has determined its return status.
        /// </summary>
        /// <param name="procedureWorker">The worker that should be notified when the procedure status is known.</param>
        /// <param name="addrFallthrough">The address to which the call instruction would fall through.</param>
        /// <returns>True if the call was accepted, false if not.</returns>
        bool TryEnqueueCaller(IProcedureWorker procedureWorker, Address addrFallthrough);
        
        /// <summary>
        /// Move this suspended worker to the working state.
        /// </summary>
        /// <param name="instrCaller"></param>
        /// <param name="addrProc"></param>
        void Wake(Address addrFallThrough);
    }
}
