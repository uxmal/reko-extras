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

    protected Node(int number, Node? cfNode,params Node?[] inputs)
    {
        this.Number = number;
        this.Inputs = [];
        this.Outputs = [];
        
        AddEdge(cfNode, this);
        foreach (var input in inputs)
        {
            if (input is not null)
                AddEdge(input, this);
        }
    }

    public int Number { get; internal set; }
    public List<Node?> Inputs { get; set; }
    public List<Node> Outputs { get; set; }

    public static void AddEdge(Node? def, Node use)
    {
        if (def is null)
            return;
        def.Outputs.Add(use);
        use.Inputs.Add(def);
    }

    /// <summary>
    /// Replaces all uses of <paramref name="original"/> with <paramref name="substitute"/>,
    /// disconnects <paramref name="original"/> from the graph, and updates
    /// <paramref name="substitute"/>.Number to the minimum of both numbers.
    /// </summary>
    public static void Replace(Node original, Node substitute)
    {
        substitute.Number = Math.Min(original.Number, substitute.Number);

        foreach (var consumer in original.Outputs.ToList())
        {
            for (int i = 0; i < consumer.Inputs.Count; i++)
            {
                if (ReferenceEquals(consumer.Inputs[i], original))
                {
                    consumer.Inputs[i] = substitute;
                    substitute.Outputs.Add(consumer);
                }
            }
        }

        foreach (var producer in original.Inputs)
            producer?.Outputs.Remove(original);

        original.Inputs.Clear();
        original.Outputs.Clear();
    }

    public virtual void RenderReference(TextWriter sw)
    {
        sw.Write($"n{this.Number}");
    }

    public abstract void Render(TextWriter sw);
}
