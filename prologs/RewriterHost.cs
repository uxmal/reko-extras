using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace prologs;

internal class RewriterHost : IRewriterHost
{
    public Constant? GlobalRegisterValue => throw new NotImplementedException();

    public void Error(Address address, string format, params object[] args)
    {
        throw new NotImplementedException();
    }

    public IProcessorArchitecture GetArchitecture(string archMoniker)
    {
        throw new NotImplementedException();
    }

    public Expression? GetImport(Address addrThunk, Address addrInstr)
    {
        throw new NotImplementedException();
    }

    public ExternalProcedure? GetImportedProcedure(IProcessorArchitecture arch, Address addrThunk, Address addrInstr)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}
