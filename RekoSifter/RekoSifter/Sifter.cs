using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Reko.Arch.Mips;
using Reko.Arch.X86;
using Reko.Core;
using Reko.Core.Configuration;
using Reko.Core.Machine;

namespace RekoSifter
{
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

        public Sifter(string[] args)
        {
            this.cfgSvc = Reko.Core.Configuration.RekoConfigurationService.Load("reko/reko.config");
            ProcessArgs(args);
            this.mem = new MemoryArea(Address.Ptr32(0x00100000), new byte[100]);
            this.rdr = arch.CreateImageReader(mem, 0);
            this.dasm = arch.CreateDisassembler(rdr);
        }

        private void ProcessArgs(string[] args)
        {
            var archName = DefaultArchName;
            var maxLength = DefaultMaxInstrLength;

            for (int i = 0; i < args.Length; ++i)
            {
                switch (args[i])
                {
                case "-a":
                case "--arch":
                    ++i;
                    if (i < args.Length)
                    {
                        archName = args[i];
                    }
                    else
                    {
                        Usage();
                        Environment.Exit(-1);
                    }
                    break;
                case "--maxlen":
                    ++i;
                    if (i >= args.Length || !int.TryParse(args[i], out maxLength))
                    {
                        Usage();
                        Environment.Exit(-1);
                    }
                    break;
                case "-r":
                case "--random":
                    this.useRandomBytes = true;
                    if (i >= args.Length - 1)
                        break; 
                    ++i;
                    if (args[i] == "-")
                        break;
                    if (!Int32.TryParse(args[i], out int seed))
                    {
                        Console.Error.WriteLine("Invalid seed value '{0}'.", args[i]);
                    }
                    this.seed = seed;
                    break;
                case "-h":
                case "--help":
                    Usage();
                    break;
                }
            }
            this.arch = cfgSvc.GetArchitecture(archName);
            this.maxInstrLength = maxLength;
        }

        private void Usage()
        {
            Console.WriteLine("RekoSifter test tool");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  RekoSifter -a=<name> | --arch=<name>");
            Console.WriteLine("Options:");
            Console.WriteLine("  -a --arch <name>       Use processor architecture <name>");
            Console.WriteLine("  --maxlen <length>      Maximum instruction length");
            Console.WriteLine("  -r --random [seed|-]   Generate random byte sequences (using");
            Console.WriteLine("                         optional seed.");
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

        public void Sift_Random(Random rng)
        {
            var buf = new byte[maxInstrLength];
            for (;;)
            {
                rng.NextBytes(buf);
                Buffer.BlockCopy(buf, 0, mem.Bytes, 0, buf.Length);
                var instr = Dasm();
                RenderLine(instr);
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
                RenderLine(instr);
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
                var instr = Dasm();
                RenderLine(instr);
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
            for (; ;)
            {
                var instr = Dasm();
                RenderLine(instr);
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

        private void RenderLine(MachineInstruction instr)
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
            Console.WriteLine(sb.ToString());
        }
    }
}