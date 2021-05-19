using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelScan
{
    public class Scanner
    {
        private MemoryArea mem;
        private Cfg cfg;
        private TaskCompletionSource<Cfg> promise;
        private ConcurrentDictionary<Address, ProcedureWorker> workers;

        public Scanner(MemoryArea mem)
        {
            this.mem = mem;
            this.cfg = new();
            this.promise = new();
            this.workers = new();
        }

        public Task<Cfg> ScanAsync(IEnumerable<ImageSymbol> symbols)
        {
            int nWorkers = 0;
            foreach (var sym in symbols.Distinct())
            {
                ++nWorkers;
                var worker = new ProcedureWorker(sym.Architecture, sym.Address, this);
                Task.Run(() => worker.Process());
            }
            if (nWorkers == 0)
                return Task.FromResult(cfg);
            else
                return promise.Task;
        }

        public bool TryRegisterBlockStart(Address addrStart)
        {
            return cfg.C.TryAdd(addrStart, addrStart);
        }

        public bool TryRegisterBlockEnd(Address addrEnd)
        {
            return cfg.BlockEnds.TryAdd(addrEnd, addrEnd);
        }

        public bool TryRegisterProcedure(Address addrProc)
        {
            return cfg.F.TryAdd(addrProc, addrProc);
        }

        public ImageReader CreateReader(IProcessorArchitecture arch, Address addr)
        {
            return arch.CreateImageReader(mem, addr);
        }

        public void TaskFailed(Address workerAddress, Exception ex)
        {
            workers.TryRemove(workerAddress, out _);
            promise.TrySetException(ex);
        }

        public void TaskCompleted(Address workerAddress)
        {
            workers.TryRemove(workerAddress, out _);
            if (workers.Count == 0)
            {
                promise.TrySetResult(cfg);
            }
        }

        public bool RegisterBlock(Address blockStart, Address addrLastInstr, int lengthLastInstr)
        {
            var blockSize = addrLastInstr - blockStart + lengthLastInstr;
            return cfg.B.TryAdd(blockStart, new AddressRange(blockStart, blockSize));
        }

        public void RegisterEdge(CfgEdge edge)
        {
            cfg.E.TryAdd(edge, edge);
        }
    }
}
