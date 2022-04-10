﻿using Reko.Core;
using Reko.Core.Diagnostics;
using Reko.Core.Expressions;
using Reko.Core.Lib;
using Reko.Core.Machine;
using Reko.Core.Rtl;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using RtlBlock = Reko.Scanning.RtlBlock;

namespace Reko.ScannerV2
{
    //$DEBUG: remove me
    internal static class CfgEx
    {
        public static void Dump(this ScanResultsV2 self, string x) { }
    }
    /// <summary>
    /// Given a heuristically discovered procedure, attempt to discard as 
    /// many basic blocks as possible.
    /// </summary>
    /// <remarks>   
    /// The heuristics for discarding conflicting blocks are inspired by
    /// Static Disassembly of Obfuscated Binaries
    /// Christopher Kruegel, William Robertson, Fredrik Valeur and Giovanni Vigna
    /// </remarks>
    public class BlockConflictResolver
    {
        private static readonly TraceSwitch trace = new TraceSwitch("HeuristicProcedureScanner", "Display progress of scanner") { Level = TraceLevel.Error };

        private readonly Core.Program program;
        private readonly IRewriterHost host;
        private readonly ScanResultsV2 sr;
        private readonly DirectedGraph<Address> blocks;
        private readonly Func<Address, bool> isAddressValid;
        private HashSet<(Address, Address)> conflicts;

        public BlockConflictResolver(
            Core.Program program,
            ScanResultsV2 sr,
            Func<Address, bool> isAddressValid,
            IRewriterHost host)
        {
            this.program = program;
            this.host = host;
            this.sr = sr;
            this.blocks = sr.ICFG;
            this.isAddressValid = isAddressValid;
            this.conflicts = BuildConflictGraph(blocks.Nodes.Select(n => sr.Blocks[n]));
        }

        /// <summary>
        /// Resolve all block conflicts.
        /// </summary>
        /// <param name="procedureStarts"></param>
        public void ResolveBlockConflicts(IEnumerable<Address> procedureStarts)
        {
            var reachable = TraceReachableBlocks(procedureStarts);
            // We're never using these stats, so disable them for now.
            //ComputeStatistics(reachable);
            Dump("Before conflict resolution");
            RemoveBlocksEndingWithInvalidInstruction();
            this.sr.Dump("After invalid instruction elimination");
            RemoveBlocksConflictingWithValidBlocks(reachable);
            Dump("After conflicting block removal");
            RemoveParentsOfConflictingBlocks();
            this.sr.Dump("After parents of conflicting blocks removed");
            // RemoveBlocksWithFewPredecessors();
            //DumpGraph();
            RemoveBlocksWithFewSuccessors();
            Dump("After few successor removal");
            RemoveConflictsRandomly();
        }

        private void Dump(string message)
        {
            trace.Inform(message);
            trace.Inform("  icfg nodes: {0}, conflicts: {1}", sr.Blocks.Count, conflicts.Count);
        }

        /// <summary>
        /// Trace the reachable blocks using DFS; call them 'reachable'.
        /// </summary>
        /// <returns>A set of blocks considered "valid".</returns>
        private HashSet<Address> TraceReachableBlocks(IEnumerable<Address> procstarts)
        {
            var reachable = new HashSet<Address>();
            var mpAddrToBlock = sr.Blocks;
            foreach (var addrProcStart in procstarts)
            {
                if (mpAddrToBlock.TryGetValue(addrProcStart, out var entry))
                {
                    var r = new DfsIterator<Address>(blocks).PreOrder(entry.Address).ToHashSet();
                    reachable.UnionWith(r);
                }
            }
            return reachable;
        }

        /// <summary>
        /// Given a set of the provably valid basic blocks in the program,
        /// create a five-level deep trie of instructions. Blocks that haven't
        /// been proved valid, but which starting with such instructions are
        /// likely to be valid.
        /// </summary>
        /// <param name="valid"></param>
        private void ComputeStatistics(ISet<RtlBlock> valid)
        {
            if (program == null || program.Architecture == null)
                return;
            var cmp = program.Architecture.CreateInstructionComparer(Normalize.Constants);
            if (cmp == null)
                return;
            //$REVIEW: to what use can we put this?
            var trie = new Reko.Scanning.Trie<MachineInstruction>(cmp);
            foreach (var item in valid.OrderBy(i => i.Address))
            {
                var dasm = program.CreateDisassembler(program.Architecture, item.Address);
                var instrs = dasm.Take(5);
                trie.Add(instrs.ToArray());
            }
            trie.Dump();
        }

