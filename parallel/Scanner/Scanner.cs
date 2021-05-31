using Reko.Core;
using Reko.Core.Machine;
using Reko.Core.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelScan
{
    public class Scanner
    {
        private readonly MemoryArea mem;
        private readonly Cfg cfg;
        private readonly TaskCompletionSource<Cfg> promise;
        /// <summary>
        /// Maintains an <see cref="IProcedureWorker"/> for each procedure address. Workers
        /// </summary>
        private readonly ConcurrentDictionary<Address, IProcedureWorker> workers;
        private readonly ConcurrentDictionary<Address, IProcedureWorker> suspendedWorkers;

        public Scanner(MemoryArea mem)
        {
            this.mem = mem;
            this.cfg = new();
            this.promise = new();
            this.workers = new();
            this.suspendedWorkers = new();
        }

        /// <summary>
        /// Asynchronously scans the given image. 
        /// </summary>
        /// <param name="symbols">Initial starting points from which to conduct the scan.</param>
        /// <returns>An interprocedural Control Flow Graph.</returns>
        public Task<Cfg> ScanAsync(IEnumerable<ImageSymbol> symbols)
        {
            int nWorkers = 0;
            foreach (var sym in symbols.Distinct())
            {
                ++nWorkers;
                TryStartProcedureWorker(sym.Architecture, sym.Address, out _);
            }
            if (nWorkers == 0)
                return Task.FromResult(cfg);
            else
                return promise.Task;
        }

        public bool TryStartProcedureWorker(IProcessorArchitecture arch, Address addr, out IProcedureWorker? iworker)
        {
            if (suspendedWorkers.TryGetValue(addr, out iworker))
                return true;
            var worker = new ProcedureWorker(arch, addr, this);
            while (!workers.TryAdd(addr, worker))
            {
                // There is already a worker?
                if (workers.TryGetValue(addr, out iworker))
                    return true;
            }
            Task.Run(() => worker.Process());
            iworker = worker;
            return true;
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
            if (!cfg.F.TryAdd(addrProc, addrProc))
                return false;
            cfg.Procedures.TryAdd(addrProc, new Procedure(addrProc));
            return true;
        }

        public EndianImageReader CreateReader(IProcessorArchitecture arch, Address addr)
        {
            return arch.CreateImageReader(mem, addr);
        }

        public void TaskFailed(Address workerAddress, Exception ex)
        {
            workers.TryRemove(workerAddress, out _);
            promise.TrySetException(ex);
        }

        public void WorkerFinished(Address workerAddress)
        {
            workers.TryRemove(workerAddress, out _);
            if (workers.IsEmpty)
            {
                promise.TrySetResult(cfg);
            }
        }

        /// <summary>
        /// Register a block starting at address <paramref name="blockStart"/> of known size.
        /// <paramref name="blockSize"/>.
        /// </summary>
        /// <param name="blockStart"></param>
        /// <param name="addrEnd"></param>
        /// <returns>A new block if none was registered before, or null if another thread got there first.</returns>
        public Block? TryRegisterBlock(Address blockStart, long blockSize, MachineInstruction[] instrs)
        {
            var block = new Block(blockStart, (int)blockSize, instrs);
            return cfg.B.TryAdd(blockStart, block) ? block : null;
        }

        public Block? TryRegisterBlock(Block newBlock)
        {
            return cfg.B.TryAdd(newBlock.Address, newBlock) ? newBlock : null;
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

        /// <summary>
        /// Splits the existing block <paramref name="block" /> at the final instruction 
        /// whose address is <paramref name="addrEnd" />.
        /// </summary>
        /// <param name="block"></param>
        /// <param name="architecture"></param>
        /// <param name="addrEnd"></param>
        public void SplitBlock(Block block, IProcessorArchitecture architecture, Address addrEnd)
        {
            var wl = new Queue<(Block, IProcessorArchitecture, Address)>();
            wl.Enqueue((block, architecture, addrEnd));
            while (wl.TryDequeue(out var item))
            {
                var (Bj, arch, y) = item;
                // We traced xj until we reached a CTI at address y. Some other block, starting at
                // xi, ends at 'y'. This is the block that has the edges out from 'y'.
                Address xi = cfg.BlockEnds[y];
                Address xj = Bj.Address;
                if (xi == xj)
                {
                    return;
                }
                var Bi = cfg.B[xi];

                // Find the address at which the instructions from both blocks start having the same addresses.
                var (i, j) = FindCommonInstructions(Bi.Instructions, Bj.Instructions);
                if (i != 0 && j != 0)
                {
                    // The shared instructions do not consume the whole of a block:
                    //  xi              y
                    //  +-+--+--+-+-----+
                    //      +--+--+-----+
                    //      xj      S
                    //
                    // We want a new block at 'S' resulting in a shared end block:
                    //  xi          S    y
                    //  +-+--+--+-+ +----+
                    //      +--+--+
                    //      xj      
                    var sAddress = Bi.Instructions[i].Address;  //$TODO: different procs, result in tail calls.
                    if (TryRegisterBlockStart(sAddress, cfg.C[xi]))
                    {
                        // Noone else is working on a block at sAddress.
                        var sSize = Bi.Size - (sAddress - Bi.Instructions[0].Address);
                        var sInstrs = Bi.Instructions.Skip(i).ToArray();
                        var sBlock = TryRegisterBlock(sAddress, sSize, sInstrs);
                        Debug.Assert(sBlock != null);
                    }
                    // Block 'i' already has edges, 'j' does not. We need to steal those edges and give them to sBlock.
                    StealEdges(xi, sAddress);
                    RegisterEdge(new CfgEdge(EdgeType.DirectJump, arch, xi, sAddress));
                    RegisterEdge(new CfgEdge(EdgeType.DirectJump, arch, xj, sAddress));
                    var newI = Chop(Bi, 0, i);
                    var newJ = Chop(Bj, 0, j);
                    if (!TryRegisterBlockEnd(newI.Address, sAddress))
                    {
                        wl.Enqueue((newI, arch, sAddress));
                    }
                    if (!TryRegisterBlockEnd(newJ.Address, sAddress))
                    {
                        wl.Enqueue((newJ, arch, sAddress));
                    }
                }
                else if (xj < xi)
                {
                    // The block starting at xj is falling through into the block starting at xi:
                    //          xi      y
                    //          +-------+
                    //    +-------------+
                    //    xj
                    // If xi > xj, Bj is split into [xj, xi) while Bi is untouched. We then register Bj at block end
                    // address xi, which will trigger a new iteration of block split when another block has already
                    // registered block end at xi.
                    var newJ = Chop(Bj, 0, j);
                    cfg.B.TryUpdate(Bj.Address, newJ, Bj);
                    RegisterEdge(new CfgEdge(EdgeType.DirectJump, arch, xj, xi));
                    var addrLast = newJ.Instructions[^1].Address;
                    if (!TryRegisterBlockEnd(xj, addrLast))
                    {
                        wl.Enqueue((newJ, arch, addrLast));
                    }
                }
                else
                {
                    // The block starting at xi is falling through into the block starting at xj:
                    //    xi            y
                    //    +-------------+
                    //          +-------+
                    //          xj
                    // If xi < xj, Bi is split into [xi, xj) while Bj is untouched. We then replace Bi with Bj for block
                    // end address y, register Bi for block end address xj, and move out-going edges from Bi to Bj.
                    // Similar to the first case, registering Bi at xj may recursively require another block split.
                    Debug.Assert(xj > xi);
                    var newI = Chop(Bi, 0, i);
                    cfg.B.TryUpdate(Bi.Address, newI, Bi);
                    StealEdges(xi, xj);
                    RegisterEdge(new CfgEdge(EdgeType.DirectJump, arch, xi, xj));
                    var addrLast = newI.Instructions[^1].Address;
                    if (!TryRegisterBlockEnd(xi, addrLast))
                    {
                        wl.Enqueue((newI, arch, addrLast));
                    }
                }
            }
        }

        /// <summary>
        /// Suspends an <see cref="IProcedureWorker"/>.
        /// </summary>
        /// <param name="worker"></param>
        /// <remarks>
        /// This only gets called from a <see cref="ProcedureWorker" /> when it
        /// wants to suspend itself waiting for a caller to return.
        /// </remarks>
        /// </summary>
        public void SuspendWorker(IProcedureWorker worker)
        {
            if (this.workers.TryRemove(worker.ProcedureAddress, out _))
            {
                if (!this.suspendedWorkers.TryAdd(worker.ProcedureAddress, worker))
                    throw new InvalidOperationException($"Procedure worker {worker.ProcedureAddress} is already suspended.");
            }
        }

        public ProcedureReturn ProcedureReturnStatus(Address addrProc)
        {
            if (cfg.Procedures.TryGetValue(addrProc, out Procedure? proc))
                return proc.Returns;
            return ProcedureReturn.Unknown;
        }

        /// <summary>
        /// Creates a new block from an existing block, using the instruction range
        /// [iStart, iEnd).
        /// </summary>
        /// <param name="block">The block to chop.</param>
        /// <param name="iStart">Index of first instruction in the new block.</param>
        /// <param name="iEnd">Index of the first instruction to not include.</param>
        /// <returns>A new, terminated but unregistered block. The caller is responsible for 
        /// registering it.
        /// </returns>
        private static Block Chop(Block block, int iStart, int iEnd)
        {
            var instrs = new MachineInstruction[iEnd - iStart];
            Array.Copy(block.Instructions, iStart, instrs, 0, instrs.Length);
            var addr = instrs[0].Address;
            var instrLast = instrs[^1];
            var size = (instrLast.Address - addr) + instrLast.Length;
            return new Block(addr, size, instrs);
        }

        private void StealEdges(Address xi, Address xj)
        {
            if (cfg.E.TryGetValue(xi, out var edges))
            {
                cfg.E.TryRemove(xi, out _);
                var newEges = edges.Select(e => new CfgEdge(e.Type, e.Architecture, xj, e.To)).ToList();
                cfg.E.TryAdd(xj, newEges);
            }
        }

        private (int, int) FindCommonInstructions(MachineInstruction[] aInstrs, MachineInstruction[] bInstrs)
        {
            int a = aInstrs.Length - 1;
            int b = bInstrs.Length - 1;
            for (;  a >= 0 && b >= 0; --a, --b)
            {
                if (aInstrs[a].Address != bInstrs[b].Address)
                {
                    break;
                }
            }
            return (a + 1, b + 1);
        }

        public void SetProcedureStatus(Address addrProc, ProcedureReturn returns)
        {
            cfg.Procedures[addrProc].Returns = returns;
        }

        public void WakeWorker(IProcedureWorker caller)
        {
            if (!suspendedWorkers.TryRemove(caller.ProcedureAddress, out var cc))
                throw new InvalidOperationException($"Worker {caller.ProcedureAddress} was not suspended.");
            workers.TryAdd(caller.ProcedureAddress, caller);
            Task.Run(() => caller.Process());
        }
    }
}
