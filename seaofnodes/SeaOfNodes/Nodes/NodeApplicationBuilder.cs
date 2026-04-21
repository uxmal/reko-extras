using Reko.Core;
using Reko.Core.Code;
using Reko.Core.Expressions;
using Reko.Core.Serialization;
using Reko.Core.Types;
using System.Diagnostics;

namespace Reko.Extras.SeaOfNodes.Nodes;

public sealed class NodeApplicationBuilder
{
    private readonly NodeFactory factory;

    public NodeApplicationBuilder(NodeFactory factory)
    {
        this.factory = factory;
    }

    public ApplicationNode Build(
        Application application,
        Node? cfNode,
        Func<Expression, Node> translateExpression)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(translateExpression);

        var callee = translateExpression(application.Procedure);
        var arguments = application.Arguments.Select(translateExpression).ToArray();
        return Build(application.DataType, cfNode, callee, arguments);
    }

    public ApplicationNode Build(
        DataType dataType,
        Node? cfNode,
        Node callee,
        params Node[] arguments)
    {
        ArgumentNullException.ThrowIfNull(dataType);
        ArgumentNullException.ThrowIfNull(callee);
        ArgumentNullException.ThrowIfNull(arguments);

        return factory.Apply(dataType, cfNode, callee, arguments);
    }

    public Node Build(
        Node callee,
        CallSite site,
        Node? cfNode,
        FunctionType sigCallee,
        ProcedureCharacteristics? chr,
        Func<Storage, DataType, ExpressionNode> bindInArg,
        Action<Storage, ExpressionNode> writeOutArg)
    {
        if (sigCallee is null || !sigCallee.ParametersValid)
            throw new InvalidOperationException("No signature available; application cannot be constructed.");
        var parameters = sigCallee.Parameters!;     // Since we checked ParametersValid, we are guaranteed there are parameters.
        var arguments = BindArguments(parameters, sigCallee.IsVariadic, chr, site, bindInArg);
        var idReturn = sigCallee.ReturnValue;
        var dtOut = idReturn is not null ? idReturn.DataType : VoidType.Instance;
        
        arguments.AddRange(BindOutputs(sigCallee.Outputs, chr, writeOutArg));

        ExpressionNode appl = factory.Apply(dtOut, cfNode, callee, arguments.ToArray());
        if (idReturn is null || dtOut is VoidType)
        {
            Debug.Assert(cfNode is not null);
            return factory.SideEffect(cfNode, appl);
        }
        else
        {
            appl.Storage = idReturn.Storage;
            writeOutArg(idReturn.Storage, appl);
            return appl;
        }
    }

    /// <summary>
    /// Bind the formal input parameters of the signature to actual arguments in
    /// the frame of the calling procedure.
    /// </summary>
    /// <param name="parameters">The formal parameters of the callee 
    /// procedure.</param>
    /// <param name="isVariadic">True if the called function is variadic (e.g. <c>printf</c>).</param>
    /// <param name="chr">The <see cref="ProcedureCharacteristics"/> of the called procedure, if any.</param>
    /// <param name="site">The <see cref="CallSite"/> of the call.</param>
    /// <returns>The resulting list of actual arguments.</returns>
    public List<ExpressionNode> BindArguments(Identifier[] parameters, bool isVariadic,
        ProcedureCharacteristics? chr,
        CallSite site,
        Func<Storage, DataType, ExpressionNode> bindInArg)
    {
        var actuals = new List<ExpressionNode>();
        for (int i = 0; i < parameters.Length; ++i)
        {
            var formalArg = parameters[i];
            Storage arg = formalArg.Storage;
            if (arg is StackStorage stk)
            { 
                arg = new StackStorage(stk.StackOffset - site.SizeOfReturnAddressOnStack, stk.DataType);
            }
            var actualArg = bindInArg(arg, formalArg.DataType);
            //$REVIEW: what does null mean here? Forcing an error here generates
            // regressions in the unit tests.
            if (actualArg is not null)
            {
                if (actualArg.DataType.BitSize > formalArg.DataType.BitSize)
                {
                    actualArg = factory.Slice(formalArg.DataType, actualArg, 0);
                }
                actuals.Add(actualArg);
            }
        }
        if (isVariadic)
        {
            return BindVariadicArguments(chr, actuals);
        }
        else
        {
            return actuals;
        }
    }

    /// <summary>
    /// Binds the formal output parameters to variables in the caller's 
    /// frame.
    /// </summary>
    /// <param name="outputs">Output parameters (of which the first
    /// one has already been processed).</param>
    /// <param name="chr">Procedure characteristics.</param>
    /// <returns>The bound output parameters.
    /// </returns>
    private List<ExpressionNode> BindOutputs(
        Identifier[] outputs,
        ProcedureCharacteristics? chr,
        Action<Storage, ExpressionNode> bindOutArg)
    {
        var actuals = new List<ExpressionNode>();
        // We've already processed the first output parameter, which is 
        // modeled as the return value. All other parameters are modeled
        // as OutArguments.
        for (int i = 1; i < outputs.Length; ++i)
        {
            var formalArg = outputs[i];
            var outArg = factory.OutArg(formalArg.DataType);
            bindOutArg(formalArg.Storage, outArg);
            actuals.Add(outArg);
        }
        return actuals;
    }

    /// <summary>
    /// Bind the variadic arguments of a function call.
    /// </summary>
    /// <param name="chr">The <see cref="ProcedureCharacteristics"/> for the
    /// variadic callee. This contains information about how to interpret
    /// format strings or other mechanisms to unpack the number of arguments.
    /// </param>
    /// <param name="actuals">The list of non-variadic actual arguments of the
    /// call.</param>
    /// <returns>A list consisting of all actual arguments including any variadic ones.
    /// </returns>
    public List<ExpressionNode> BindVariadicArguments(ProcedureCharacteristics? chr, List<ExpressionNode> actuals)
    {
        actuals.Add(factory.Word32(0));
        Debug.Print($"{nameof(NodeApplicationBuilder)}: Varargs are not implemented yet.");
        return actuals;
    }
}