        /// <summary>
        /// Given a list of blocks, creates an undirected graph of all blocks which overlap.
        /// </summary>
        /// <param name="blocks"></param>
        /// <returns></returns>
        public static HashSet<(Address, Address)> BuildConflictGraph(
            IEnumerable<RtlBlock> blocks)
        {
            var conflicts = new HashSet<(Address, Address)>(new CollisionComparer());
            // Find all conflicting blocks: pairs that overlap.
            var blockMap = blocks.OrderBy(n => n.Address).ToList();
            for (int i = 0; i < blockMap.Count; ++i)
            {
                var u = blockMap[i];
                var uEnd = u.GetEndAddress();
                for (int j = i + 1; j < blockMap.Count; ++j)
                {
                    var v = blockMap[j];
                    if (v.Address >= uEnd)
                        break;
                    conflicts.Add((u.Address, v.Address));
                }
            }
            return conflicts;
        }

        private void RemoveBlocksEndingWithInvalidInstruction()
        {
            foreach (var n in blocks.Nodes.Where(n => !sr.Blocks[n].IsValid).ToList())
            {
                RemoveBlockFromGraph(n);
            }
        }

        /// <summary>
        /// Any node that is in conflict with a valid node must be removed.
        /// </summary>
        /// <param name="valid"></param>
        private void RemoveBlocksConflictingWithValidBlocks(HashSet<Address> valid)
        {
            // `nodes` are all blocks that weren't reachable by DFS.
            var nodes = blocks.Nodes.Where(nn => !valid.Contains(nn)).ToHashSet();
            foreach (var cc in
                (from c in conflicts
                 where nodes.Contains(c.Item1) && valid.Contains(c.Item2)
                 select c.Item1))
            {
                nodes.Remove(cc);
                RemoveBlockFromGraph(cc);
            }
            foreach (var cc in
                (from c in conflicts
                 where nodes.Contains(c.Item2) && valid.Contains(c.Item1)
                 select c.Item2))
            {
                nodes.Remove(cc);
                RemoveBlockFromGraph(cc);
            }
        }

        private void RemoveBlockFromGraph(Address n)
        {
            trace.Verbose("Removing block: {0}", n);
            var block = sr.Blocks[n];
            foreach (var i in block.Instructions)
            {
                RemoveDirectlyCalledAddress(i);
            }
            if (sr.Predecessors.TryGetValue(n, out var pp))
            {
                foreach (var p in pp)
                {
                    sr.Predecessors[p.From].RemoveAll(e => e.To == n);
                }
                sr.Predecessors.TryRemove(n, out _);
            }
            sr.Blocks.TryRemove(n, out _);
            if (sr.Successors.TryGetValue(n, out var ss))
            {
                foreach (var s in ss)
                {
                    sr.Predecessors[s.To].RemoveAll(e => e.From == n);
                }
                sr.Successors.TryRemove(n, out _);
            }
        }

        private void RemoveDirectlyCalledAddress(RtlInstructionCluster i)
        {
            var callTransfer = InstrClass.Call | InstrClass.Transfer;
            if ((i.Class & callTransfer) != callTransfer)
                return;
            var addrDest = DestinationAddress(i);
            if (addrDest is null)
                return;
            if (!this.sr.SpeculativeProcedures.ContainsKey(addrDest))
                return;
            this.sr.SpeculativeProcedures[addrDest]--;
            if (this.sr.SpeculativeProcedures[addrDest] == 0)
            {
                this.sr.SpeculativeProcedures.TryRemove(addrDest, out _);
            }
        }

        /// <summary>
        /// Find the constant destination of a transfer instruction.
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        private Address? DestinationAddress(RtlInstructionCluster i)
        {
            var rtl = i.Instructions[i.Instructions.Length - 1];
            for (; ; )
            {
                if (!(rtl is RtlIf rif))
                    break;
                rtl = rif.Instruction;
            }
            if (rtl is RtlTransfer xfer)
            {
                return xfer.Target as Address;
            }
            return null;
        }

        private void RemoveConflictsRandomly()
        {
            // foreach (conflict u, v)
            //    pick u, v randomly and remove it.
            foreach (var conflict in conflicts.Where(c => Remaining(c)))
            {
                if (blocks.Nodes.Contains(conflict.Item1) &&
                    blocks.Nodes.Contains(conflict.Item2))
                {
                    RemoveBlockFromGraph(conflict.Item2);
                }
            }
        }

