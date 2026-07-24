namespace Reko.Extras.SeaOfNodes.Nodes;

public class NodeGraphRenderer
{
    public void Render(StartNode node, TextWriter sw, bool includeOutputRefs = false)
    {
        var reachable = CollectReachableNodes(node);
        var defMode = reachable.OfType<DefNode>().Any();
        var blocks = reachable.OfType<BlockNode>().ToArray();
        var entryBlock = node.Outputs.OfType<BlockNode>().First();
        var endNode = node.EndNode;
        var exitBlock = endNode.Inputs.OfType<BlockNode>().First();
        var orderedBlocks = blocks
            .Where(block => block != entryBlock && block != exitBlock)
            .OrderBy(block => block.Block.Address)
            .ToList();

        orderedBlocks.Insert(0, entryBlock);

        // Global set of floating nodes already rendered — prevents duplicate rendering
        // when a floating node is consumed in multiple blocks.
        var globalScheduled = new HashSet<Node>();

        orderedBlocks.Add(exitBlock);

        for (int i = 0; i < orderedBlocks.Count; ++i)
        {
            var block = orderedBlocks[i];
            var nextBlock = i + 1 < orderedBlocks.Count ? orderedBlocks[i + 1] : null;
            var suppressFinalNodeNewline = defMode && i == orderedBlocks.Count - 1;
            RenderBlock(block, nextBlock, exitBlock, reachable, sw, !defMode, suppressFinalNodeNewline, globalScheduled, includeOutputRefs);
        }
    }

    private static HashSet<Node> CollectReachableNodes(StartNode start)
    {
        var reachable = new HashSet<Node>();
        var workList = new Stack<Node>();
        workList.Push(start);
        while (workList.Count > 0)
        {
            var node = workList.Pop();
            if (!reachable.Add(node))
                continue;
            foreach (var output in node.Outputs)
            {
                workList.Push(output);
            }
        }
        return reachable;
    }

    private static void RenderBlock(BlockNode block, BlockNode? nextBlock, BlockNode exitBlock, HashSet<Node> reachable, TextWriter sw, bool renderSuccessors, bool suppressFinalNodeNewline, HashSet<Node> globalScheduled, bool includeOutputRefs)
    {
        sw.WriteLine($"{block.Block}:");

        var blockNodes = GetBlockNodes(block, exitBlock, reachable, globalScheduled)
            .DistinctBy(node => node.Number)
            .OrderBy(node => IsBlockTerminator(node) ? 1 : 0)
            .ToArray();

        for (int i = 0; i < blockNodes.Length; ++i)
        {
            var node = blockNodes[i];
            if (IsSuppressed(node))
                continue;
            sw.Write("    ");
            node.Render(sw);
            if (includeOutputRefs)
            {
                sw.Write(" # [ ");
                sw.Write(string.Join(", ", node.Outputs.Select(FormatOutputReference)));
                sw.Write(" ]");
            }
            sw.WriteLine();
        }

        if (ShouldRenderGoto(block, nextBlock, blockNodes))
        {
            sw.WriteLine($"    goto {block.Block.Succ[0]}");
        }

        if (renderSuccessors && blockNodes.Length > 0 && block.Block.Succ.Count > 0)
        {
            var successors = string.Join(", ", block.Block.Succ.Select(succ => succ.ToString()));
            sw.WriteLine($"    // succ: {successors}");
        }
    }

