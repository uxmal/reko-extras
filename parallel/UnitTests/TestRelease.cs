using NUnit.Framework;
using Reko;
using Reko.Core;
using Reko.Core.Configuration;
using Reko.Core.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelScan.UnitTests
{
    [TestFixture]
    public class TestRelease
    {
        [Test][Ignore("")]
        public async Task Zot()
        {
            var sc = new ServiceContainer();
            var plSvc = new PluginLoaderService();
            sc.AddService<IPluginLoaderService>(plSvc);
            var fsSvc = new FileSystemServiceImpl();
            sc.AddService<IFileSystemService>(fsSvc);
            var cfgSvc = RekoConfigurationService.Load(sc, @"D:\dev\uxmal\reko\extras\parallel\UnitTests\bin\Debug\net5.0\reko\reko.config");
            sc.AddService<IConfigurationService>(cfgSvc);
            var listener = new NullDecompilerEventListener();
            sc.AddService<DecompilerEventListener>(listener);
            var dechost = new Reko.DecompiledFileService(fsSvc, listener);
            sc.AddService<IDecompiledFileService>(dechost);
            var tlSvc = new TypeLibraryLoaderServiceImpl(sc);
            sc.AddService<ITypeLibraryLoaderService>(tlSvc);
            var loader = new Reko.Loading.Loader(sc);
            var reko = new Reko.Decompiler(loader, sc);
            reko.Load(@"D:\dev\uxmal\reko\users\smx-zoo\RELEASE_MIPS\RELEASE");
            var program = reko.Project.Programs[0];
            TryFindSegment(program, ".text", out var seg);
            var scanner = new Scanner(seg.MemoryArea);
            var result = await scanner.ScanAsync(program.EntryPoints.Values);
            Console.Write(result.B.Count);

        }

        private bool TryFindSegment(Program program, string segName, out ImageSegment seg)
        {
            seg = program.SegmentMap.Segments.Values.FirstOrDefault(s => s.Name == segName);
            return seg is not null;
        }
    }
}
