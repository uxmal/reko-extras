using Reko.Core;
using System.Collections.Generic;
using System.Linq;

namespace ParallelScan
{
    /*
* Block End Resolution: Given a graph G containing a candidate block [t] ∈ C,
we define O_BER(G, [t]) as G with the candidate block [t] replaced by an
actual basic block starting at t with a determined end address. There are
three possible cases:

– Block splitting. If there is an existing block b = [s, e) ∈ B such that
s < t < e, then we have to split b into the basic blocks [s,t) and [t, e).
Any incoming edges on b are redirected to [s,t), while outgoing edges on b and
incoming edges on [t] are moved to [t, e).

– Early block ending. If there is an existing block b = [s, e) ∈ B such that
t < s and the range [t,s) contains no control flow instructions, we replace
[t] with [t,s) as in the first case and append the edge ([t,s) → [s, e)).

– Linear parsing. If neither of the previous cases apply, let e be the address
directly after the first control flow instruction following t. We replace [t]
with [t, e) as in the first case.

* Direct Edge Creation: Given a block a in a graph G, which ends with a direct
control flow instruction, we define O_DEC(G, a) as G with outgoing edges
appended to a, based on the control flow instruction within a (if one exists).
There are three cases:

– If a terminates with an unconditional jump to address t, we append the edge
(a → [t]).

– If a = [s, e) terminates with a conditional jump to address t, we append 
edges for the cases where the condition is true (a → [t]) and false (a → [e]).

– If a terminates with a function call instruction to address t, we append the
edge (a → [t]).

* Call Fall-Through Edge Creation: Given an edge e = ([s, e) → f ) in a graph
G where [s, e) contains a function call instruction and f ∈ F , we define
OCF_EC(G, e) as G potentially with the additional edge ([s, e) → [e]) 
summarizing the execution of the callee function. Correct
application of this operation depends on the non-returning function analysis
used to identify whether the target function can return or not.

* Indirect Edge Creation: Given a block a in a graph G which contains a jump
to a dynamic address, we define OI EC(G, a) as G with the additional edges
(a → [t1]), . . . ,(a → [tn]), where t1, . . . ,tn are target addresses
determined statically. It is possible for this operation to add no edges if
the analysis used is insufficient to statically determine the possible targets.

* Function Entry Identification: Given an edge e = (a → b) in a graph G, we
define OF E I(G, e) as G with the block b potentially labeled as a function
entry. This operation is trivial if e was created by an explicit call
instruction, but further heuristics are required to identify functions that
are reached only through optimized tail calls.

* Edge Removal: Given an edge e = (a → b) within a graph G, we define
O_ER(G, e) as G with the edge e removed along with any blocks and edges that
are no longer reachable from any function entry point. Formally, let
B′ ⊆ B and C′ ⊆ C be the sets of blocks and candidate blocks in G reachable
from any block in F without traversing e. We can then define O_ER(G, e) = 
⟨B′,C′, E ∩ {(a′ → b′) : a′ ∈ B′, b′ ∈ B′ ∪ C′} \ {e}, F ⟩.

Starting with the initial graph G0 = ⟨, F0,, F0⟩, where F0 is the set of
candidate function entry blocks discovered via the binary’s symbol table and
unwind information, the task of CFG construction can be abstracted as repeated
application of these operations. We denote G1,G2 · · · ,Gn−1 as the
intermediate results and Gn as the final CFG.


    /*
    input : F : a set of functions; and knownNonRet: a set of
    known non-returning functions
    output : nonRet: a set of identified non-returning functions
    1 nonRet ← knownNonRet ∩F ;
    2 ret ← ∅;
    3 funcList ← F − nonRet;
    4 oldList ← ∅;
    // Fix point calculation
    5 while funcList != oldList do
    6   oldList ← funcList;
        // Inspect all “unknown” functions
    7   for f ∈ funcList do
    8       blocks ← ReachableBlocks(f, funcList);
            // If f has a return block or tail calls a returning
               function, it is a returning function
    9       if ContainRetBlock(blocks) or TailCall(f, ret) then
    10          ret←ret∪{f};
            // If none of the control flow paths returns, f is a
               non-returning function.
    11      if NoBlockedCalls(blocks, funcList) and f /∈ ret then
    12          nonRet ← nonRet ∪{f};
            // Determine the functions to be revisited
    13      funcList ← F − nonRet −ret;
            // Resolve cyclic dependencies
    14      nonRet ← F − ret;
    */
    public class DivergingFunctionFinder
    {

        public HashSet<Address> FindDivergingFunctions(HashSet<Address> F, HashSet<Address> knownNonRet)
        {
            var nonRet = new HashSet<Address>(knownNonRet.Intersect(F));
            var ret = new HashSet<Address>();
            var funcList = F.Except(nonRet);
            // Fixpoint calculation
            bool changed = true;
            while (changed)
            {
                changed = false;
                var oldList = funcList;
                // Inspect unknown functions
                foreach (var f in funcList)
                {
                    var blocks = ReachableBlocks(f, funcList);
                    if (ContainsRetBlock(blocks) || TailCall(f, ret))
                    {
                        ret.Add(f);
                        changed = true;
                    }
                    // If none of the control flow paths returns, f is a nonreturning 
                    // procedure.
                    if (NoBlockedCalls(blocks, funcList) && !ret.Contains(f))
                    {
                        changed = true;
                        nonRet.Add(f);
                    }
                }
                // Determine functions to be revisited
                funcList = F.Except(nonRet).Except(ret);
            }
            return new HashSet<Address>(F.Except(ret));
        }

        private bool NoBlockedCalls(object blocks, IEnumerable<Address> funcList)
        {
            throw new System.NotImplementedException();
        }

        private bool TailCall(Address f, HashSet<Address> ret)
        {
            throw new System.NotImplementedException();
        }

        private bool ContainsRetBlock(IEnumerable<Block> blocks)
        {
            foreach (var b in blocks)
            {
                if (b.Instructions.Length > 0 &&
                    b.Instructions[^1].InstructionClass.HasFlag(InstrClass.Return))
                    return true;
            }
            return false;
        }

        /// Calculates a set of reachable blocks from the entry node of 
        /// function <param name="f"> by
        ///traversing only known intraprocedural edges.
        private IEnumerable<Block> ReachableBlocks(Address f, IEnumerable<Address> funcList)
        {
            throw new System.NotImplementedException();
        }
    }
}