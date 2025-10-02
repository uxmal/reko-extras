using Reko.Core;
using Reko.Core.Configuration;
using Reko.Core.Machine;
using Reko.Core.Memory;
using Reko.Core.Output;
using Reko.Core.Services;
using Reko.ImageLoaders.Elf;
using Reko.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RekoSifter;

public class Sifter
{
    private const string DefaultArchName = "x86-protected-32";
    private const int DefaultMaxInstrLength = 15;

    private readonly IProcessorArchitecture arch;
    private readonly InstrRenderer instrRenderer;
    private readonly RekoConfigurationService cfgSvc;
    private readonly ITestGenerationService testGen;
    private readonly IFileSystemService fsSvc;
    private ByteMemoryArea mem;
    private EndianImageReader rdr;
    private IEnumerable<MachineInstruction> dasm;
    private IProgress progress;
    private int maxInstrLength;
    private int? seed;
    private long? count;
    private Action<byte[], MachineInstruction?> processInstr; // What to do with each disassembled instruction
    private IDisassembler? otherDasm;
    private char endianness;
    private string syntax;
    
    private string? inputFilePath = null;
    private string? objdumpTarget = null;

    private readonly Address baseAddress;

    private Action mainProcessing;

    private readonly ServiceContainer sc;

    private TextWriter? outputStream = Console.Out;
    private TextWriter? errorStream = Console.Error; 
    
    public Sifter(string[] args)
    {
        sc = new ServiceContainer();
        testGen = new TestGenerationService(sc)
        {
            OutputDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
        };
        fsSvc = new FileSystemService();
        this.cfgSvc = RekoConfigurationService.Load(sc, "reko/reko.config");
        sc.AddService<ITestGenerationService>(testGen);
        sc.AddService<IFileSystemService>(fsSvc);
        sc.AddService<IConfigurationService>(cfgSvc);
        sc.AddService<IPluginLoaderService>(new PluginLoaderService());

        this.processInstr = new Action<byte[], MachineInstruction?>(ProcessInstruction);
        this.progress = new Progress();

        IProcessorArchitecture? arch;
        (arch, this.instrRenderer) = ProcessArgs(args);
        if (arch is null)
        {
            throw new ApplicationException("Unable to load Reko architecture.");
        }
        this.arch = arch;
        this.baseAddress = Address.Create(arch.PointerType, 0x00000000);    //$TODO allow customization?
        this.rdr = default!;
    }



    public void SetOutputStream(TextWriter? os)
    {
        outputStream = os;
    }

    public void SetErrorStream(TextWriter? os)
    {
        errorStream = os;
    }

    public void OutputLine(string line)
    {
        outputStream?.WriteLine(line);
    }
    
    public void ErrorLine(string line)
    {
        errorStream?.WriteLine(line);
    }

    public void RenameTestFiles(string filename)
    {
        var ownDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var snapshot = Path.Combine(ownDir, "snapshot");
        Directory.CreateDirectory(snapshot);

        foreach(var f in Directory.EnumerateFiles(ownDir, "*.tests"))
        {
            var prefix = Path.Combine(snapshot,
                filename + "_" + Path.GetFileNameWithoutExtension(f));
            
            for(int i=0; ; i++)
            {
                var candidate = $"{prefix}_{i}.tests";
                Console.WriteLine(candidate);
                if (!File.Exists(candidate))
                {
                    File.Move(f, candidate);
                    break;
                }
            }
        }
    }


    private void InitializeRekoDisassembler()
    {
        this.rdr = arch.CreateImageReader(mem, 0);
        this.dasm = arch.CreateDisassembler(rdr);
    }

    private IDecompilerService CreateDecompiler(ServiceContainer sc)
    {
        var dcSvc = new DecompilerService();
        var project = new Project
        {
            Programs =
                {
                    new Reko.Core.Program
                    {
                        DisassemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                    }
                }
        };

        dcSvc.Decompiler = new Reko.Decompiler(project, sc);
        return dcSvc;
    }

