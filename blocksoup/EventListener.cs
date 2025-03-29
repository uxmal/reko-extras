using Reko.Core;
using Reko.Core.Output;
using Reko.Core.Scripts;
using Reko.Core.Services;
using Reko.Services;

namespace Reko.Extras.blocksoup;

internal class EventListener : IDecompilerEventListener
{
    public IProgressIndicator Progress => throw new NotImplementedException();

    public ICodeLocation CreateAddressNavigator(IReadOnlyProgram program, Address address)
    {
        return new CodeLocation();
    }

    public ICodeLocation CreateBlockNavigator(IReadOnlyProgram program, Block block)
    {
        throw new NotImplementedException();
    }

    public ICodeLocation CreateJumpTableNavigator(IReadOnlyProgram program, IProcessorArchitecture arch, Address addrIndirectJump, Address? addrVector, int stride)
    {
        throw new NotImplementedException();
    }

    public ICodeLocation CreateProcedureNavigator(IReadOnlyProgram program, Procedure proc)
    {
        throw new NotImplementedException();
    }

    public ICodeLocation CreateStatementNavigator(IReadOnlyProgram program, Statement stm)
    {
        throw new NotImplementedException();
    }

    public void Error(string message)
    {
        throw new NotImplementedException();
    }

    public void Error(string message, params object[] args)
    {
        throw new NotImplementedException();
    }

    public void Error(Exception ex, string message)
    {
        throw new NotImplementedException();
    }

    public void Error(Exception ex, string message, params object[] args)
    {
        throw new NotImplementedException();
    }

    public void Error(ICodeLocation location, string message)
    {
        throw new NotImplementedException();
    }

    public void Error(ICodeLocation location, string message, params object[] args)
    {
        throw new NotImplementedException();
    }

    public void Error(ICodeLocation location, Exception ex, string message)
    {
        throw new NotImplementedException();
    }

    public void Error(ICodeLocation location, Exception ex, string message, params object[] args)
    {
        throw new NotImplementedException();
    }

    public void Error(ScriptError scriptError)
    {
        throw new NotImplementedException();
    }

    public void Info(string message)
    {
        WriteMessage("Info", new NullCodeLocation(""), message);
    }

    public void Info(string message, params object[] args)
    {
        throw new NotImplementedException();
    }

    public void Info(ICodeLocation location, string message)
    {
        throw new NotImplementedException();
    }

    public void Info(ICodeLocation location, string message, params object[] args)
    {
        throw new NotImplementedException();
    }

    public bool IsCanceled()
    {
        throw new NotImplementedException();
    }

    public void OnProcedureFound(Reko.Core.Program program, Address addrProc)
    {
        throw new NotImplementedException();
    }

    public void Warn(string message)
    {
        throw new NotImplementedException();
    }

    public void Warn(string message, params object[] args)
    {
        throw new NotImplementedException();
    }

    public void Warn(ICodeLocation location, string message)
    {
        WriteMessage("Warning", location, message);
    }

    public void Warn(ICodeLocation location, string message, params object[] args)
    {
        throw new NotImplementedException();
    }

    private void WriteMessage(string category, ICodeLocation location, string message)
    {

    }

    private class CodeLocation : ICodeLocation
    {
        public string Text => throw new NotImplementedException();

        public ValueTask NavigateTo()
        {
            throw new NotImplementedException();
        }
    }
}