    private static Node[] GetBlockNodes(BlockNode block, BlockNode exitBlock, HashSet<Node> reachable, HashSet<Node> globalScheduled)
    {
        // Step 1: Collect CF-anchored nodes (first input is a CF predecessor in this block's chain).
        var cfAnchoredSet = new HashSet<Node>();
        var workList = new Stack<Node>();
        workList.Push(block);
        while (workList.Count > 0)
        {
            var ctrl = workList.Pop();
            foreach (var output in ctrl.Outputs)
            {
                if (!reachable.Contains(output))
                    continue;
                if (output is StartNode or EndNode or BlockNode)
                    continue;
                if (IsFloating(output))
                    continue; // floating node — not anchored to this CF position
                if (!ReferenceEquals(output.Inputs.FirstOrDefault(), ctrl))
                    continue;
                if (!cfAnchoredSet.Add(output))
                    continue;
                workList.Push(output);
            }
        }

        // Step 2: Topologically sort CF-anchored nodes so that if node A is a
        // transitive dependency of node B through floating inputs, A appears before B.
        // PhiNodes always first; block terminators always last.
        var orderedAnchored = TopologicalSortAnchored(cfAnchoredSet, reachable);

        // Step 3: Build the final list by interleaving floating nodes just before
        // their first CF-anchored consumer ("as late as possible").
        // Use globalScheduled to prevent re-rendering floating nodes already placed
        // in an earlier block (e.g., nodes consumed by multiple blocks).
        var scheduled = new HashSet<Node>(cfAnchoredSet);
        scheduled.UnionWith(globalScheduled);
        var result = new List<Node>();

        foreach (var node in orderedAnchored)
        {
            if (node is not PhiNode && !(ReferenceEquals(block, exitBlock) && node is UseNode))
                ScheduleFloatingInputs(node, scheduled, result, reachable);
            result.Add(node);
        }

        // Step 4: Schedule floating nodes that feed PhiNodes in successor blocks
        // from this block's predecessor position.
        foreach (var succBlockNode in block.Outputs.OfType<BlockNode>())
        {
            var succPredIndex = succBlockNode.Block.Pred.IndexOf(block.Block);
            if (succPredIndex < 0)
                continue;
            var valueInputIndex = succPredIndex + 1; // Inputs[0] is the cfNode (BlockNode)
            foreach (var phi in succBlockNode.Outputs.OfType<PhiNode>())
            {
                if (!reachable.Contains(phi)) continue;
                if (valueInputIndex >= phi.Inputs.Count) continue;
                var phiValue = phi.Inputs[valueInputIndex];
                if (phiValue is null || !IsFloating(phiValue)) continue;
                    ScheduleFloatingInputs(phiValue, scheduled, result, reachable);
                    if (scheduled.Add(phiValue))
                        result.Add(phiValue);
            }

            if (!ReferenceEquals(succBlockNode, exitBlock) || succBlockNode.Block.Pred.Count != 1)
                continue;

            foreach (var use in succBlockNode.Outputs.OfType<UseNode>())
            {
                if (!reachable.Contains(use) || use.Inputs.Count < 2)
                    continue;
                var useValue = use.Inputs[1];
                if (useValue is null || !IsFloating(useValue))
                    continue;
                if (!ShouldHoistExitUseInput(useValue))
                    continue;
                if (!CanHoistExitUseInput(useValue, exitBlock, []))
                    continue;
                if (TryInsertFloatingNodeBeforeTerminator(useValue, scheduled, result, reachable))
                    globalScheduled.Add(useValue);
            }
        }

        // Register newly scheduled floating nodes globally.
        foreach (var n in result)
        {
            if (n.IsFloating)
                globalScheduled.Add(n);
        }

        // Step 5: Schedule named floating "dangling" nodes — those with no non-floating
        // consumers that could anchor them elsewhere — whose non-floating inputs are all
        // CF-anchored in this block. These represent named assignments (e.g., sp += 4
        // after a call) whose result is live but not explicitly consumed in the graph.
        // Step 5: Follow data-flow edges *forward* from locally scheduled floating
        // nodes to find "dangling" named floating nodes that have no graph consumers
        // (Outputs == []) and would otherwise be missed. Example: sp += 4 after a
        // call, where the updated sp has no explicit UseNode connecting it to the graph.
        // Step 5: Follow data-flow edges *forward* from locally scheduled floating
        // nodes to find "dangling" named floating nodes that have no graph consumers
        // (Outputs == []) and would otherwise be missed. Example: sp += 4 after a
        // call, where the updated sp has no explicit UseNode connecting it to the graph.
        // These nodes are inserted just before the block's CF terminator (return/branch)
        // since they are "as late as possible" with no explicit consumer to anchor them.
        var forwardQueue = new Queue<Node>(result.Where(n => n.IsFloating));
        while (forwardQueue.Count > 0)
        {
            var floatNode = forwardQueue.Dequeue();
            foreach (var output in floatNode.Outputs)
            {
                if (!output.IsFloating) continue;
                if (scheduled.Contains(output)) continue;
                if (output.Storage is null) continue; // only rendering named assignments
                ScheduleFloatingInputs(output, scheduled, result, reachable);
                if (scheduled.Add(output))
                {
                    result.Insert(FindTerminatorInsertPos(result), output);
                    globalScheduled.Add(output);
                    forwardQueue.Enqueue(output);
                }
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// Topologically sorts the CF-anchored nodes so that if node A is reachable from
    /// node B through floating-node paths (i.e., B's computation depends on A), A appears
    /// before B. Within that constraint, PhiNodes first, terminators last, then by number.
    /// </summary>
    private static List<Node> TopologicalSortAnchored(HashSet<Node> cfAnchoredSet, HashSet<Node> reachable)
    {
        // Build: for each anchored node, compute the set of anchored nodes it transitively
        // depends on through floating-node paths (i.e., anchored nodes that must precede it).
        var deps = new Dictionary<Node, HashSet<Node>>();
        foreach (var node in cfAnchoredSet)
            deps[node] = [];

        foreach (var node in cfAnchoredSet)
        {
            // Walk floating inputs transitively to find which anchored nodes feed into 'node'.
            CollectAnchoredDeps(node, cfAnchoredSet, reachable, deps[node], []);
        }

        // Kahn's algorithm with stable tiebreaking by (PhiFirst, TerminatorLast, Number).
        // inDegree[b] = number of anchored nodes that must come before b.
        var inDegree = cfAnchoredSet.ToDictionary(n => n, _ => 0);
        foreach (var (node, nodeDeps) in deps)
        {
            foreach (var dep in nodeDeps)
                inDegree[node]++;
        }

        var ready = new SortedSet<Node>(
            Comparer<Node>.Create((a, b) =>
            {
                int pa = a is PhiNode ? 0 : 1;
                int pb = b is PhiNode ? 0 : 1;
                if (pa != pb) return pa.CompareTo(pb);
                int ta = IsBlockTerminator(a) ? 1 : 0;
                int tb = IsBlockTerminator(b) ? 1 : 0;
                if (ta != tb) return ta.CompareTo(tb);
                return a.Number.CompareTo(b.Number);
            }));

        foreach (var node in cfAnchoredSet.Where(n => inDegree[n] == 0))
            ready.Add(node);

        var result = new List<Node>(cfAnchoredSet.Count);
        while (ready.Count > 0)
        {
            var node = ready.Min!;
            ready.Remove(node);
            result.Add(node);

            // For every anchored node that depended on 'node', decrement its in-degree.
            foreach (var other in cfAnchoredSet)
            {
                if (deps[other].Contains(node))
                {
                    inDegree[other]--;
                    if (inDegree[other] == 0)
                        ready.Add(other);
                }
            }
        }

        // Fallback: if there are cycles (shouldn't happen in valid SSA), append remaining nodes.
        foreach (var node in cfAnchoredSet)
        {
            if (!result.Contains(node))
                result.Add(node);
        }

        return result;
    }

    /// <summary>
    /// Walks floating inputs of <paramref name="node"/> transitively and collects any
    /// CF-anchored nodes (other than <paramref name="node"/> itself) that are inputs.
    /// These represent anchored nodes that must be rendered before <paramref name="node"/>.
    /// Memory-chain inputs (Inputs[1] of Load/Store/MemoryNodes) are excluded because
    /// they reflect conservative memory ordering, not computation order.
    /// </summary>
    private static void CollectAnchoredDeps(
        Node node,
        HashSet<Node> cfAnchoredSet,
        HashSet<Node> reachable,
        HashSet<Node> foundDeps,
        HashSet<Node> visited)
    {
        for (int i = 0; i < node.Inputs.Count; i++)
        {
            var input = node.Inputs[i];
            if (input is null || !reachable.Contains(input)) continue;
            if (input is StartNode or EndNode or BlockNode) continue;
            // Skip memory-chain inputs (index 1 on Load/Store/Memory nodes) to avoid
            // creating false cycles between loads and the stores that precede them.
            if (i == 1 && (node is LoadNode or StoreNode or MemoryNode)) continue;
            if (!IsFloating(input))
            {
                // CF-anchored input: if it's in the same block, record it as a dependency.
                if (cfAnchoredSet.Contains(input))
                    foundDeps.Add(input);
                continue;
            }
            // Floating: recurse to find anchored nodes deeper in the floating subgraph.
            if (visited.Add(input))
                CollectAnchoredDeps(input, cfAnchoredSet, reachable, foundDeps, visited);
        }
    }

    /// <summary>
    /// Recursively inserts floating (null-cfNode) inputs of <paramref name="node"/>
    /// into <paramref name="result"/> before <paramref name="node"/> itself, as long
    /// as they have not yet been scheduled.
    /// </summary>
    private static void ScheduleFloatingInputs(
        Node node,
        HashSet<Node> scheduled,
        List<Node> result,
        HashSet<Node> reachable,
        HashSet<Node>? localAnchored = null)
    {
        foreach (var input in node.Inputs)
        {
            if (input is null) continue;
            if (!reachable.Contains(input)) continue;
            if (scheduled.Contains(input)) continue;
            if (input is StartNode or EndNode or BlockNode) continue;
            if (!IsFloating(input)) continue; // CF-anchored globally — belongs to another block

            // Floating input: recurse to schedule its own inputs first.
            ScheduleFloatingInputs(input, scheduled, result, reachable, localAnchored);
            if (scheduled.Add(input))
                result.Add(input);
        }
    }

    private static bool TryInsertFloatingNodeBeforeTerminator(
        Node node,
        HashSet<Node> scheduled,
        List<Node> result,
        HashSet<Node> reachable)
    {
        if (!IsFloating(node) || scheduled.Contains(node))
            return false;

        var toInsert = new List<Node>();
        ScheduleFloatingInputs(node, scheduled, toInsert, reachable);
        if (scheduled.Add(node))
            toInsert.Add(node);
        if (toInsert.Count == 0)
            return false;

        result.InsertRange(FindTerminatorInsertPos(result), toInsert);
        return true;
    }

    private static bool CanHoistExitUseInput(Node node, BlockNode exitBlock, HashSet<Node> visited)
    {
        if (!visited.Add(node))
            return true;

        foreach (var input in node.Inputs)
        {
            if (input is null or StartNode or EndNode or BlockNode)
                continue;
            if (input.IsFloating)
            {
                if (!CanHoistExitUseInput(input, exitBlock, visited))
                    return false;
                continue;
            }

            if (ReferenceEquals(input.Inputs.FirstOrDefault(), exitBlock))
                return false;
        }
        return true;
    }

    private static bool ShouldHoistExitUseInput(Node node)
    {
        return node is not ConstantNode
            and not AddressNode
            and not StringNode
            and not ProcedureConstantNode;
    }

    private static int FindTerminatorInsertPos(List<Node> result)
    {
        int insertPos = result.Count;
        for (int i = result.Count - 1; i >= 0; i--)
        {
            if (IsBlockTerminator(result[i]))
                insertPos = i;
            else
                break;
        }
        return insertPos;
    }

    private static bool IsBlockTerminator(Node node)
    {
        return node is ReturnNode or IfNode or SwitchNode;
    }

    /// <summary>
    /// Returns true if <paramref name="node"/> is a "floating" node — one that was
    /// constructed with a null cfNode and is therefore not anchored to any specific
    /// position in the control-flow chain.
    /// Leaf nodes (no inputs at all) are also floating.
    /// </summary>
    private static bool IsFloating(Node node) => node.IsFloating;

    private static bool IsSuppressed(Node node)
    {
        if (node is ApplicationNode applicationNode &&
            applicationNode.Outputs.Count == 1 &&
            applicationNode.Outputs[0] is SideEffectNode)
            return true;

        // Don't render def subnodes of call nodes; CallNode
        // already renders them as part of its output.
        if (node is DefNode defNode &&
            defNode.Inputs.Count >= 2 &&
            defNode.Inputs[1] is CallNode)
            return true;
        if (node is UseNode useNode &&
            useNode.Outputs.Count == 1 &&
            useNode.Outputs[0] is CallNode)
            return true;

        return false;
    }

    private static string FormatOutputReference(Node node)
    {
        var sw = new StringWriter();
        node.RenderReference(sw);
        return sw.ToString();
    }

    private static bool ShouldRenderGoto(BlockNode block, BlockNode? nextBlock, Node[] blockNodes)
    {
        if (block.Block.Succ.Count == 0)
            return false;

        if (blockNodes.Any(node => node is CfNode))
            return false;

        return nextBlock is null || block.Block.Succ[0] != nextBlock.Block;
    }

    public static string RenderToString(StartNode graph)
    {
        var writer2 = new StringWriter();
        new NodeGraphRenderer().Render(graph, writer2, false);
        return writer2.ToString();
    }
}