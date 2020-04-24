using System;
using System.Runtime.InteropServices;
using System.Text;
using libopcodes;

namespace RekoSifter
{
	public unsafe class ObjDump
    {

        [DllImport("msvcrt.dll", CharSet = CharSet.Ansi, CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
        public static extern int vsprintf(StringBuilder buffer,string format,IntPtr args);

        [DllImport("msvcrt.dll", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
        public static extern int _vscprintf(string format,IntPtr ptr);

        private StringBuilder buf;

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

        private int BufferReadMemory(ulong memaddr, byte* myaddr, uint length, global::System.IntPtr dinfo) {
            DisassembleInfo di = new DisassembleInfo(dinfo.ToPointer());
            return dis_asm.BufferReadMemory(memaddr, myaddr, length, di);
        }

        public string Disassemble(byte[] bytes) {
            buf = new StringBuilder();
            StreamState ss = new StreamState();

            IntPtr ssPtr = Marshal.AllocHGlobal(Marshal.SizeOf<StreamState>());
            Marshal.StructureToPtr(ss, ssPtr, false);

            libopcodes.DisassembleInfo disasm_info = new libopcodes.DisassembleInfo();
            dis_asm.InitDisassembleInfo(disasm_info, ssPtr, fprintf);


            uint x86_64 = 1 << 3;
            uint intel_syntax = 1 << 0;

            fixed(byte* dptr = bytes) {
                disasm_info.Arch = BfdArchitecture.BfdArchI386;
                disasm_info.Mach = x86_64 | intel_syntax;
                disasm_info.ReadMemoryFunc = BufferReadMemory;
                disasm_info.Buffer = dptr;
                disasm_info.BufferVma = 0;
                disasm_info.BufferLength = (ulong)bytes.Length;

                dis_asm.DisassembleInitForTarget(disasm_info);

                DisassemblerFtype disasm = dis_asm.Disassembler(BfdArchitecture.BfdArchI386, 0, x86_64, null);

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

        public ObjDump() {    
        }
    }
}