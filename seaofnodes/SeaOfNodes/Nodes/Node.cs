using System.Diagnostics;
using Reko.Core;

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
            else
                this.Inputs.Add(null);
        }
    }

    protected Node(int number, Node? cfNode, params Node?[] inputs)
    {
        this.Number = number;
        this.Inputs = [];
        this.Outputs = [];

        if (cfNode is not null)
            AddEdge(cfNode, this);
        else
            this.Inputs.Add(null);
        foreach (var input in inputs)
        {
            Debug.Assert(input is not null);
            AddEdge(input, this);
        }
    }

    protected Node(int number, Node? cfNode, Node n, params Node?[] inputs)
    {
        this.Number = number;
        this.Inputs = [];
        this.Outputs = [];

        if (cfNode is not null)
            AddEdge(cfNode, this);
        else
            this.Inputs.Add(null);
        Debug.Assert(n is not null);
        AddEdge(n, this);
        foreach (var input in inputs)
        {
            AddEdge(input, this);
        }
    }

    public int Number { get; internal set; }
    public Storage? Storage { get; set; }
    public List<Node?> Inputs { get; set; }
    public List<Node> Outputs { get; set; }

    /// <summary>
    /// Returns true if this node is "floating" — it was created without an explicit
    /// control-flow dependency (cfNode was null), so it can be scheduled anywhere
    /// between its inputs and its consumers.
    /// </summary>
    public virtual bool IsFloating => false;

    public abstract string Label { get; }

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
        foreach (var consumer in original.Outputs.ToList())
        {
            for (int i = 0; i < consumer.Inputs.Count; i++)
            {
                if (ReferenceEquals(consumer.Inputs[i], original))
                {
                    consumer.Inputs[i] = substitute;
                }
            }
            substitute.Outputs.Add(consumer);
        }

        foreach (var producer in original.Inputs)
            producer?.Outputs.Remove(original);

        original.Inputs.Clear();
        original.Outputs.Clear();

        substitute.Number = Math.Min(original.Number, substitute.Number);
    }

    public virtual void RenderReference(TextWriter sw)
    {
        if (this.Storage is not null)
            sw.Write($"{this.Storage.Name}_{this.Number}");
        else
            sw.Write($"n{this.Number}");
    }

    public abstract T Accept<T>(INodeVisitor<T> visitor);

    public abstract T Accept<T, C>(INodeVisitor<T, C> visitor, C context);

    public abstract void Render(TextWriter sw);

    public override string ToString()
    {
        StringWriter sw = new();
        this.RenderReference(sw);
        sw.Write($":{Label}");
        return sw.ToString();
    }
}
