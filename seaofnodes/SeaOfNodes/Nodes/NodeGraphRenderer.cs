using System.Collections.Generic;
using System.Linq;

namespace Reko.Extras.SeaOfNodes.Nodes;

public class NodeGraphRenderer
{
    public void Render(StartNode node, TextWriter sw)
    {
        var reachable = CollectReachableNodes(node);
        var blocks = reachable.OfType<BlockNode>().ToArray();
        var entryBlock = node.Outputs.OfType<BlockNode>().First();
        var endNode = reachable.OfType<EndNode>().First();
        var exitBlock = endNode.Inputs.OfType<BlockNode>().First();
        var orderedBlocks = blocks
            .Where(block => block != entryBlock && block != exitBlock)
            .OrderBy(block => block.Block.Address)
            .ToList();

        orderedBlocks.Insert(0, entryBlock);
        orderedBlocks.Add(exitBlock);

        foreach (var block in orderedBlocks)
        {
            RenderBlock(block, reachable, sw);
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

    private static void RenderBlock(BlockNode block, HashSet<Node> reachable, TextWriter sw)
    {
        sw.WriteLine($"{block.Block}:");

        var blockNodes = reachable
            .Where(node => node is not StartNode && node is not EndNode && node is not BlockNode)
            .Where(node => node.Inputs.FirstOrDefault() == block)
            .OrderBy(node => node.Number)
            .ToArray();

        foreach (var node in blockNodes)
        {
            sw.Write("    ");
            node.Render(sw);
            sw.WriteLine();
        }

        if (blockNodes.Length > 0 && block.Block.Succ.Count > 0)
        {
            var successors = string.Join(", ", block.Block.Succ.Select(succ => succ.ToString()));
            sw.WriteLine($"    // succ: {successors}");
        }
    }
}