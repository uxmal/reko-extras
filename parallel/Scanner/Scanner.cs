using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

        public bool TryRegisterBlockStart(Address addrStart, Address addrProc)
        {
            return cfg.C.TryAdd(addrStart, addrProc);
        }

        public bool TryRegisterBlockEnd(Address blockStart, Address addrEnd)
        {
            return cfg.BlockEnds.TryAdd(addrEnd, blockStart);
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

        /// <summary>
        /// Register a block starting at address <paramref name="blockStart"/> of known size
        /// <paramref name="blockSize"/>.
        /// </summary>
        /// <param name="blockStart"></param>
        /// <param name="addrEnd"></param>
        /// <returns>True if the block was registered, false if another thread got there first.</returns>
        public bool RegisterBlock(Address blockStart, long blockSize)
        {
            return cfg.B.TryAdd(blockStart, new AddressRange(blockStart, (int)blockSize));
        }

        public void RegisterEdge(CfgEdge edge)
        {
            if (!cfg.E.TryGetValue(edge.From, out var edges))
            {
                edges = new(2); // The majority of blocks have at most 2 out edges.
                cfg.E[edge.From] = edges;
            }
            edges.Add(edge);
        }


        public void SplitBlock(Address xj, IProcessorArchitecture arch, Address y)
        {
            Address xi = cfg.BlockEnds[y];
            if (xi == xj)
            {
                return;
            }
            else if (xj < xi)
            {
                // If xi > xj, Bj is split into [xj, xi) while Bi is untouched. We then register Bj at block end
                // address xi, which will trigger a new iteration of block split when another block has already
                // registered block end at xi.
                if (!TryRegisterBlockEnd(xj, xi))
                    throw new NotImplementedException();//$TODO: what if this happens.
                RegisterBlock(xj, y - xj);
                RegisterEdge(new CfgEdge(EdgeType.DirectJump, arch, xj, xi));
            }
            else
            {
                // If xi < xj, Bi is split into [xi, xj) while Bj is untouched. We then replace Bi with Bj for block
                // end address y, register Bi for block end address xj, and move out-going edges from Bi to Bj.
                // Similar to the first case, registering Bi at xj may recursively require another block split.
                Debug.Assert(xj > xi);
                if (!TryRegisterBlockEnd(xi, xj))
                    throw new NotImplementedException();
                cfg.B[xi] = new AddressRange(xi, xj - xi);
                RegisterBlock(xj, y - xj);
                if (cfg.E.TryGetValue(xi, out var edges))
                {
                    cfg.E.TryRemove(xi, out _);
                    var newEges = edges.Select(e => new CfgEdge(e.Type, e.Architecture, xj, e.To)).ToList();
                    cfg.E.TryAdd(xj, newEges);
                }
                cfg.BlockEnds[y] = xj;
                RegisterEdge(new CfgEdge(EdgeType.DirectJump, arch, xi, xj));
            }
        }
    }
}