        private void RemoveBlocksWithFewSuccessors()
        {
            // foreach (conflict (u, v)
            //    if (u.succ.Count < v.succ.Count)
            //      remove u
            //    else if (u.succ.Count > v.succ.count)
            //      remove v
            foreach (var conflict in conflicts.Where(c => Remaining(c)))
            {
                if (blocks.Nodes.Contains(conflict.Item1) &&
                    blocks.Nodes.Contains(conflict.Item2))
                {
                    var uCount = blocks.Successors(conflict.Item1).Count;
                    var vCount = blocks.Successors(conflict.Item2).Count;
                    if (uCount < vCount)
                        RemoveBlockFromGraph(conflict.Item1);
                }
            }
        }

        private void RemoveBlocksWithFewAncestors()
        {
            conflicts = BuildConflictGraph(blocks.Nodes.Select(n => sr.Blocks[n]));
            foreach (var conflict in conflicts.Where(c => Remaining(c)))
            {
                var uCount = GetAncestors(conflict.Item1).Count;
                var vCount = GetAncestors(conflict.Item2).Count;
                if (uCount < vCount)
                {
                    RemoveBlockFromGraph(conflict.Item1);
                }
                else if (uCount > vCount)
                {
                    RemoveBlockFromGraph(conflict.Item2);
                }
            }
        }

        private bool Remaining((Address, Address) c)
        {
            var nodes = blocks.Nodes;
            return
                nodes.Contains(c.Item1) &&
                nodes.Contains(c.Item2);
        }

        private void RemoveParentsOfConflictingBlocks()
        {
            // for all conflicting (u,v)
            //    for all common_parents p
            //        remove p.
            foreach (var conflict in conflicts.Where(c => Remaining(c)))
            {
                var uParents = GetAncestors(conflict.Item1);
                var vParents = GetAncestors(conflict.Item2);
                foreach (var uP in uParents)
                    if (vParents.Contains(uP))
                        RemoveBlockFromGraph(uP);
            }
        }

        private IEnumerable<(Address, Address)> GetGaps()
        {
            var blockMap = blocks.Nodes
                .Select(b => sr.Blocks[b])
                .OrderBy(n => n)
                .ToList();
            var addrLastEnd = blockMap[0].FallThrough;
            foreach (var b in blockMap)
            {
                if (addrLastEnd < b.Address)
                    yield return (addrLastEnd, b.Address);
                addrLastEnd = b.FallThrough;
            }
        }

        /// <summary>
        /// The task of the gap completion phase is to improve the
        /// results of our analysis by filling the gaps between basic
        /// blocks in the control flow graph with instructions that
        /// are likely to be valid.
        /// When all possible instruction sequences are determined,
        /// the one with the highest sequence score is selected as the
        /// valid instruction sequence between b1 and b2.
        /// </summary>
        public void GapResolution()
        {
            foreach (var gap in GetGaps())
            {
                int bestScore = 0;
                var bestSequence = default((Address, Address));
                foreach (var sequence in GetValidSequences(gap))
                {
                    int score = ScoreSequence(sequence);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestSequence = sequence;
                    }
                }
            }
        }

        /// <summary>
        /// Find all possibly valid sequences in an address range. A necessary
        /// condition for a valid instruction sequence is that its last 
        /// instruction either (i) ends with the last byte of the gap or 
        /// (ii) its last instruction is a non intra-procedural control 
        /// transfer instruction.
        /// </summary>
        private IEnumerable<(Address, Address)> GetValidSequences((Address, Address) gap)
        {
            int instrGranularity = program.Architecture.InstructionBitSize / program.Architecture.MemoryGranularity;
            for (Address addr = gap.Item1; addr < gap.Item2; addr += instrGranularity)
            {
                var addrStart = addr;
                var dasm = CreateRewriter(addr);
                bool isValid = false;
                foreach (var instr in dasm)
                {
                    var addrNext = instr.Address + instr.Length;
                    if (addrNext > gap.Item2)
                        break;
                    if (addrNext.ToLinear() == gap.Item2.ToLinear())
                    {
                        // Falls out of the gap
                        isValid = true;
                        break;
                    }
                    if (NonLocalTransferInstruction(instr))
                    {
                        isValid = true;
                        break;
                    }
                }
                if (isValid)
                    yield return (addrStart, addr);
            }
        }

        /// <summary>
        /// The sequence
        /// score is a measure of the likelihood that this instruction
        /// sequence appears in an executable. It is calculated
        /// as the sum of the instruction scores of all instructions
        /// in the sequence. The instruction score is similar to
        /// the sequence score and reflects the likelihood of an individual
        /// instruction. Instruction scores are always greater
        /// or equal than zero. Therefore, the score of a sequence
        /// cannot decrease when more instructions are added. We
        /// calculate instruction scores using statistical techniques
        /// and heuristics to identify improbable instructions.
        /// </summary>    
        private int ScoreSequence((Address, Address) sequence)
        {
            return 0;
        }

