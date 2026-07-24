using Reko.Core;
using Reko.Core.Types;
using System.Diagnostics;

namespace Reko.Extras.SeaOfNodes.Nodes;

public abstract class Node
{
    protected Node(int number, DataType dt, params Node?[] inputs)
    {
        this.Number = number;
        this.DataType = dt;
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

    protected Node(int number, DataType dt, Node? cfNode, params Node?[] inputs)
    {
        this.Number = number;
        this.DataType = dt;
        this.Inputs = [];
        this.Outputs = [];

        if (cfNode is not null)
            AddEdge(cfNode, this);
        else
            this.Inputs.Add(null);
        foreach (var input in inputs)
        {
            if (input is null)
                throw new InvalidOperationException();
            AddEdge(input, this);
        }
    }

    protected Node(int number, DataType dt, Node? cfNode, Node n, params Node?[] inputs)
    {
        this.Number = number;
        this.DataType = dt; 
        this.Inputs = [];
        this.Outputs = [];

        if (cfNode is not null)
            AddEdge(cfNode, this);
        else
            this.Inputs.Add(null);
        if (n is null)
            throw new InvalidOperationException();
        AddEdge(n, this);
        foreach (var input in inputs)
        {
            AddEdge(input, this);
        }
    }

    /// <summary>
    /// Unique identifier for this node.
    /// </summary>
    public int Number { get; internal set; }

    /// <summary>
    /// The data type of the value produced by this node.
    /// </summary>
    /// <remarks>
    /// Control-flow nodes generate values of type <see cref="VoidType"/>.
    /// </remarks>
    public DataType DataType { get; }

    /// <summary>
    /// Optional <see cref="Storage"/> describing where this node
    /// came from. Node transformations are encouraged to maintain
    /// this as accurately as possible.
    /// </summary>
    public Storage? Storage { get; set; }

    /// <summary>
    /// The inputs consumed by this node.
    /// Think of these as the reaching definitions of this node.
    /// </summary>
    /// <remarks>
    /// By convention, the first (index 0) node is either a 
    /// control flow node, or null. In the latter case the
    /// node has no control flow dependencies.
    /// </remarks>
    public List<Node?> Inputs { get; set; }

    /// <summary>
    /// The nodes consuming the output of this node. Think of these as the
    /// live uses of this node.
    /// </summary>
    public List<Node> Outputs { get; set; }

    /// <summary>
    /// Returns true if this node is "floating" — it was created without an explicit
    /// control-flow dependency (cfNode was null), so it can be scheduled anywhere
    /// between its inputs and its consumers.
    /// </summary>
    /// <inheritdoc />
    public bool IsFloating => Inputs.Count < 1 || Inputs[0] is null;

    /// <summary>
    /// A short textual representation for this node.
    /// </summary>
    public abstract string Label { get; }

    /// <summary>
    /// Cached hash code for this node.
    /// </summary>
    internal int CachedHashCode { get; set; }

    /// <summary>
    /// Establishes an edge between the defining node <paramref name="def"/>
    /// and the using node <paramref name="use"/>.
    /// </summary>
    /// <param name="def">The defining node.</param>
    /// <param name="use">The using node.</param>
    public static void AddEdge(Node? def, Node use)
    {
        if (def is null)
            return;
        def.Outputs.Add(use);
        use.Inputs.Add(def);
    }

    /// <summary>
    /// Removes <paramref name="nodeToRemove"/> from all of its inputs,
    /// potentially making those inputs "dead" (if they don't have side effects).
    /// </summary>
    /// <param name="nodeToRemove">The node to remove from its inputs.</param>
    public static void RemoveFromInputs(Node nodeToRemove)
    {
        foreach (var input in nodeToRemove.Inputs)
        {
            input?.RemoveOutput(nodeToRemove);
        }
        nodeToRemove.Inputs.Clear();
    }

    /// <summary>
    /// Removes <paramref name="nodeToRemove"/> from this node's outputs.
    /// </summary>
    /// <param name="nodeToRemove">The node to remove from this node's outputs.</param>
    public void RemoveOutput(Node nodeToRemove)
    {
        int count = this.Outputs.Count;
        for (int i = 0; i < count; ++i)
        {
            if (this.Outputs[i] == nodeToRemove)
            {
                if (i < count - 1)
                    this.Outputs[i] = this.Outputs[count - 1];
                this.Outputs.RemoveAt(count - 1);
                break;
            }
        }
    }

    /// <summary>
    /// Replaces all uses of <paramref name="original"/> with <paramref name="substitute"/>,
    /// disconnects <paramref name="original"/> from the graph, and updates
    /// <paramref name="substitute"/>.Number to the minimum of both numbers.
    /// </summary>
    /// <param name="original">The node to be replaced.</param>
    /// <param name="substitute">The node to replace with.</param>
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
            producer?.RemoveOutput(original);

        original.Inputs.Clear();
        original.Outputs.Clear();

        substitute.Number = Math.Min(original.Number, substitute.Number);
        substitute.Storage ??= original.Storage;
    }

    public void ReplaceInput(int iInput, Node replacement)
    {
        var oldInput = this.Inputs[iInput];
        Debug.Assert(oldInput is not null);
        oldInput.RemoveOutput(this);
        this.Inputs[iInput] = replacement;
        replacement.Outputs.Add(this);
    }

    public virtual void RenderReference(TextWriter sw)
    {
        if (this.Storage is SequenceStorage seq)
        {
            var seqId = string.Join("_", seq.Elements.Select(e => e.Name));
            sw.Write($"{seqId}_{this.Number}");
        }
        else if (this.Storage is not null)
            sw.Write($"{this.Storage.Name}_{this.Number}");
        else
            sw.Write($"v{this.Number}");
    }

    public abstract T Accept<T>(INodeVisitor<T> visitor);

    public abstract T Accept<T, C>(INodeVisitor<T, C> visitor, C context);

    public abstract void Render(TextWriter sw);

    public override string ToString()
    {
        StringWriter sw = new();
        this.Render(sw);
        return sw.ToString();
    }
}
