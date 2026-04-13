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
        return reachable
            .Where(node => node is not StartNode && node is not EndNode && node is not BlockNode)
            .Any(node => node.Inputs.FirstOrDefault() == block);
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

        var blockNodes = reachable
            .Where(node => node is not StartNode && node is not EndNode && node is not BlockNode)
            .Where(node => node.Inputs.FirstOrDefault() == block)
            .OrderBy(node => node is PhiNode ? 0 : 1)
            .ThenBy(node => node.Number)
            .ToArray();

        for (int i = 0; i < blockNodes.Length; ++i)
        {
            var node = blockNodes[i];
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

    private static bool ShouldRenderGoto(BlockNode block, BlockNode? nextBlock, Node[] blockNodes)
    {
        if (block.Block.Succ.Count == 0)
            return false;

        if (blockNodes.Any(node => node is CfNode))
            return false;

        return nextBlock is null || block.Block.Succ[0] != nextBlock.Block;
    }
}