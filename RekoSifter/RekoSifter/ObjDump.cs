using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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

        private StringBuilder buf;

        private readonly IntPtr hLib;

        private delegate IntPtr BfdScanArchDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string @string);
        private delegate IntPtr BfdArchListDelegate();

        private Dictionary<string, List<string>> libToArches = new Dictionary<string, List<string>>();

        private void PrintAvailableArchitectures() {
            foreach(var pair in libToArches) {
                Console.WriteLine($"[{pair.Key}]");
                foreach(string arch in pair.Value) {
                    Console.WriteLine($" -- {arch}");
                }
                Console.WriteLine();
            }
        }

        private unsafe IntPtr FindArchitectureLibrary(string architecture) {
            var libraries = Directory.GetFiles(".", "opcodes-*.dll");
            foreach (string libName in libraries) {
                List<string> archList = new List<string>();

                IntPtr hLib = NativeLibrary.Load(libName);

                IntPtr bfd_scan_arch = NativeLibrary.GetExport(hLib, "bfd_scan_arch");
                IntPtr bfd_arch_list = NativeLibrary.GetExport(hLib, "bfd_arch_list");

                BfdScanArchDelegate scanArchFn = Marshal.GetDelegateForFunctionPointer<BfdScanArchDelegate>(bfd_scan_arch);
                BfdArchListDelegate archListFn = Marshal.GetDelegateForFunctionPointer<BfdArchListDelegate>(bfd_arch_list);

                sbyte **archListPtr = (sbyte **)archListFn();
                if (archListPtr != null) {
                    for (sbyte** sptr = archListPtr; *sptr != null; sptr++) {
                        IntPtr strPtr = new IntPtr(*sptr);
                        string arch = Marshal.PtrToStringAnsi(strPtr);
                        archList.Add(arch);
                    }
                }

                // populate local list
                // ($TODO: should be done in another function to make this function stateless)
                libToArches[libName] = archList;

                IntPtr res = scanArchFn(architecture);
                if (res != IntPtr.Zero) {
                    return hLib;
                }
                NativeLibrary.Free(hLib);
            }
            return IntPtr.Zero;
        }

        private IntPtr ImportResolver(string libraryName, Assembly asm, DllImportSearchPath? searchPath) {
            switch (libraryName) {
                case "bfd":
                case "opcodes":
                    return hLib;
            }
            return IntPtr.Zero;
        }

        private void SetResolver() {
            NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), ImportResolver);
        }

        public ObjDump(string arch) {
            hLib = FindArchitectureLibrary(arch);
            if(hLib == IntPtr.Zero) {
                PrintAvailableArchitectures();
                throw new NotSupportedException($"No opcode library found for architecture '{arch}'.");
            }

            SetResolver();
            BfdArchInfo ai = Bfd.BfdScanArch(arch);
            if(ai == null) {
                throw new NotSupportedException($"This build of binutils doesn't support architecture '{arch}'.");
            }

            this.arch = ai;
        }

        public int fprintf(IntPtr h, string fmt, IntPtr args) {
            GCHandle argsH = GCHandle.Alloc(args, GCHandleType.Pinned);
            IntPtr pArgs = argsH.AddrOfPinnedObject();
            
            var sb = new StringBuilder(_vscprintf(fmt, pArgs) + 1);
            vsprintf(sb, fmt, pArgs);

            argsH.Free();

            var formattedMessage = sb.ToString().Replace("(null)", "\t");
            buf.Append(formattedMessage);
            return 0;
        }

        private int BufferReadMemory(ulong memaddr, byte* myaddr, uint length, IntPtr dinfo) {
            DisassembleInfo di = new DisassembleInfo(dinfo.ToPointer());
            return dis_asm.BufferReadMemory(memaddr, myaddr, length, di);
        }

        public (string, byte[]) Disassemble(byte[] bytes) {
            buf = new StringBuilder();

            var disasm_info = new DisassembleInfo();
            dis_asm.InitDisassembleInfo(disasm_info, IntPtr.Zero, fprintf);

            GCHandle hBytes = GCHandle.Alloc(bytes, GCHandleType.Pinned);

            disasm_info.Arch = arch.Arch;
            disasm_info.Mach = arch.Mach;
            disasm_info.ReadMemoryFunc = BufferReadMemory;
            disasm_info.Buffer = (byte *)hBytes.AddrOfPinnedObject();
            disasm_info.BufferVma = 0;
            disasm_info.BufferLength = (ulong)bytes.Length;

            dis_asm.DisassembleInitForTarget(disasm_info);

            DisassemblerFtype disasm = dis_asm.Disassembler(arch.Arch, 0, arch.Mach, null);
            if(disasm == null) {
                string archName = Enum.GetName(typeof(BfdArchitecture), arch);
                throw new NotSupportedException($"This build of binutils doesn't support architecture '{archName}'");
            }

            byte[] ibytes = null;

            ulong pc = 0;
            while(pc < (ulong)bytes.Length) {
                int insn_size = disasm(pc, disasm_info.__Instance);

                ibytes = new byte[insn_size];
                Buffer.BlockCopy(bytes, (int)pc, ibytes, 0, insn_size);

                pc += (ulong)insn_size;

                break; //only first instruction
            }

            hBytes.Free();

            return (buf.ToString(), ibytes);
        }
    }
}