using Reko.Core;
using Reko.Core.Configuration;
using Reko.Core.Memory;
using Reko.Core.Services;
using Reko.Services;
using System;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace chunks
{
    class Program
    {
        private const int MemorySize = 1024 * 1024;
        private const int MinChunkSize =  8 * 1024;
        private const int CReps = 1;

        static void Main(string[] args)
        {
            var cfg = MakeServices();
            var includedArchs = new Regex("m68k", RegexOptions.IgnoreCase);
            var exceptedArchs = new Regex("x86.*16|65816|8670|PIC|YMP|C166|Etrax|exago|IA64|pdp10|Vax", RegexOptions.IgnoreCase);
            foreach (var archDef in cfg.GetArchitectures().Where(a => includedArchs.IsMatch(a.Name!)))
            //foreach (var archDef in cfg.GetArchitectures().Where(a => !exceptedArchs.IsMatch(a.Name!)))
            {
                TestArchitecture(cfg, archDef);
            }
        }

        private static RekoConfigurationService MakeServices()
        {
            var services = new ServiceContainer();
            var cfg = RekoConfigurationService.Load(services, "reko/reko.config");
            services.AddService<IFileSystemService>(new FileSystemServiceImpl());
            var testSvc = new TestGenerationService(services)
            {
                OutputDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location!)
            };
            var mutable = new MutableTestGenerationService(testSvc)
            {
                IsMuted = true
            };
            services.AddService<ITestGenerationService>(mutable);
            return cfg;
        }

        private static void TestArchitecture(RekoConfigurationService cfg, ArchitectureDefinition archDef)
        {
            Console.Out.WriteLine("= Testing {0} ============", archDef.Description);
            var work = MakeWorkUnit(cfg, archDef, 42);
            if (work is null)
            {
                Console.Out.WriteLine("*** Failed to load {0}", archDef.Name);
                return;
            }
            var factories = new RewriterTaskFactory[] {
                new LinearTaskFactory(),
                new ShingleTaskFactory(),
                new LinearShingleTaskFactory()
            };
            foreach (var factory in factories)
            {
                CollectStatistics(work, factory);
            }
        }

        private static void CollectStatistics(WorkUnit work, RewriterTaskFactory factory)
        {
            for (int chunkSize = MinChunkSize; chunkSize <= MemorySize; chunkSize *= 16)
            {
                Console.Out.WriteLine("    {0}, chunk size {1}", factory.Name, chunkSize);
                long sum = 0;
                long totClusters = 0;
                for (int rep = 0; rep < CReps; ++rep)
                {
                    var sc = new ChunkScanner(work, chunkSize);
                    var (msec, clusters) = sc.DoIt(factory);
                    sum += msec;
                    totClusters += clusters;
                    Console.Out.Write(" {0,7}", msec);
                    Console.Out.Flush();
                    GC.Collect();
                }
                var avg = sum / (double)CReps;
                var perInstr = sum * 1000.0 / totClusters;
                Console.Out.WriteLine(", avg: {0:0.000} msec; {1:0.000} usec/rtl cluster {2,6} clusters;", avg, perInstr, totClusters);
            }
        }

        private static WorkUnit? MakeWorkUnit(IConfigurationService cfg, ArchitectureDefinition archDef, int seed)
        {
            var arch = cfg.GetArchitecture(archDef.Name!);
            if (arch is null)
            {
                return null;
            }
            var bytes = new byte[MemorySize];
            var rnd = new Random(seed);
            rnd.NextBytes(bytes);

            var addr = Address.Create(arch.PointerType, 0x0010_0000);
            var mem = arch.CreateCodeMemoryArea(addr, bytes);
            return new WorkUnit(arch, mem, mem.BaseAddress, (int)mem.Length);
        }
    }
}
