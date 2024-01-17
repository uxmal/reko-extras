using System;
using System.Collections.Concurrent;
using System.Linq;
using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Serialization;
using Reko.Core.Types;

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

        public Expression CallIntrinsic(string name, bool isIdempotent, FunctionType fnType, params Expression[] args)
        {
            throw new System.NotImplementedException();
        }

        public IntrinsicProcedure EnsureIntrinsic(string name, bool isIdempotent, DataType returnType, int arity)
        {
            throw new System.NotImplementedException();
        }

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

        public Expression Intrinsic(string name, bool isIdempotent, DataType returnType, params Expression[] args)
        {
            static Identifier IdFromExpression(Expression arg, int i)
            {
                var id = arg as Identifier;
                var stg = id?.Storage;
                return new Identifier("", arg.DataType, stg!);
            }

            var sig = new FunctionType(
                new Identifier("", returnType, null!),
                args.Select((arg, i) => IdFromExpression(arg, i)).ToArray());
            var intrinsic = EnsureIntrinsicProcedure(name, isIdempotent, sig);
            return new Application(
                new ProcedureConstant(arch.PointerType, intrinsic),
                returnType,
                args);
        }

        public IntrinsicProcedure EnsureIntrinsicProcedure(string name, bool isIdempotent, FunctionType sig)
        {
            if (!intrinsics.TryGetValue(name, out var de))
            {
                de = new ConcurrentDictionary<FunctionType, IntrinsicProcedure>(new DataTypeComparer());
                if (!intrinsics.TryAdd(name, de))
                    de = intrinsics[name];
            }
            if (!de.TryGetValue(sig, out var intrinsic))
            {
                intrinsic = new IntrinsicProcedure(name, isIdempotent, sig);
                if (!de.TryAdd(sig, intrinsic))
                    intrinsic = de[sig];
            }
            return intrinsic;
        }

        public Expression Intrinsic(string name, bool isIdempotent, ProcedureCharacteristics c, DataType returnType, params Expression[] args)
        {
            static Identifier IdFromExpression(Expression arg, int i)
            {
                var id = arg as Identifier;
                var stg = id?.Storage;
                return new Identifier("", arg.DataType, stg!);
            }

            var sig = new FunctionType(
                new Identifier("", returnType, null!),
                args.Select((arg, i) => IdFromExpression(arg, i)).ToArray());
            var intrinsic = EnsureIntrinsicProcedure(name, isIdempotent, sig);
            intrinsic.Characteristics = c;
            return new Application(
                new ProcedureConstant(arch.PointerType, intrinsic),
                returnType,
                args);        
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
