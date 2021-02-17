using Reko.Core;
using Reko.Core.Memory;
using Reko.Core.Services;
using System;
using System.ComponentModel.Design;

namespace chunks
{
    class Program
    {
        private const int MemorySize = 1024 * 1024;
        private const int CReps = 3;

        static void Main(string[] args)
        {
            var services = new ServiceContainer();
            services.AddService<IFileSystemService>(new FileSystemServiceImpl());
            services.AddService<ITestGenerationService>(new TestGenerationService(services));
            var work = MakeWorkUnit(services, 42);
            //var factory = new LinearTaskFactory();
            var factory = new ShingleTaskFactory();

            // Warm up the caches
            var sc = new ChunkScanner(work, MemorySize);
            sc.DoIt(factory);

            // Now do the stats.
            for (int chunkSize = 16; chunkSize <= MemorySize; chunkSize *= 2)
            {
                Console.Out.WriteLine(" == Shingle scan, chunk size {0}", chunkSize);
                for (int rep = 0; rep < CReps; ++rep)
                {
                    sc = new ChunkScanner(work, chunkSize);
                    var msec = sc.DoIt(factory);
                    Console.Out.Write(" {0,7}", msec);
                    Console.Out.Flush();
                    GC.Collect();
                }
                Console.Out.WriteLine();
            }
        }

        private static WorkUnit MakeWorkUnit(IServiceProvider services, int seed)
        {
            var cfg = Reko.Core.Configuration.RekoConfigurationService.Load(services,"reko/reko.config");
            //var arch = cfg.GetArchitecture("risc-v")!;
            //var arch = cfg.GetArchitecture("m68k")!;
            //var arch = cfg.GetArchitecture("arm")!;
            //var arch = cfg.GetArchitecture("mips-be-32")!;
            var arch = cfg.GetArchitecture("x86-protected-64")!;
            var addr = Address.Create(arch.PointerType, 0x0010_0000);
            var mem = new ByteMemoryArea(addr, new byte[MemorySize]);
            var rnd = new Random(seed);
            rnd.NextBytes(mem.Bytes);
            return new WorkUnit(arch, mem, mem.BaseAddress, mem.Bytes.Length);
        }
    }
}
