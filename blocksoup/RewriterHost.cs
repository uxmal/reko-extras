using System.Diagnostics.CodeAnalysis;
using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Types;

namespace Reko.Extras.blocksoup;

public class RewriterHost : IRewriterHost
{
    public Constant? GlobalRegisterValue => throw new NotImplementedException();

    public void Error(Address address, string format, params object[] args)
    {
        WriteMessage("Error", $"{address}: {string.Format(format, args)}");
    }

    public IProcessorArchitecture GetArchitecture(string archMoniker)
    {
        throw new NotImplementedException();
    }

    public Expression? GetImport(Address addrThunk, Address addrInstr)
    {
        return null;
    }

    public ExternalProcedure? GetImportedProcedure(IProcessorArchitecture arch, Address addrThunk, Address addrInstr)
    {
        return null;
    }

    public ExternalProcedure? GetInterceptedCall(IProcessorArchitecture arch, Address addrImportThunk)
    {
        throw new NotImplementedException();
    }

    public bool TryRead(IProcessorArchitecture arch, Address addr, PrimitiveType dt, [MaybeNullWhen(false)] out Constant value)
    {
        throw new NotImplementedException();
    }

    public void Warn(Address address, string format, params object[] args)
    {
        WriteMessage("Warning", $"{address}: {string.Format(format, args)}");
    }

    private void WriteMessage(string category, string message)
    {

    }
}