        private IEnumerable<RtlInstructionCluster> CreateRewriter(Address addr)
        {
            var arch = program.Architecture;
            var rw = arch.CreateRewriter(
                program.CreateImageReader(arch, addr),
                arch.CreateProcessorState(),
                arch.CreateFrame(),
                host);
            return rw;
        }

        private bool NonLocalTransferInstruction(RtlInstructionCluster cluster)
        {
            if (cluster.Class == InstrClass.Linear)
                return false;
            switch (cluster.Instructions.Last())
            {
            case RtlCall _:
                return true;
            case RtlGoto rtlGoto when rtlGoto.Target is Address target:
                return !isAddressValid(target);
            }
            return true;
        }

        // Block conflict resolution

        /// <summary>
        /// Collect all ancestors of the block <paramref name="n"/> in a set.
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public ISet<Address> GetAncestors(Address n)
        {
            var anc = new HashSet<Address>();
            foreach (var p in blocks.Predecessors(n))
            {
                GetAncestorsAux(p, n, anc);
            }
            return anc;
        }

        ISet<Address> GetAncestorsAux(
            Address n,
            Address orig,
            ISet<Address> ancestors)
        {
            if (ancestors.Contains(n) || n == orig)
                return ancestors;
            ancestors.Add(n);
            foreach (var p in sr.Predecessors[n])
            {
                GetAncestorsAux(p.From, orig, ancestors);
            }
            return ancestors;
        }

        private class CollisionComparer : IEqualityComparer<(Address, Address)>
        {
            public bool Equals((Address, Address) x, (Address, Address) y)
            {
                return x.Item1 == y.Item1 && x.Item2 == y.Item2 ||
                       x.Item1 == y.Item2 && x.Item2 == y.Item1;
            }

            public int GetHashCode((Address, Address) obj)
            {
                return obj.Item1.GetHashCode() ^ obj.Item2.GetHashCode();
            }
        }


        // Gap resolution

        // find all valid instruction sequences
        //   valid: if (last_sequence) ends at gap.end
        //   or : last instr is non-intra-procedure control
        //
        // Instruction sequences are found by considering each
        //byte between the start and the end of the gap as a potential
        //start of a valid instruction sequence. Subsequent
        //instructions are then decoded until the instruction sequence
        //either meets or violates one of the necessary conditions
        //defined above. When an instruction sequence
        //meets a necessary condition, it is considered possibly
        //valid and a sequence score is calculated for it. The sequence
        //score is a measure of the likelihood that this instruction
        //sequence appears in an executable. It is calculated
        //as the sum of the instruction scores of all instructions
        //in the sequence. The instruction score is similar to
        //the sequence score and reflects the likelihood of an individual
        //instruction. Instruction scores are always greater
        //or equal than zero. Therefore, the score of a sequence
        //cannot decrease when more instructions are added. We
        //calculate instruction scores using statistical techniques
        //and heuristics to identify improbable instructions.
        //The statistical techniques are based on instruction probabilities
        //and digraphs. Our approach utilizes tables that
        //denote both the likelihood of individual instructions appearing
        //in a binary as well as the likelihood of two instructions
        //occurring as a consecutive pair. The tables
        //were built by disassembling a large set of common executables
        //and tabulating counts for the occurrence of each
        //individual instruction as well as counts for each occurrence
        //of a pair of instructions. These counts were subsequently
        //stored for later use during the disassembly of
        //an obfuscated binary. It is important to note that only instruction
        //opcodes are taken into account with this technique;
        //operands are not considered. The basic score
        //for a particular instruction is calculated as the sum of
        //the probability of occurrence of this instruction and the
        //probability of occurrence of this instruction followed by
        //the next instruction in the sequence.

        //In addition to the statistical technique, a set of heuristics
        //are used to identify improbable instructions. This
        //analysis focuses on instruction arguments and observed
        //notions of the validity of certain combinations of operations,
        //registers, and accessing modes. Each heuristic is
        //applied to an individual instruction and can modify the
        //basic score calculated by the statistical technique. In our
        //current implementation, the score of the corresponding
        //instruction is set to zero whenever a rule matches. Examples
        //of these rules include the following:
        //• operand size mismatches;
        //• certain arithmetic on special-purpose registers;
        //• unexpected register-to-registermoves (e.g., moving
        //from a register other than %ebp into %esp);
        //• moves of a register value into memory referenced
        //by the same register.

        //        When all possible instruction sequences are determined,
        //the one with the highest sequence score is selected as the
        //valid instruction sequence between b1 and b2.
    }
}
