using System;
using System.Runtime.InteropServices;
using System.Text;
using libopcodes;

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

        private BfdArchitecture arch;
        private BfdMachine machine;
        private StringBuilder buf;

        public ObjDump(BfdArchitecture arch, BfdMachine machine) {
            this.arch = arch;
            this.machine = machine;
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
                disasm_info.Arch = this.arch;
                disasm_info.Mach = (uint)this.machine;
                disasm_info.ReadMemoryFunc = BufferReadMemory;
                disasm_info.Buffer = dptr;
                disasm_info.BufferVma = 0;
                disasm_info.BufferLength = (ulong)bytes.Length;

                dis_asm.DisassembleInitForTarget(disasm_info);

                DisassemblerFtype disasm = dis_asm.Disassembler(this.arch, 0, (uint)this.machine, null);
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
    }
}