    bool TryTake(IEnumerator<string> it, [MaybeNullWhen(false)] out string arg)
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
            case "--elf":
                res = TryTake(it, out var filePath);
                mainProcessing = () => DasmElfObject(File.ReadAllBytes(filePath));
                break;
            case "--no-progress":
                progress = new NullProgress();
                break;
            case "--server":
                progress = new NullProgress(); // don't report progress
                var srv = new NetworkServer();
                mainProcessing = () => srv.StartMainLoop();
                break;
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
                int seedValue;
                if (TryTake(it, out string? seedString))
                {
                    if (int.TryParse(seedString, out seedValue))
                    {
                        this.seed = seedValue;
                        mainProcessing = DisassembleRandomBytes;
                    }
                    else
                    {
                        ErrorLine($"Invalid seed value '{seedString}'");
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
                res = TryTake(it, out objdumpTarget);
                if (res)
                {
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
                if (!TryTake(it, out var s))
                {
                    res = false;
                }
                this.syntax = s;
                break;
            case "-b":
            case "--bytes":
                if (!TryTake(it, out string? hexbytes))
                {
                    res = false;
                }
                mainProcessing = () => DisassembleHexBytes(hexbytes);
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
        var rekoOptions = new Dictionary<string, object>();
        if (this.endianness != '\0')
        {
            rekoOptions[ProcessorOption.Endianness] = char.ToLowerInvariant(this.endianness) == 'b'
                ? "be"
                : "le";
        }
        return (
            cfgSvc.GetArchitecture(archName!, rekoOptions),
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
    -b <hexbytes>          Disassemble the following hex encoded bytes
    -l --llvm <llvmarch>   Enable llvm comparison and use arch <llvmarch>.
    --elf <inputfile>      Disassemble ELF file and compare
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

        if (arch.InstructionBitSize == 8)
            Sift_8Bit();
        else if (arch.InstructionBitSize == 32)
            Sift_32Bit();
        else if (arch.InstructionBitSize == 16)
            Sift_16Bit();
        else
            throw new NotImplementedException();

        if (otherDasm is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void DisassembleRandomBytes()
    {
        this.mem = new ByteMemoryArea(baseAddress, new byte[100]);
        InitializeRekoDisassembler();

        otherDasm?.SetEndianness(this.endianness);

        var rng = seed.HasValue
            ? new Random(seed.Value)
            : new Random();
        Sift_Random(rng);
        if (otherDasm is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void DisassembleHexBytes(string hexBytes)
    {
        var bytes = BytePattern.FromHexBytes(hexBytes);
        var buf = new Memory<byte>(bytes);
        this.mem = new ByteMemoryArea(baseAddress, bytes);
        InitializeRekoDisassembler();

        otherDasm?.SetEndianness(this.endianness);
        foreach (var instr in dasm)
        {
            byte[] instrBytes = buf.Slice((int) instr.Address.ToLinear(), instr.Length).ToArray();
            processInstr(instrBytes, instr);
        }

        if (otherDasm is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public void ProcessInstruction(byte[] bytes, MachineInstruction? instr)
    {
        OutputLine(RenderLine(instr));
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
        otherDasm!.SetProgramCounter(instr.Address.ToLinear());
        (string llvmOut, byte[]? llvmBytes) = otherDasm!.Disassemble(bytes, true);

        var obj_reko = new
        {
            Id = 'R',
            Text = reko,
            Hex = string.Join(" ", bytes.Take(instrLength).Select(b => $"{b:X2}")),
            Address = instr.Address.ToLinear()
        };
        var obj_other = new
        {
            Id = 'L',
            Text = llvmOut.Trim(),
            Hex = string.Join(" ", llvmBytes.Take(instrLength).Select(b => $"{b:X2}")),
            Address = otherDasm.GetProgramCounter() - (ulong)instrLength
        };

#if false
        OutputLine(JsonSerializer.Serialize(obj_reko));
        OutputLine(JsonSerializer.Serialize(obj_other));
#else

        OutputLine(string.Format("R:{0,-40} :{1}", reko, string.Join(" ", bytes.Take(instrLength).Select(b => $"{b:X2}"))));
        OutputLine(string.Format("L:{0,-40} :{1}", llvmOut, string.Join(" ", llvmBytes!.Select(b => $"{b:X2}"))));
        OutputLine("");
#endif
    }

    // These are X86 opcodes that objdump renders in a dramatically different
    // way than Reko, and we're 100% sure Reko is doing it right.
    private static readonly Dictionary<libopcodes.BfdArchitecture, HashSet<byte>> objDumpSkips = new Dictionary<libopcodes.BfdArchitecture, HashSet<byte>>()
    {
        { libopcodes.BfdArchitecture.BfdArchI386, new HashSet<byte>() {
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

        0xB8,       // movabs
        0xB9,       // movabs
        0xBA,       // movabs
        0xBB,       // movabs
        0xBC,       // movabs
        0xBD,       // movabs
        0xBE,       // movabs
        0xBF,       // movabs

        0xCC,       // int3
        0xD7,       // xlat
        } }
    };

    private void CompareWithObjdump(byte[] bytes, MachineInstruction? instr)
    {
        string reko;
        int instrLength;
        if (instr != null)
        {
            reko = instrRenderer.RenderAsObjdump(instr);
            instrLength = instr.Length;
            otherDasm!.SetProgramCounter(instr.Address.ToLinear());
        }
        else
        {
            reko = "(null)";
            instrLength = 0;
        }
        (string odOut, byte[]? odBytes) = otherDasm!.Disassemble(bytes, true);
        var rekoIsBad = reko.Contains("illegal") || reko.Contains("invalid");
        var objdIsBad = otherDasm.IsInvalidInstruction(odOut);

        var obj_reko = new
        {
            Id = 'R',
            Text = reko,
            Hex = string.Join(" ", bytes.Take(instrLength).Select(b => $"{b:X2}")),
            Address = instr?.Address.ToLinear()?? 0
        };
        var obj_other = new
        {
            Id = 'O',
            Text = odOut.Trim(),
            Hex = string.Join(" ", odBytes.Take(instrLength).Select(b => $"{b:X2}")),
            Address = otherDasm.GetProgramCounter() - (ulong)instrLength
        };
#if false
        OutputLine(JsonSerializer.Serialize(obj_reko));
        OutputLine(JsonSerializer.Serialize(obj_other));
#endif

        if (rekoIsBad ^ objdIsBad)
        {
            progress?.Reset();
            if (!objdIsBad && bytes.Length > 0){
                EmitUnitTest(bytes, odOut);
            }
        }
        else if (!rekoIsBad)
        {
            odOut = instrRenderer.AdjustObjdump(odOut.Trim());
            if (odOut != reko.Trim())
            {
                var otherArch = otherDasm.GetArchitecture();
                if(objDumpSkips.TryGetValue(otherArch, out var skips)
                    && skips.Contains(bytes[0]))
                {
                    progress?.Advance();
                }
                else
                {
                    progress?.Reset();

                    OutputLine(string.Format("R:{0,-40} :{1}", reko, string.Join(" ", bytes.Take(instrLength).Select(b => $"{b:X2}"))));
                    OutputLine(string.Format("O:{0,-40} :{1}", odOut, string.Join(" ", odBytes!.Select(b => $"{b:X2}"))));
                    OutputLine("");
                }
            }
            else
            {
                progress?.Advance();
            }
        }
    }

    private void EmitUnitTest(byte[] bytes, string expected)
    {
        if (this.dasm is DisassemblerBase dasm)
        {
            try
            {
                var testPrefix = arch.Name.Replace('-', '_') + "Dis";
                testGen.ReportMissingDecoder(testPrefix,
                        rdr.Address - (long)rdr.Address.Offset, // - bytes.Length,
                        this.rdr, "");
            } catch
            { }
        }
    }

#if false
    private void SaveIt(byte[] data)
    {
        var prefix = @"C:/temp/obj_";

        for(int i=0; ; i++)
        {
            var path = prefix + i + ".o";
            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, data);
                break;
            }
        }
    }
#endif

    public void DasmElfObject(byte[] objectData)
    {
#if false
        SaveIt(objectData);
#endif

        var loadAddr = Address.Ptr32(0);

        var ldr = new ElfImageLoader(sc, null!, objectData);
        var image = ldr.LoadProgram(loadAddr);
        var codeSeg = image.SegmentMap
            .Segments.Where(x => x.Value.IsExecutable)
            .First().Value;


        var temp = codeSeg.CreateImageReader(arch);
        var data = temp.ReadBytes(codeSeg.ContentSize);
        this.mem = new ByteMemoryArea(loadAddr, data);
        // $BUG
        //var newRdr = new LeImageReader(mem, 0);
        var newRdr = new LeImageReader(mem, loadAddr);

        this.rdr = newRdr;
        this.dasm = arch.CreateDisassembler(newRdr);

        var offset = 0;
        foreach (var instr in dasm)
        {
            // save the current position (after the insn)
            var pos = rdr.Offset;

            // backtrack to the instr start, and read it
            rdr.Offset = offset;
            byte[] instrBytes = rdr.ReadBytes(instr.Length);

            // restore the position
            rdr.Offset = pos;

            offset += instr.Length;
            processInstr(instrBytes, instr);
        }
    }

    private void ProcessFile()
    {
        Memory<byte> buf = File.ReadAllBytes(inputFilePath!);

        this.mem = new ByteMemoryArea(baseAddress, buf.ToArray());
        InitializeRekoDisassembler();

        buf.CopyTo(mem.Bytes);

        foreach (var instr in dasm)
        {
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
            var instr = dasm.FirstOrDefault();
            if (instr is not null)
                return instr;
            Debug.Print("Null instr: {0}", Dump(mem, 0x10));
            return null;
        }
        catch
        {
            //$TODO: emit some kind of unit test.
            return null;
        }
    }

    private string Dump(ByteMemoryArea mem, int v)
    {
        var bytes = mem.Bytes.Take(v);
        var sBytes = string.Join(" ", bytes.Select(b => b.ToString("X2")));
        return sBytes;
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