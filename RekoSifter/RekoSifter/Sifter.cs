
using Reko;
using Reko.Core;
using Reko.Core.Configuration;
using Reko.Core.Lib;
using Reko.Core.Machine;
using Reko.Core.Memory;
using Reko.Core.Services;
using Reko.Loading;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RekoSifter
{
    public class Sifter
    {
        private const string DefaultArchName = "x86-protected-32";
        private const int DefaultMaxInstrLength = 15;
        private ByteMemoryArea mem;
        private readonly IProcessorArchitecture arch;
        private readonly InstrRenderer instrRenderer;
        private EndianImageReader rdr;
        private IEnumerable<MachineInstruction> dasm;
        private readonly RekoConfigurationService cfgSvc;
        private readonly ITestGenerationService testGen;
        private readonly IFileSystemService fsSvc;
        private readonly Progress progress;
        private int maxInstrLength;
        private int? seed;
        private long? count;
        private bool useRandomBytes;
        private Action<byte[], MachineInstruction?> processInstr; // What to do with each disassembled instruction
        private IDisassembler? otherDasm;
        private char endianness;
        private string syntax;
        private string? inputFilePath = null;

        private readonly Address baseAddress;

        private Action mainProcessing;

        public Sifter(string[] args)
        {
            var sc = new ServiceContainer();
            testGen = new TestGenerationService(sc)
            {
                OutputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            };
            fsSvc = new FileSystemServiceImpl();
            this.cfgSvc = RekoConfigurationService.Load(sc, "reko/reko.config");
            sc.AddService<ITestGenerationService>(testGen);
            sc.AddService<IFileSystemService>(fsSvc);
            sc.AddService<IConfigurationService>(cfgSvc);
            this.processInstr = new Action<byte[], MachineInstruction?>(ProcessInstruction);
            IProcessorArchitecture? arch;
            (arch, this.instrRenderer) = ProcessArgs(args);
            if (arch is null)
            {
                throw new ApplicationException("Unable to load Reko architecture.");
            }
            this.arch = arch;
            this.baseAddress = Address.Create(arch.PointerType, 0x00000000);    //$TODO allow customization?
            this.progress = new Progress();
            this.rdr = default!;
        }

        private void InitializeRekoDisassembler()
        {
            this.rdr = arch.CreateImageReader(mem, 0);
            this.dasm = arch.CreateDisassembler(rdr);
        }

        private IDecompilerService CreateDecompiler(ServiceContainer sc)
        {
            var dcSvc = new DecompilerService();
            dcSvc.Decompiler = new Reko.Decompiler(new Loader(sc), sc)
            {
                Project = new Project
                {
                    Programs =
                    {
                        new Reko.Core.Program
                        {
                            DisassemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                        }
                    }
                }
            };
            return dcSvc;
        }

        bool TryTake(IEnumerator<string> it, out string? arg)
        {
            if (!it.MoveNext())
            {
                arg = null;
                return false;
            }

            arg = it.Current;
            return true;
        }

        private (IProcessorArchitecture?, InstrRenderer) ProcessArgs(IEnumerable<string> args)
        {
            string? archName = DefaultArchName;
            var maxLength = DefaultMaxInstrLength;

            var it = args.GetEnumerator();

            mainProcessing = () => Sift();

            Func<IDisassembler>? mkOtherDasm = null;
            while (it.MoveNext())
            {
                bool res = true;
                string arg = it.Current;

                switch (arg)
                {
                    case "-a":
                    case "--arch":
                        res = TryTake(it, out archName);
                        break;
                    case "--maxlen":
                        res = TryTake(it, out string? maxLengthStr) && int.TryParse(maxLengthStr, out maxLength);
                        break;
                    case "-i":
                    case "--input":
                        res = TryTake(it, out inputFilePath) && File.Exists(inputFilePath);
                        mainProcessing = () => ProcessFile();
                        break;
                    case "-r":
                    case "--random":
                        this.useRandomBytes = true;
                        int seedValue;
                        if (TryTake(it, out string? seedString))
                        {
                            if (int.TryParse(seedString, out seedValue))
                            {
                                this.seed = seedValue;
                            }
                            else
                            {
                                Console.Error.WriteLine("Invalid seed value '{0}'.", seedString);
                            }
                        }
                        break;
                    case "-l":
                    case "--llvm":
                        res = TryTake(it, out string? llvmArch);
                        if (res)
                        {
                            processInstr = this.CompareWithLlvm;
                        }
                        mkOtherDasm = () => new LLVMDasm(llvmArch!);
                        break;
                    case "-o":
                    case "--objdump":
                        res = TryTake(it, out string? objdumpTarget);
                        if (res) {
                            var parts = objdumpTarget!.Split(',', 2);
                            string arch = parts[0];
                            
                            // $TODO: machine parameter
                            // string mach = parts[1];
                            // $TODO: convert machine to uint (BfdMachine)

                            mkOtherDasm = () => new ObjDump(arch);
                            processInstr = this.CompareWithObjdump;
                        }
                        break;
                    case "-c":
                    case "--count":
                        if (TryTake(it, out var sCount) && long.TryParse(sCount, out var count))
                        {
                            this.count = count;
                        }
                        else
                        {
                            res = false;
                        }
                        break; 
                    case "-h":
                    case "--help":
                        res = false;
                        break;
                case "-e":
                case "--endianness":
                    if (TryTake(it, out var sEndianness) && (sEndianness![0] == 'b' || sEndianness[0] == 'l'))
                    {
                        this.endianness = sEndianness[0];
                    }
                    else
                    {
                        res = false;
                    }
                    break;
                case "-s":
                case "--syntax":
                    if (!TryTake(it, out this.syntax))
                    {
                        res = false;
                    }
                    break;
                }

                if (!res)
                {
                    Usage();
                    Environment.Exit(1);
                }
            }

            this.maxInstrLength = maxLength;
            if (mkOtherDasm != null)
                otherDasm = mkOtherDasm();

            return (
                cfgSvc.GetArchitecture(archName!),
                InstrRenderer.Create(archName!));
        }

        private void Usage()
        {
            Console.Write(
@"RekoSifter test tool

Usage:
    RekoSifter -a=<name> | --arch=<name>
Options:
    -a --arch <name>       Use processor architecture <name>.
    --maxlen <length>      Maximum instruction length.
    -i --input <inputfile> Use <inputfile> as input for disassemblers
    -e --endianness <b|l>  Specify either big- or little-endianness.
    -r --random [seed|-]   Generate random byte sequences (using
                            optional seed.
    -l --llvm <llvmarch>   Enable llvm comparison and use arch <llvmarch>.
    -o --objdump <arch>    Enable Objdump comparison
                           Uses the opcodes-* library that contains <arch> in the library name
                           The default (generic) architecture will be used
    -s --syntax <name>     Use the named syntax when generating disassembly text.
    -c <count>             Disassemble <count> instructions, then stop.
");
        }

        public void Run() => mainProcessing();

        private void Sift()
        {
            this.mem = new ByteMemoryArea(baseAddress, new byte[100]);
            InitializeRekoDisassembler();

            otherDasm?.SetEndianness(this.endianness);

            if (useRandomBytes)
            {
                var rng = seed.HasValue
                    ? new Random(seed.Value)
                    : new Random();
                Sift_Random(rng);
            }
            else
            {
                if (arch.InstructionBitSize == 8)
                    Sift_8Bit();
                else if (arch.InstructionBitSize == 32)
                    Sift_32Bit();
                else if (arch.InstructionBitSize == 16)
                    Sift_16Bit();
                else
                    throw new NotImplementedException();
            }

            if(otherDasm is IDisposable disposable) {
                disposable.Dispose();
            }
        }

        public void ProcessInstruction(byte[] bytes, MachineInstruction? instr)
        {
            Console.WriteLine(RenderLine(instr));
        }

        public void CompareWithLlvm(byte[] bytes, MachineInstruction? instr)
        {
            string reko;
            int instrLength;
            if (instr != null)
            {
                reko = instrRenderer.RenderAsLlvm(instr);
                instrLength = instr.Length;
            }
            else
            {
                reko = "(null)";
                instrLength = 0;
            }
            (string llvmOut, byte[]? llvmBytes) = otherDasm!.Disassemble(bytes);

            Console.WriteLine("R:{0,-40} {1}", reko, string.Join(" ", bytes.Take(instrLength).Select(b => $"{b:X2}")));
            Console.WriteLine("L:{0,-40} {1}", llvmOut, string.Join(" ", llvmBytes!.Select(b => $"{b:X2}")));
            Console.WriteLine();
        }

        // These are X86 opcodes that objdump renders in a dramatically different
        // way than Reko, and we're 100% sure Reko is doing it right.
        private static readonly HashSet<byte> objDumpSkips = new HashSet<byte>()
        {
            0x6C,       // insb
            0x6D,       // ins
            0x6E,       // outsb
            0x6F,       // outs

            0x73,       // jnc / jae
            0x74,       // jz / je
            0x75,       // jnz / jne
            0x7A,       // jp/ jpe
            0x7B,       // jpo/ jnp
            0x98,       // cbw / cwde (check this)
            0x9B,       // fwait

            0xA0,       // Objdump calls it 'movabs'. D'oh.
            0xA1,       // movabs
            0xA2,       // movabs
            0xA3,       // movabs
            0xA4,       // movsb
            0xA5,       // movs
            0xA6,       // cmpsb

            0xA7,       // cmps
            0xAA,       // stosb,
            0xAB,       // stos,
            0xAC,       // lodsb
            0xAD,       // lods
            0xAE,       // scasb
            0xAF,       // scas
            0xCC,       // int3
            0xD7,       // xlat
        };

        private void CompareWithObjdump(byte[] bytes, MachineInstruction? instr)
        {
            string reko;
            int instrLength;
            if (instr != null)
            {
                reko = instrRenderer.RenderAsObjdump(instr);
                instrLength = instr.Length;
            }
            else
            {
                reko = "(null)";
                instrLength = 0;
            }
            (string odOut, byte[]? odBytes) = otherDasm!.Disassemble(bytes);
            var rekoIsBad = reko.Contains("illegal") || reko.Contains("invalid");
            var objdIsBad = otherDasm.IsInvalidInstruction(odOut);
            if (rekoIsBad ^ objdIsBad)
            {
                progress.Reset();
                if (!objdIsBad)
                {
                    EmitUnitTest(bytes, odOut);
                }
            }
            else if (!rekoIsBad)
            {
                if (odOut.Trim() != reko.Trim())
                {
                    //$BUG: arch-dependent
                    if (objDumpSkips.Contains(bytes[0]))
                    {
                        progress.Advance();
                    }
                    else
                    {
                        progress.Reset();
                        Console.WriteLine("R:{0,-40} {1}", reko, string.Join(" ", bytes.Take(instrLength).Select(b => $"{b:X2}")));
                        Console.WriteLine("O:{0,-40} {1}", odOut, string.Join(" ", odBytes!.Select(b => $"{b:X2}")));
                        Console.WriteLine();
                    }
                }
                else
                {
                    progress.Advance();
                }
            }
        }

        private void EmitUnitTest(byte[] bytes, string expected)
        {
            if (this.dasm is DisassemblerBase dasm)
            {
                var testPrefix = arch.Name.Replace('-', '_') + "Dis";
                testGen.ReportMissingDecoder(testPrefix, mem.BaseAddress, arch.CreateImageReader(mem, 0), "");
            }
        }

        private void ProcessFile()
        {
            Memory<byte> buf = File.ReadAllBytes(inputFilePath!);

            this.mem = new ByteMemoryArea(baseAddress, buf.ToArray());
            InitializeRekoDisassembler();

            buf.CopyTo(mem.Bytes);

            foreach (var instr in dasm) {
                byte[] instrBytes = buf.Slice((int)instr.Address.ToLinear(), instr.Length).ToArray();
                processInstr(instrBytes, instr);
            }
        }

        private void Sift_Random(Random rng)
        {
            var buf = new byte[maxInstrLength];
            while (DecrementCount())
            {
                rng.NextBytes(buf);
                Buffer.BlockCopy(buf, 0, mem.Bytes, 0, buf.Length);
                
                var instr = Dasm();
                processInstr(buf, instr);
            }
        }

        private void Sift_8Bit()
        {
            var stack = mem.Bytes;
            int iLastByte = 0;
            int lastLen = 0;
            while (iLastByte >= 0 && DecrementCount())
            {
                var instr = Dasm();
                processInstr(mem.Bytes, instr);

                if (rdr.Offset != lastLen)
                {
                    // Length changed, moved marker.
                    iLastByte = (int)rdr.Offset - 1;
                    lastLen = (int)rdr.Offset;
                }
                if (iLastByte >= maxInstrLength)
                {
                    iLastByte = maxInstrLength - 1;
                }
                var val = stack[iLastByte] + 1;
                while (val >= 0x100)
                {
                    stack[iLastByte] = 0;
                    --iLastByte;
                    if (iLastByte < 0)
                        return;
                    val = stack[iLastByte] + 1;
                }
                stack[iLastByte] = (byte)val;
            }
        }

        private void Sift_16Bit()
        {
            var writer = arch.CreateImageWriter(mem, mem.BaseAddress);
            int iLastByte = 0;
            int lastLen = 0;
            while (iLastByte >= 0 && DecrementCount())
            {
                var instr = Dasm();
                processInstr(mem.Bytes, instr);

                if (rdr.Offset != lastLen)
                {
                    iLastByte = (int)rdr.Offset - 1;
                    lastLen = (int)rdr.Offset;
                }
                if (iLastByte >= maxInstrLength)
                {
                    iLastByte = maxInstrLength - 1;
                }
                rdr.Offset = iLastByte & ~1;
                var val = rdr.ReadUInt16() + 1u;
                while (val >= 0x10000)
                {
                    writer.Position = iLastByte & ~1;
                    writer.WriteUInt16(0);
                    iLastByte -= 2;
                    if (iLastByte < 0)
                        return;
                    rdr.Offset = iLastByte & ~1;
                    val = rdr.ReadUInt16() + 1u;
                }
                writer.Position = iLastByte & ~1;
                writer.WriteUInt16((ushort)val);
            }
        }

        private void Sift_32Bit()
        {
            var writer = arch.CreateImageWriter(mem, mem.BaseAddress);
            while (DecrementCount())
            {
                var instr = Dasm();
                processInstr(mem.Bytes, instr);

                rdr.Offset = 0;
                if (!rdr.TryReadUInt32(out uint val) || val == 0xFFFFFFFFu)
                {
                    break;
                }
                ++val;
                writer.Position = 0;
                writer.WriteUInt32(val);
            }
        }


        private MachineInstruction? Dasm()
        {
            rdr.Offset = 0;
            try
            {
                var instr = dasm.First();
                return instr;
            }
            catch
            {
                //$TODO: emit some kind of unit test.
                return null;
            }
        }

        private bool DecrementCount()
        {
            if (count.HasValue)
            {
                --count;
                return count > 0;
            }
            else
            {
                return true;
            }
        }

        private string RenderLine(MachineInstruction? instr)
        {
            var sb = new StringBuilder();
            var sInstr = instr != null
                ? instr.ToString()
                : "*** ERROR ***";
            sb.AppendFormat("{0,-40}", sInstr);
            var bytes = mem.Bytes;
            for (int i = 0; i < rdr.Offset; ++i)
            {
                sb.AppendFormat(" {0:X2}", (uint)bytes[i]);
            }
            return sb.ToString();
        }

    }
}