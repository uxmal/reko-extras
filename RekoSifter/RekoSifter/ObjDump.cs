using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using libopcodes;
using Reko.Arch.X86;
using Reko.Core;
using Reko.Core.Machine;
using Constant = Reko.Core.Expressions.Constant;

namespace RekoSifter
{

    /// <summary>
    /// This class uses the runtime library used by objdump to disassemble instructions.
    /// </summary>
	public unsafe class ObjDump
    {

        [DllImport("msvcrt.dll", CharSet = CharSet.Ansi, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
        public static extern int vsprintf(StringBuilder buffer,string format,IntPtr args);

        [DllImport("msvcrt.dll", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
        public static extern int _vscprintf(string format,IntPtr ptr);

        private readonly BfdArchInfo arch;
        private BfdMachine mach;

        private StringBuilder buf;

        private readonly string archNameParam;

        private IEnumerable<string> libraries;

        private Dictionary<string, BfdMachine> defaultMachine = new Dictionary<string, BfdMachine>() {
            { "i386", BfdMachine.i386_i386 | BfdMachine.i386_i386_intel_syntax }
        };

        private IntPtr ImportResolver(string libraryName, Assembly asm, DllImportSearchPath? searchPath) {
            switch (libraryName) {
                case "bfd":
                case "opcodes":
                    // find the proper opcodes-* library
                    string libName = libraries.Where(l => l.Contains($"-{archNameParam}-")).First();
                    return NativeLibrary.Load(libName);
            }
            return IntPtr.Zero;
        }

        private void SetResolver() {
            libraries = Directory.GetFiles(".", "opcodes-*.dll");
            NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), ImportResolver);
        }

        public ObjDump(string arch) {
            this.archNameParam = arch;

            SetResolver();
            BfdArchInfo ai = Bfd.BfdScanArch(arch);
            if(ai == null) {
                throw new NotSupportedException($"This build of binutils doesn't support architecture '{arch}'");
            }

            if (defaultMachine.ContainsKey(ai.ArchName)) {
                this.mach = defaultMachine[ai.ArchName];
            }

            this.arch = ai;
        }

        public int fprintf(IntPtr h, string fmt, IntPtr args) {
            GCHandle argsH = GCHandle.Alloc(args, GCHandleType.Pinned);
            IntPtr pArgs = argsH.AddrOfPinnedObject();
            
            var sb = new StringBuilder(_vscprintf(fmt, pArgs) + 1);
            vsprintf(sb, fmt, pArgs);

            argsH.Free();

            var formattedMessage = sb.ToString();
            buf.Append(formattedMessage);
            return 0;
        }

        private int BufferReadMemory(ulong memaddr, byte* myaddr, uint length, IntPtr dinfo) {
            DisassembleInfo di = new DisassembleInfo(dinfo.ToPointer());
            return dis_asm.BufferReadMemory(memaddr, myaddr, length, di);
        }

        public string Disassemble(byte[] bytes) {
            buf = new StringBuilder();
            StreamState ss = new StreamState();

            IntPtr ssPtr = Marshal.AllocHGlobal(Marshal.SizeOf<StreamState>());
            Marshal.StructureToPtr(ss, ssPtr, false);

            var disasm_info = new libopcodes.DisassembleInfo();
            dis_asm.InitDisassembleInfo(disasm_info, ssPtr, fprintf);

            fixed(byte* dptr = bytes) {
                disasm_info.Arch = arch.Arch;
                disasm_info.Mach = (uint)this.mach;
                disasm_info.ReadMemoryFunc = BufferReadMemory;
                disasm_info.Buffer = dptr;
                disasm_info.BufferVma = 0;
                disasm_info.BufferLength = (ulong)bytes.Length;

                dis_asm.DisassembleInitForTarget(disasm_info);

                DisassemblerFtype disasm = dis_asm.Disassembler(arch.Arch, 0, (uint)this.mach, null);
                if(disasm == null) {
                    string archName = Enum.GetName(typeof(BfdArchitecture), arch);
                    throw new NotSupportedException($"This build of binutils doesn't support architecture '{archName}'");
                }

                ulong pc = 0;
                while(pc < (ulong)bytes.Length) {
                    int insn_size = disasm(pc, disasm_info.__Instance);
                    pc += (ulong)insn_size;
                    
                    buf.AppendLine();

                    break; //only first instruction
                }
            }

            Marshal.FreeHGlobal(ssPtr);

            return buf.ToString();
        }

        //$TODO: make general; currently hard-wired to x86.
        /// <summary>
        /// Render a Reko <see cref="MachineInstruction"/> so that it looks like 
        /// the output of objdump.
        /// </summary>
        /// <param name="i">Reko machine instruction to render</param>
        /// <returns>A string containing the rendering of the instruction.</returns>
        public string RenderAsObjdump(MachineInstruction i)
        {
            var sb = new StringBuilder();
            var instr = (X86Instruction)i;
            sb.AppendFormat("{0,-6}", instr.Mnemonic.ToString());
            var sep = " ";
            foreach (var op in instr.Operands)
            {
                sb.Append(sep);
                sep = ",";
                switch (op)
                {
                    case RegisterOperand rop:
                        sb.Append(rop);
                        break;
                    case ImmediateOperand imm:
                        RenderObjdumpConstant(imm.Value, false, sb);
                        break;
                    case MemoryOperand mem:
                        RenderObjdumpMemoryOperand(mem, sb);
                        break;
                    case AddressOperand addr:
                        sb.AppendFormat("0x{0}", addr.Address.ToString().ToLower());
                        break;
                    default:
                        sb.AppendFormat("[{0}]", op.GetType().Name);
                        break;
                }
            }
            return sb.ToString();
        }

        private void RenderObjdumpConstant(Constant c, bool renderPlusSign, StringBuilder sb)
        {
            long offset;
            if (renderPlusSign)
            {
                offset = c.ToInt32();
                if (offset < 0)
                {
                    sb.Append("-");
                    offset = -c.ToInt64();
                }
                else
                {
                    sb.Append("+");
                    offset = c.ToInt64();
                }
            }
            else
            {
                offset = (long)c.ToUInt32();
            }

            string fmt = c.DataType.Size switch
            {
                1 => "0x{0:x}",
                2 => "0x{0:x}",
                4 => "0x{0:x}",
                _ => "@@@[{0:x}:w{1}]",
            };
            sb.AppendFormat(fmt, offset, c.DataType.BitSize);
        }

        private void RenderObjdumpMemoryOperand(MemoryOperand mem, StringBuilder sb)
        {
            switch (mem.Width.Size)
            {
                case 1: sb.Append("BYTE PTR"); break;
                case 2: sb.Append("WORD PTR"); break;
                case 4: sb.Append("DWORD PTR"); break;
                case 8: sb.Append("QWORD PTR"); break;
                default: sb.AppendFormat("[SIZE {0} PTR]", mem.Width.Size); break;
            }
            sb.AppendFormat(" {0}[", mem.SegOverride != null && mem.SegOverride != RegisterStorage.None
                ? $"{mem.SegOverride}:"
                : "");
            if (mem.Base != null && mem.Base != RegisterStorage.None)
            {
                sb.Append(mem.Base.Name);
                if (mem.Index != null && mem.Index != RegisterStorage.None)
                {
                    sb.Append("+");
                    sb.Append(mem.Index.Name);
                    if (mem.Scale > 1)
                    {
                        sb.AppendFormat("*{0}", mem.Scale);
                    }
                }
                if (mem.Offset != null && mem.Offset.IsValid)
                {
                    RenderObjdumpConstant(mem.Offset, true, sb);
                }
            }
            else
            {
                sb.Append(mem.Offset);
            }
            sb.Append("]");
        }

    }
}