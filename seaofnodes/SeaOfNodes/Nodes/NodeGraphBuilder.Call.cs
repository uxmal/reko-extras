using System.Diagnostics;
using Reko.Analysis;
using Reko.Core;
using Reko.Core.Code;
using Reko.Core.Expressions;
using Reko.Core.Types;

namespace Reko.Extras.SeaOfNodes.Nodes;

public partial class NodeGraphBuilder
{
    public Node VisitCallInstruction(CallInstruction call)
    {
        var callee = call.Callee.Accept(this);
        var pc = call.Callee as ProcedureConstant;
        if (pc is not null && pc.Signature.ParametersValid)
        {
            return GenerateApplicationFromCall(call, pc, callee);
        }
        if (pc?.Procedure is Procedure proc &&
            programFlow.ProcedureFlows.TryGetValue(proc, out var calleeFlow) &&
            !sccProcs.Contains(proc))
        {
            // If the callee is a procedure constant and it's not part of the
            // current recursion group, we should know what storages are live
            // in and trashed.
            return GenerateUseDefsForKnownCallee(call, callee, proc, calleeFlow);
        }
        else
        {
            return GenerateUseDefsForUnknownCallee(call);
        }
    }

    private Node GenerateApplicationFromCall(CallInstruction call, ProcedureConstant callee, Node calleeNode)
    {
        var ab = new NodeApplicationBuilder(factory);
        var s = ab.Build(
            calleeNode,
            call.CallSite,
            cfNode,
            callee.Signature,
            callee.Procedure.Characteristics,
            BindInArg,
            BindOutArg);
        cfNode = s;
        return s;
    }

    private ExpressionNode BindInArg(Storage storage, DataType dt)
    {
        Debug.Assert(this.currentBlock is not null);
        return ReadStorage(this.currentBlock, storage, dt);
    }

    private void BindOutArg(Storage storage, ExpressionNode value)
    {
        Debug.Assert(currentBlock is not null);
        WriteStorage(blocks[currentBlock], storage, value);
    }

    private CallNode GenerateUseDefsForKnownCallee(CallInstruction call, Node callee, Procedure proc, ProcedureFlow calleeFlow)
    {
        var callNode = factory.Call(this.cfNode, callee);
        foreach (var (stgUse, bitRange) in calleeFlow.BitsUsed)
        {
            var value = ReadStorage(this.currentBlock!, stgUse, stgUse.DataType);
            if (stgUse is RegisterStorage reg)
            {
                Debug.Assert(this.cfNode is not null);
                var useNode = factory.Use(this.cfNode, reg, bitRange);
                Node.AddEdge(value, useNode);
                Node.AddEdge(useNode, callNode);
            }
            else 
                throw new NotImplementedException();
        }
        foreach (var stgDef in calleeFlow.Trashed)
        {
            if (stgDef is RegisterStorage reg)
            {
                Debug.Assert(this.cfNode is not null);
                var defNode = factory.Def(this.cfNode, reg, reg.DataType);
                Node.AddEdge(callNode, defNode);
                WriteStorage(blocks[this.currentBlock!], stgDef, defNode);
            }
            else
                throw new NotImplementedException();
        }
        return callNode;
    }

    private Node GenerateUseDefsForUnknownCallee(CallInstruction call)
    {
        throw new NotImplementedException();
    }
}