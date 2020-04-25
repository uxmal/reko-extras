using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using libopcodes;
using Reko.Arch.Mips;
using Reko.Arch.X86;
using Reko.Core;
using Reko.Core.Configuration;
using Reko.Core.Machine;

namespace RekoSifter
{
    public struct ParseResult
    {
        public string hex;
        public string asm;
    }

    public struct StreamState
    {
    }

    public class Sifter
    {
        private const string DefaultArchName = "x86-protected-32";
        private const int DefaultMaxInstrLength = 15;

        private MemoryArea mem;
        private IProcessorArchitecture arch;
        private int maxInstrLength;
        private EndianImageReader rdr;
        private IEnumerable<MachineInstruction> dasm;
        private RekoConfigurationService cfgSvc;
        private int? seed;

        private bool useRandomBytes;
        private string llvmArch = null;
        private Action<byte[], MachineInstruction> processInstr;

        private ObjDump objDump;

        public Sifter(string[] args)
        {
            this.cfgSvc = Reko.Core.Configuration.RekoConfigurationService.Load("reko/reko.config");
            this.processInstr = new Action<byte[], MachineInstruction>(ProcessInstruction);
            ProcessArgs(args);
            this.mem = new MemoryArea(Address.Ptr32(0x00100000), new byte[100]);
            this.rdr = arch.CreateImageReader(mem, 0);
            this.dasm = arch.CreateDisassembler(rdr);
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
                string arg = (string)it.Current;

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
                        objDump = new ObjDump(BfdArchitecture.BfdArchI386, BfdMachine.x86_64 | BfdMachine.i386_intel_syntax);
                        processInstr = this.CompareWithObjdump;
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
            this.maxInstrLength = maxLength;
        }

        private void Usage()
        {
            Console.Write(
@"RekoSifter test tool

Usage:
    RekoSifter -a=<name> | --arch=<name>
Options:
    -a --arch <name>       Use processor architecture <name>
    --maxlen <length>      Maximum instruction length
    -r --random [seed|-]   Generate random byte sequences (using
                            optional seed.
    -l --llvm <llvmarch>   Enable llvm comparison and use arch <llvmarch>
    -o --objdump           Enable Objdump comparison (hardcoded to x64 for now)
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

        static void RenderLLVM(ParseResult obj)
        {
            Console.Write("L:{0,-45}", obj.asm);
            Console.WriteLine(obj.hex + " -- LLVM");
        }

        public void ProcessInstruction(byte[] bytes, MachineInstruction instr)
        {
            RenderLine("", instr);
        }


        public void CompareWithLlvm(byte[] bytes, MachineInstruction instr)
        {
            RenderLine("R:", instr);
            foreach (var obj in LLVM.Disassemble(llvmArch, mem.Bytes))
            {
                RenderLLVM(obj);
                break; // cheaper than Take(1), less GC.
            }
        }

        private void CompareWithObjdump(byte[] bytes, MachineInstruction instr)
        {
            RenderLine("R:", instr);

            string odOut = objDump.Disassemble(bytes);
            Console.WriteLine("O:{0}", odOut);
            if (instr.ToString().Contains("illegal") ^ odOut.Contains("(bad)"))
            {
                Console.WriteLine("*** discrepancy between Reko disassembler and objdump");
                Console.In.ReadLine();
            }
        }

        public void Sift_Random(Random rng)
        {
            var buf = new byte[maxInstrLength];
            for (; ; )
            {
                //Console.WriteLine();
                rng.NextBytes(buf);
                Buffer.BlockCopy(buf, 0, mem.Bytes, 0, buf.Length);
                
                var instr = Dasm();
                processInstr(buf, instr);
            }
        }

        public void Sift_8Bit()
        {
            var stack = mem.Bytes;
            int iLastByte = 0;
            int lastLen = 0;
            while (iLastByte >= 0)
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
            while (iLastByte >= 0)
            {
                if (llvmArch != null)
                {
                    foreach (var obj in LLVM.Disassemble(llvmArch, mem.Bytes))
                    {
                        RenderLLVM(obj);
                    }
                }
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
            for (; ; )
            {
                if (llvmArch != null)
                {
                    foreach (var obj in LLVM.Disassemble(llvmArch, mem.Bytes))
                    {
                        RenderLLVM(obj);
                    }
                }
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

        private void RenderLine(string prefix, MachineInstruction instr)
        {
            var sb = new StringBuilder(prefix);
            var sInstr = instr != null
                ? instr.ToString()
                : "*** ERROR ***";
            sb.AppendFormat("{0,-40}", sInstr);
            var bytes = mem.Bytes;
            for (int i = 0; i < rdr.Offset; ++i)
            {
                sb.AppendFormat(" {0:X2}", (uint)bytes[i]);
            }
            Console.WriteLine(sb.ToString());
        }
    }
}