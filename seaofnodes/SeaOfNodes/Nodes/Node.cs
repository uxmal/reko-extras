namespace Reko.Extras.SeaOfNodes.Nodes;

public abstract class Node
{
    protected Node(int number, params Node?[] inputs)
    {
        this.Number = number;
        this.Inputs = [];
        this.Outputs = [];
        
        foreach (var input in inputs)
        {
            if (input is not null)
                AddEdge(input, this);
        }
    }

    public int Number { get; }
    public List<Node?> Inputs { get; set; }
    public List<Node> Outputs { get; set; }

    public static void AddEdge(Node from, Node to)
    {
        from.Outputs.Add(to);
        to.Inputs.Add(from);
    }   

    public virtual void RenderReference(TextWriter sw)
    {
        sw.Write($"n{this.Number}");
    }

    public abstract void Render(TextWriter sw);
}
