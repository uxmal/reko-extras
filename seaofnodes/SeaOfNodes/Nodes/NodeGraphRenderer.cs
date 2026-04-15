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
        if (!defMode || HasRenderableNodes(exitBlock, reachable))
        {
            orderedBlocks.Add(exitBlock);
        }

        for (int i = 0; i < orderedBlocks.Count; ++i)
        {
            var block = orderedBlocks[i];
            var nextBlock = i + 1 < orderedBlocks.Count ? orderedBlocks[i + 1] : null;
            var suppressFinalNodeNewline = defMode && i == orderedBlocks.Count - 1;
            RenderBlock(block, nextBlock, reachable, sw, !defMode, suppressFinalNodeNewline);
        }
    }

    private static bool HasRenderableNodes(BlockNode block, HashSet<Node> reachable)
    {
        return GetBlockNodes(block, reachable).Length > 0;
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

    private static void RenderBlock(BlockNode block, BlockNode? nextBlock, HashSet<Node> reachable, TextWriter sw, bool renderSuccessors, bool suppressFinalNodeNewline)
    {
        sw.WriteLine($"{block.Block}:");

        var blockNodes = GetBlockNodes(block, reachable);

        for (int i = 0; i < blockNodes.Length; ++i)
        {
            var node = blockNodes[i];
            if (IsSuppressed(node))
                continue;
            sw.Write("    ");
            node.Render(sw);
            if (!(suppressFinalNodeNewline && i == blockNodes.Length - 1))
            {
                sw.WriteLine();
            }
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

    private static Node[] GetBlockNodes(BlockNode block, HashSet<Node> reachable)
    {
        var controlledNodes = new HashSet<Node>();
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
                if (!ReferenceEquals(output.Inputs.FirstOrDefault(), ctrl))
                    continue;
                if (!controlledNodes.Add(output))
                    continue;

                workList.Push(output);
            }
        }

        return controlledNodes
            .OrderBy(node => node is PhiNode ? 0 : 1)
            .ThenBy(node => node.Number)
            .ToArray();
    }

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