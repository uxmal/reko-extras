using System.Collections.Generic;
using System.Linq;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class NodeGraphRenderer
{
    public void Render(StartNode node, TextWriter sw)
    {
        var reachable = CollectReachableNodes(node);
        var defMode = reachable.OfType<DefNode>().Any();
        var blocks = reachable.OfType<BlockNode>().ToArray();
        var entryBlock = node.Outputs.OfType<BlockNode>().First();
        var endNode = reachable.OfType<EndNode>().First();
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
            RenderBlock(block, nextBlock, reachable, sw, !defMode, suppressFinalNodeNewline, globalScheduled);
        }
    }

    private static bool HasRenderableNodes(BlockNode block, HashSet<Node> reachable, HashSet<Node> globalScheduled)
    {
        return GetBlockNodes(block, reachable, globalScheduled).Length > 0;
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

    private static void RenderBlock(BlockNode block, BlockNode? nextBlock, HashSet<Node> reachable, TextWriter sw, bool renderSuccessors, bool suppressFinalNodeNewline, HashSet<Node> globalScheduled)
    {
        sw.WriteLine($"{block.Block}:");

        var blockNodes = GetBlockNodes(block, reachable, globalScheduled);

        for (int i = 0; i < blockNodes.Length; ++i)
        {
            var node = blockNodes[i];
            if (IsSuppressed(node))
                continue;
            sw.Write("    ");
            node.Render(sw);
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

    private static Node[] GetBlockNodes(BlockNode block, HashSet<Node> reachable, HashSet<Node> globalScheduled)
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

        // Step 2: Order CF-anchored nodes.
        var orderedAnchored = cfAnchoredSet
            .OrderBy(node => node is PhiNode ? 0 : 1)
            .ThenBy(node => node.Number)
            .ToList();

        // Step 3: Build the final list by interleaving floating nodes just before
        // their first CF-anchored consumer ("as late as possible").
        // Use globalScheduled to prevent re-rendering floating nodes already placed
        // in an earlier block (e.g., nodes consumed by multiple blocks).
        var scheduled = new HashSet<Node>(cfAnchoredSet);
        scheduled.UnionWith(globalScheduled);
        var result = new List<Node>();

        foreach (var node in orderedAnchored)
        {
            if (node is not PhiNode)
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
                    // Insert before the CF terminator (ReturnNode / IfNode / SwitchNode)
                    // so the node appears "as late as possible" while still before the
                    // block-ending control transfer.
                    int insertPos = result.Count;
                    for (int i = result.Count - 1; i >= 0; i--)
                    {
                        if (result[i] is ReturnNode or IfNode or SwitchNode)
                            insertPos = i;
                        else
                            break;
                    }
                    result.Insert(insertPos, output);
                    globalScheduled.Add(output);
                    forwardQueue.Enqueue(output);
                }
            }
        }

        return result.ToArray();
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
        HashSet<Node> reachable)
    {
        foreach (var input in node.Inputs)
        {
            if (input is null) continue;
            if (!reachable.Contains(input)) continue;
            if (scheduled.Contains(input)) continue;
            if (input is StartNode or EndNode or BlockNode) continue;
            if (!IsFloating(input)) continue; // CF-anchored globally — belongs to another block

            // Floating input: recurse to schedule its own inputs first.
            ScheduleFloatingInputs(input, scheduled, result, reachable);
            if (scheduled.Add(input))
                result.Add(input);
        }
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

    private static bool ShouldRenderGoto(BlockNode block, BlockNode? nextBlock, Node[] blockNodes)
    {
        if (block.Block.Succ.Count == 0)
            return false;

        if (blockNodes.Any(node => node is CfNode))
            return false;

        return nextBlock is null || block.Block.Succ[0] != nextBlock.Block;
    }
}