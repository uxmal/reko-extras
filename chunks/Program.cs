using Reko.Core;
using Reko.Core.Memory;
using Reko.Core.Services;
using System;
using System.ComponentModel.Design;

namespace chunks
{
    class Program
    {
        private const int MemorySize = 1_000_000;

        static void Main(string[] args)
        {
            var services = new ServiceContainer();
            services.AddService<IFileSystemService>(new FileSystemServiceImpl());
            //services.AddService<ITestGenerationService>(new TestGenerationService(services));
            var work = MakeWorkUnit(services, 42);
            var sc = new ChunkScanner(work, 0x10000);
            sc.doit();
        }

        private static WorkUnit MakeWorkUnit(IServiceProvider services, int seed)
        {
            var cfg = Reko.Core.Configuration.RekoConfigurationService.Load(services,"reko/reko.config");
            var arch = cfg.GetArchitecture("x86-protected-64")!;
            var mem = new ByteMemoryArea(Address.Ptr64(0x0010_0000), new byte[MemorySize]);
            var rnd = new Random(seed);
            rnd.NextBytes(mem.Bytes);
            return new WorkUnit(arch, mem, mem.BaseAddress, mem.Bytes.Length);
        }
    }
}
