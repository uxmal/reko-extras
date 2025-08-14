using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Types;
using System.Diagnostics.CodeAnalysis;

namespace Reko.Extras.Interactive;

public class RewriterHost : IRewriterHost
{
    public Constant? GlobalRegisterValue => throw new System.NotImplementedException();

    public void Error(Address address, string format, params object[] args)
    {
        throw new System.NotImplementedException();
    }

    public IProcessorArchitecture GetArchitecture(string archMoniker)
    {
        throw new System.NotImplementedException();
    }

    public Expression? GetImport(Address addrThunk, Address addrInstr)
    {
        throw new System.NotImplementedException();
    }

    public ExternalProcedure? GetImportedProcedure(IProcessorArchitecture arch, Address addrThunk, Address addrInstr)
    {
        throw new System.NotImplementedException();
    }

    public ExternalProcedure? GetInterceptedCall(IProcessorArchitecture arch, Address addrImportThunk)
    {
        throw new System.NotImplementedException();
    }

    public bool TryRead(IProcessorArchitecture arch, Address addr, PrimitiveType dt, [MaybeNullWhen(false)] out Constant value)
    {
        throw new System.NotImplementedException();
    }

    public void Warn(Address address, string format, params object[] args)
    {
        throw new System.NotImplementedException();
    }
}