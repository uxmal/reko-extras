using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Types;
using System;
using System.Collections.Concurrent;

namespace Reko.Benchmarks
{
    public class RewriterHost : IRewriterHost
    {
        private readonly IProcessorArchitecture arch;

        public RewriterHost(IProcessorArchitecture arch)
        {
            this.arch = arch;
        }

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<FunctionType, IntrinsicProcedure>> intrinsics = new();

        public Constant? GlobalRegisterValue => throw new NotImplementedException();

        public void Error(Address address, string format, params object[] args)
        {
            // Console.WriteLine("Error: {0}", string.Format(format, args));
        }

        public IProcessorArchitecture GetArchitecture(string archMoniker)
        {
            throw new System.NotImplementedException();
        }

        public Expression? GetImport(Address addrThunk, Address addrInstr)
        {
            return null;
        }

        public ExternalProcedure? GetImportedProcedure(IProcessorArchitecture arch, Address addrThunk, Address addrInstr)
        {
            return null;
        }

        public ExternalProcedure GetInterceptedCall(IProcessorArchitecture arch, Address addrImportThunk)
        {
            throw new System.NotImplementedException();
        }

        public bool TryRead(IProcessorArchitecture arch, Address addr, PrimitiveType dt, out Constant value)
        {
            throw new System.NotImplementedException();
        }

        public void Warn(Address address, string format, params object[] args)
        {
            // Console.WriteLine("Warning: {0}", string.Format(format, args));
        }
    }

}
