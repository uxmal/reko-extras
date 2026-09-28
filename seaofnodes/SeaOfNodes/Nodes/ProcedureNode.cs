using Reko.Core;
using Reko.Core.Collections;
using Reko.Core.Types;
using System.Diagnostics;
using System.Text;

namespace Reko.Extras.SeaOfNodes.Nodes;

/// <summary>
/// This class models the code of a procedure as a value node graph.
/// </summary>
public class ProcedureNode : Node
{

    public ProcedureNode(int number, Procedure proc, params Node?[] inputs) 
        : base(number, VoidType.Instance, inputs)
    {
        this.Procedure = proc;
        this.EndNode = null!;
    }
    
    public EndNode EndNode { get; internal set; }
    public Procedure Procedure { get; }

    public override string Label => "Proc";

    public override void Render(TextWriter sw)
    {
        sw.Write($"start{base.Number}");
    }

    public override void Accept(INodeVisitor visitor)
        => visitor.VisitStartNode(this);

    public override T Accept<T>(INodeVisitor<T> visitor)
        => visitor.VisitStartNode(this);

    public override T Accept<T, C>(INodeVisitor<T, C> visitor, C context)
        => visitor.VisitStartNode(this, context);

    public void Dump()
    {
        var wl = new WorkList<Node>();
        wl.Add(this);
        var visited = new HashSet<Node>();
        while (wl.TryGetWorkItem(out var node))
        {
            if (!visited.Add(node))
                continue;
            wl.AddRange(node.Inputs.Where(i => i is not null)!);
            wl.AddRange(node.Outputs);
        }
        foreach (var n in visited.OrderBy(n => n.Number))
        {
            var sb = new StringBuilder();
            sb.Append($"#{n.Number} {n.Label}");
            sb.AppendLine();
            sb.Append("    uses: ");
            sb.Append(string.Join(
                ", ",
                n.Inputs
                    .Where(i => i is not null)
                    .Select(i => $"[{i!.Number} {i.Label}]")));
            sb.AppendLine();
            sb.Append("    defs: ");
            sb.Append(string.Join(
                ", ",
                n.Outputs
                    .Where(o => o is not null)
                    .Select(o => $"[{o!.Number} {o.Label}]")));
            Console.WriteLine(sb.ToString());
            Debug.WriteLine(sb.ToString());
        }
    }
}