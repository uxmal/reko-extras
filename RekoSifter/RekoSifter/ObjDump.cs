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
        private BfdMachine mach = 0;

        private StringBuilder buf;

        private readonly IntPtr hLib;

        private Dictionary<string, BfdMachine> defaultMachine = new Dictionary<string, BfdMachine>() {
            { "i386", BfdMachine.i386_i386 | BfdMachine.i386_i386_intel_syntax }
        };

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
                throw new NotSupportedException($"No opcode library found for architecture '{arch}'");
            }

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
            if (formattedMessage == "(null)") {
                formattedMessage = "\t";
            }
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
    }
}