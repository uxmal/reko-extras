using System.Collections.Generic;
using System.Collections.Concurrent;
using System;

namespace ParallelScan
{
    public class Cfg
    {
        /// <summary>
        /// B is a set of address ranges [s, e), representing basic blocks within the binary. Each of these
        /// contains at most one control flow instruction, which if present is the final instruction within
        /// the range, and has incoming control flow at only address s.
        /// </summary>
        public readonly ConcurrentDictionary<Address, Block> B;

        /// <summary>
        /// C is a set of candidate blocks [t], representing addresses which are known to start basic
        /// blocks but do not have known ending addresses yet.
        /// </summary> 
        public readonly ConcurrentDictionary<Address, Address> C;

        /// <summary> 
        /// E ⊆ {(a → b) : a ∈ B,b ∈ B ∪C} is a set of directed edges between basic blocks, representing
        /// possible control flow executions between blocks.
        /// </summary> 
        public readonly ConcurrentDictionary<Address, List<CfgEdge>> E;

        /// <summary> 
        /// F ⊆ B ∪C is the set of function entry blocks.
        /// </summary> 
        public readonly ConcurrentDictionary<Address, Address> F;

        /// <summary>
        /// This dictionary maintains the parent procedure of each basic block.
        /// </summary>
        public ConcurrentDictionary<Address, Address> ParentProcedure;

        /// <summary>
        /// This dictionary maps block end address to block start addresses.
        /// </summary>
        public ConcurrentDictionary<Address, Address> BlockEnds { get; }

        public ConcurrentDictionary<Address, Procedure> Procedures { get; }

        public Cfg()
        {
            this.B = new();
            this.C = new();
            this.E = new();
            this.F = new();
            this.ParentProcedure = new();
            this.BlockEnds = new();
            this.Procedures = new();
        }
    }

    public class CfgEdge
    {

        public CfgEdge(EdgeType type, IProcessorArchitecture arch, Address addrFrom, Address to)
        {
            this.Type = type;
            this.From = addrFrom;
            this.To = to;
            this.Architecture = arch;
        }

        public Address From { get; }
        public Address To { get; }
        public EdgeType Type { get; }
        public IProcessorArchitecture Architecture { get; set; }

        public override string ToString()
        {
            return $"{Type}: {From} -> {To}";
        }
    }

    public enum EdgeType
    {
        Call,
        TailCall,
        Return,
        IndirectCall,
        DirectJump,
        IndirectJump,
        FallThrough,
    }

    public enum ProcedureReturn
    {
        Unknown,
        Diverges,
        Returns,
    }

    public class Block
    {
        public Address Address { get; }
        public long Size { get; }
        public MachineInstruction[] Instructions { get; }

        public Block(Address addr, long size, MachineInstruction[] instrs) 
        {
            this.Address = addr;
            this.Size = size > 0 ? size : throw new ArgumentOutOfRangeException(nameof(size));
            this.Instructions = instrs;
        }

        public Block()
        {
            this.Address = Address.Ptr32(~0);
            this.Size = 1;
            this.Instructions = Array.Empty<MachineInstruction>();
        }

        public override string ToString()
        {
            return $"l{Address}";
        }
    }

    /*
    Partial order: We utilize a partial order between control flow graphs, designed such that a larger
    graph includes more control flow elements. We define G1 ≼ G2 if all of the following are true:
    • The address ranges contained in G1 are also contained by G2. Formally, let A1 and A2 be the
    addresses contained by the blocks in B1 and B2 respectively. Then we require A1 ⊆ A2.
    • The explicit control flow present inG1 is also present inG2, regardless of adjustments to block
    ranges. Formally, for every edge (a = [sa, ea) → b = [sb , eb )) or(a = [sa, ea) → b = [sb ]) ∈ E1,
    one of the similar edges ([s′a, ea) → [sb , e′b)) and ([s′a, ea] → [sb]) must be present in E2.
    Intuitively, G2 may contain additional control flow edges that target addresses inside a or b,
    causing them to be split. The requirement here is that the end address of the source block ea
    and the start address of the target block sb are preserved under the partial order.
    • The implicit control flow through a basic block in G1 is preserved in G2. Formally, for
    every block b = [s0, e) ∈ B1 there is a sequence of blocks [s0,s1), . . . ,[sn, e) ∈ B2 such that
    ([si
    ,si+1) → [si+1,si+2)) ∈ E2 for i = 0, . . . ,n − 2. This means that a block b in G1 can be split
    into multiple smaller blocks in G2 to incorporate other incoming control flow.
    */

    partial class CfgConstruction
    {
    }
}