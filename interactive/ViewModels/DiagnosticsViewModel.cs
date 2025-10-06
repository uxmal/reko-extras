using Reko.Core;
using Reko.Core.Output;
using Reko.Core.Scripts;
using Reko.Services;
using System;
using System.Collections.ObjectModel;

namespace Reko.Extras.Interactive.ViewModels;

public class DiagnosticsViewModel : IDecompilerEventListener
{
    private bool isCanceled;

    public DiagnosticsViewModel(ProgressIndicator progress)
    {
        this.Messages = new ObservableCollection<Message>();
        this.Progress = progress;
    }

    public ObservableCollection<Message> Messages { get; }

    public IProgressIndicator Progress { get; }

    public void Cancel()
    {
        this.isCanceled = true;
    }

    public void Start()
    {
        this.isCanceled = false;
    }

    public ICodeLocation CreateAddressNavigator(IReadOnlyProgram program, Address address)
    {
        throw new NotImplementedException();
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
        this.Messages.Add(new Message("E", message, ""));
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

    public void Error(ProgramAddress paddr, string message)
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
        Messages.Add(new("I", message, ""));
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
        return this.isCanceled;
    }

    public void OnProcedureFound(Program program, Address addrProc)
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

    public void Warn(ProgramAddress paddr, string message)
    {
        throw new NotImplementedException();
    }

    public void Warn(ICodeLocation location, string message)
    {
        throw new NotImplementedException();
    }

    public void Warn(ICodeLocation location, string message, params object[] args)
    {
        throw new NotImplementedException();
    }

    public record Message(string Level, string Text, string Location);
}
