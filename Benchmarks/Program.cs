using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel.Design;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Reko.Core;
using Reko.Core.Lib;
using Reko.Core.Machine;
using Reko.Core.Memory;
using Reko.Core.Rtl;
using Reko.Core.Services;

namespace Reko.Benchmarks
{
    [DisassemblyDiagnoser(maxDepth: 1)] // change to 0 for just the [Benchmark] method
    [MemoryDiagnoser(displayGenColumns: false)]
    public class Program
    {
        private readonly byte[] data;
        private readonly ByteMemoryArea mem;
        private readonly ServiceContainer sc;
        private readonly IProcessorArchitecture archX86;
        private readonly IProcessorArchitecture archArm;
        private ulong sum;
        private NullDecompilerEventListener eventListener;

        public static void Main(string[] args) =>
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, DefaultConfig.Instance
                //.WithSummaryStyle(new SummaryStyle(CultureInfo.InvariantCulture, printUnitsInHeader: false, SizeUnit.B, TimeUnit.Microsecond))
                );

        public Program()
        {
            this.data = new byte[1_000_000];
            var rnd = new Random(42);
            rnd.NextBytes(data);
            this.mem = new ByteMemoryArea(Address.Ptr32(0x10_0000), data);

            this.sc = new ServiceContainer();
            sc.AddService<IFileSystemService>(new FileSystemServiceImpl());
            sc.AddService<ITestGenerationService>(new TestGenerationService(sc));
            this.eventListener = new NullDecompilerEventListener();
            sc.AddService<DecompilerEventListener>(eventListener);

            var options = new Dictionary<string, object>();
            this.archX86 = new Reko.Arch.X86.X86ArchitectureFlat32(sc, "", options);
            this.archArm = new Reko.Arch.Arm.Arm32Architecture(sc, "", options);

        }

        private IEnumerable<MachineInstruction> CreateDisassembler<T>() where T : IProcessorArchitecture
        {
            var arch = (T?)Activator.CreateInstance(typeof(T), null, "x86", new Dictionary<string, object>());
            var rdr = arch!.CreateImageReader(mem, 0);
            var dasm = arch.CreateDisassembler(rdr);
            return dasm;
        }

        // BENCHMARKS GO HERE
        //[Benchmark]
        public void ReadBeUints()
        {
            this.sum = 0;
            for (int i = 0; i < data.Length; i += 4)
            {
                sum += Reko.Core.Memory.ByteMemoryArea.ReadBeUInt32(data, i);
            }
        }

        //[Benchmark]
        public void ReadBeUintBuffer()
        {
            this.sum = 0;
            var seq = new ReadOnlySpan<byte>(data);
            for (int i = 0; i < data.Length; i += 4)
            {
                var rspan = seq.Slice(i);
                sum += System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(rspan);
            }
        }

        // [Benchmark]
        public void DisassembleX86Code()
        {
            this.sum = 0;
            var dasm = MakeDasm(archX86);
            foreach (var instr in dasm)
            {
                sum += (uint)(instr.Length + instr.Operands.Length);
            }
        }

        // [Benchmark]
        public void DisassembleArmCode()
        {
            this.sum = 0;
            var dasm = MakeDasm(archArm);
            foreach (var instr in dasm)
            {
                sum += (uint)(instr.Length + instr.Operands.Length);
            }
        }

        private IEnumerable<MachineInstruction> MakeDasm(IProcessorArchitecture arch)
        {
            var rdr = arch.CreateImageReader(mem, 0);
            return arch.CreateDisassembler(rdr);
        }

        
        private IEnumerable<RtlInstructionCluster> MakeRewriter(IProcessorArchitecture arch)
        {
            var rdr = arch.CreateImageReader(mem, 0);
            var rw = arch.CreateRewriter(rdr, arch.CreateProcessorState(), new StorageBinder(), new RewriterHost(arch));
            return rw;
        }

        [Benchmark]
        public void RewriteX86Code()
        {
            this.sum = 0;
            var rw = MakeRewriter(archX86);
            foreach (var instr in rw)
            {
                sum += (uint)(instr.Length);
            }
        }

        // [Benchmark]
        public void RewriteArmCode()
        {
            this.sum = 0;
            var rw = MakeRewriter(archArm);
            foreach (var instr in rw)
            {
                sum += (uint)(instr.Length);
            }
        }

        [Benchmark]
        public void ShingleScanner()
        {
            var platform = new DefaultPlatform(this.sc, archX86);
            var segmentMap = new SegmentMap(new ImageSegment("code", mem.BaseAddress, mem, AccessMode.ReadExecute));
            var program = new Reko.Core.Program(segmentMap, archX86, platform);
            var scanner = new Reko.Scanning.ScannerInLinq(sc, program, new RewriterHost(program.Architecture), eventListener);
            var sr = new Scanning.ScanResults();
            sr.ICFG = new DiGraph<Reko.Scanning.RtlBlock>();
            sr.KnownProcedures = new HashSet<Address>();
            sr.KnownAddresses = new Dictionary<Address, ImageSymbol>();

            scanner.ScanImage(sr);
        }
    }
}
