using Reko.Core;
using Reko.Core.Configuration;
using Reko.Core.Machine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RekoSifter
{
    public struct ParseResult
    {
        public string hex;
        public string asm;
    }

    public class Sifter
    {
        private const string DefaultArchName = "x86-protected-32";
        private const int DefaultMaxInstrLength = 15;

        private readonly MemoryArea mem;
        private IProcessorArchitecture arch;
        private InstrRenderer instrRenderer;
        private readonly EndianImageReader rdr;
        private readonly IEnumerable<MachineInstruction> dasm;
        private readonly RekoConfigurationService cfgSvc;
        private int maxInstrLength;
        private int? seed;
        private long? count;
        private bool useRandomBytes;
        private string llvmArch = null;
        private Action<byte[], MachineInstruction> processInstr;
        private ObjDump objDump;
        private Progress progress;

        public Sifter(string[] args)
        {
            this.cfgSvc = Reko.Core.Configuration.RekoConfigurationService.Load("reko/reko.config");
            this.processInstr = new Action<byte[], MachineInstruction>(ProcessInstruction);
            ProcessArgs(args);
            var baseAddress = Address.Ptr32(0x00000000);    //$TODO allow customization?
            this.mem = new MemoryArea(baseAddress, new byte[100]);
            this.rdr = arch.CreateImageReader(mem, 0);
            this.dasm = arch.CreateDisassembler(rdr);
            this.progress = new Progress();
        }

        bool TryTake(IEnumerator<string> it, out string arg)
        {
            if (!it.MoveNext())
            {
                arg = null;
                return false;
            }

            arg = it.Current;
            return true;
        }

        private void ProcessArgs(IEnumerable<string> args)
        {
            var archName = DefaultArchName;
            var maxLength = DefaultMaxInstrLength;

            var it = args.GetEnumerator();

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
                        res = TryTake(it, out string maxLengthStr) && int.TryParse(maxLengthStr, out maxLength);
                        break;
                    case "-r":
                    case "--random":
                        this.useRandomBytes = true;
                        int seedValue;
                        if (TryTake(it, out string seedString))
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
                        res = TryTake(it, out this.llvmArch);
                        if (res)
                        {
                            processInstr = this.CompareWithLlvm;
                        }
                        break;
                    case "-o":
                    case "--objdump":
                        res = TryTake(it, out string objdumpTarget);
                        if (res) {
                            var parts = objdumpTarget.Split(',', 2);
                            string arch = parts[0];
                            
                            // $TODO: machine parameter
                            // string mach = parts[1];
                            // $TODO: convert machine to uint (BfdMachine)

                            objDump = new ObjDump(arch);
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
                }

                if (!res)
                {
                    Usage();
                    Environment.Exit(1);
                }
            }

            this.arch = cfgSvc.GetArchitecture(archName);
            this.instrRenderer = InstrRenderer.Create(archName);
            this.maxInstrLength = maxLength;
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
    -r --random [seed|-]   Generate random byte sequences (using
                            optional seed.
    -l --llvm <llvmarch>   Enable llvm comparison and use arch <llvmarch>.
    -o --objdump <arch>    Enable Objdump comparison
                           Uses the opcodes-* library that contains <arch> in the library name
                           The default (generic) architecture will be used
    -c <count>             Disassemble <count> instructions, then stop.
");
        }

        public void Sift()
        {
            if (useRandomBytes)
            {
                var rng = seed.HasValue
                    ? new Random(seed.Value)
                    : new Random();
                Sift_Random(rng);
            }
            if (arch.InstructionBitSize == 8)
                Sift_8Bit();
            else if (arch.InstructionBitSize == 32)
                Sift_32Bit();
            else if (arch.InstructionBitSize == 16)
                Sift_16Bit();
            else
                throw new NotImplementedException();
        }

        static string RenderLLVM(ParseResult obj)
        {
            return string.Format("L:{0,-45}{1}", obj.asm, obj.hex);
        }

        public void ProcessInstruction(byte[] bytes, MachineInstruction instr)
        {
            Console.WriteLine(RenderLine(instr));
        }


        public void CompareWithLlvm(byte[] bytes, MachineInstruction instr)
        {
            var reko = instrRenderer.RenderAsLlvm(instr);
            Console.WriteLine("R:{0}", reko);
            foreach (var obj in LLVM.Disassemble(llvmArch, mem.Bytes))
            {
                var llvm = RenderLLVM(obj);
                Console.WriteLine(llvm);
                break; // cheaper than Take(1), less GC.
            }
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
            0xA4,       // movsb
            0xA5,       // movs
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

        private void CompareWithObjdump(byte[] bytes, MachineInstruction instr)
        {
            var reko = instrRenderer.RenderAsObjdump(instr);
            (string odOut, byte[] odBytes) = objDump.Disassemble(bytes);
            var sInstr = instr.ToString();
            var rekoIsBad = sInstr.Contains("illegal") || sInstr.Contains("invalid");
            var objdIsBad = odOut.Contains("(bad)");
            if (rekoIsBad ^ objdIsBad)
            {
                progress.Reset();
                if (!odOut.Contains("bad"))
                {
                    EmitUnitTest(bytes, odOut);
                }
                //Console.WriteLine("*** discrepancy between Reko disassembler and objdump");
                //Console.In.ReadLine();
            }
            else if (!rekoIsBad)
            {
                if (odOut.Trim() != reko.Trim())
                {
                    if (objDumpSkips.Contains(bytes[0]))
                    {
                        progress.Advance();
                    }
                    else
                    {
                        progress.Reset();
                        Console.WriteLine("R:{0,-40} {1}", reko, string.Join(" ", bytes.Take(instr.Length).Select(b => $"{b:X2}")));
                    Console.WriteLine("O:{0,-40} {1}", odOut, string.Join(" ", odBytes.Select(b => $"{b:X2}")));
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
                var hex = string.Join("", bytes.Select(b => $"{b:X2}"));
                var testPrefix = arch.Name.Replace('-', '_') + "Dis";
                dasm.EmitUnitTest(arch.Name, hex, "", testPrefix, mem.BaseAddress, w =>
                {
                    w.WriteLine("    AssertCode(\"{0}\", \"{1}\");",
                        expected.Trim(),
                        hex);
                });
            }
        }

        public void Sift_Random(Random rng)
        {
            var buf = new byte[maxInstrLength];
            while (DecrementCount())
            {
                //Console.WriteLine();
                rng.NextBytes(buf);
                Buffer.BlockCopy(buf, 0, mem.Bytes, 0, buf.Length);
                
                var instr = Dasm();
                processInstr(buf, instr);
                --this.count;
            }
        }

        public void Sift_8Bit()
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

        public void Sift_16Bit()
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

        public void Sift_32Bit()
        {
            var writer = arch.CreateImageWriter(mem, mem.BaseAddress);
            while (DecrementCount())
            {
                var instr = Dasm();
                processInstr(mem.Bytes, instr);

                rdr.Offset = 0;
                var val = rdr.ReadUInt32(0);
                if (val == 0xFFFFFFFFu)
                {
                    break;
                }
                ++val;
                writer.Position = 0;
                writer.WriteUInt32(val);
            }
        }


        private MachineInstruction Dasm()
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

        private string RenderLine(MachineInstruction instr)
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