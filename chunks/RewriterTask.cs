using Reko.Core;
using Reko.Core.Expressions;
using Reko.Core.Rtl;
using Reko.Core.Serialization;
using Reko.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace chunks
{
    public class RewriterTask
    {
        protected WorkUnit workUnit;
        protected int i;
        protected int v;

        protected RewriterTask(WorkUnit workUnit, int i, int v)
        {
            this.workUnit = workUnit;
            this.i = i;
            this.v = v;
        }

        public TaskResult Run()
        {
            IEnumerable<RtlInstructionCluster> rw = CreateReader();
            var clusters =
                (from cluster in rw
                 where cluster.Address - workUnit.MemoryArea.BaseAddress < v
                 select cluster).ToArray();
            return new TaskResult
            {
                Clusters = clusters,
            };
        }

        protected IEnumerable<RtlInstructionCluster> CreateReader()
        {
            var arch = workUnit.Architecture;
            var rdr = arch.Endianness.CreateImageReader(workUnit.MemoryArea, i);
            var state = arch.CreateProcessorState();
            var host = new RewriterHost();
            var rw = arch.CreateRewriter(rdr, state, new StorageBinder(), host);
            return rw;
        }

        private class RewriterHost : IRewriterHost
        {
            private Dictionary<string, IntrinsicProcedure> intrinsics;

            public RewriterHost()
            {
                this.intrinsics = new Dictionary<string, IntrinsicProcedure>();
            }

            public Expression CallIntrinsic(string name, bool isIdempotent, FunctionType fnType, params Expression[] args)
            {
                throw new NotImplementedException();
            }

            public IntrinsicProcedure EnsureIntrinsic(string name, bool isIdempotent, DataType returnType, int arity)
            {
                throw new NotImplementedException();
            }

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

            public Expression Intrinsic(string name, bool isIdempotent, DataType returnType, params Expression[] args)
            {
                return Intrinsic(name, isIdempotent, DefaultProcedureCharacteristics.Instance, returnType, args);

            }

            public Expression Intrinsic(string name, bool isIdempotent, ProcedureCharacteristics c, DataType returnType, params Expression[] args)
            {
                if (!this.intrinsics.TryGetValue(name, out var intrinsic))
                {
                    var ret = new Identifier("", returnType, new TemporaryStorage("xx", 42, returnType));
                    var formals = args.Select((a, i) =>
                        new Identifier($"arg{i}", a.DataType, new TemporaryStorage("aa", 43, a.DataType)))
                        .ToArray();
                    intrinsic = new IntrinsicProcedure(name, isIdempotent, FunctionType.Func(ret, formals))
                    {
                        Characteristics = c,
                    };
                    this.intrinsics.Add(name, intrinsic);
                }
                return new Application(
                    new ProcedureConstant(new UnknownType(), intrinsic),
                    intrinsic.ReturnType,
                    args);
            }

            public bool TryRead(IProcessorArchitecture arch, Address addr, PrimitiveType dt, out Constant value)
            {
                throw new NotImplementedException();
            }

            public void Warn(Address address, string format, params object[] args)
            {
                // Console.WriteLine("Warning: {0} {1}", address, string.Format(format, args));
            }
        }
    }
}