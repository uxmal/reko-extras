using System;
using System.Collections.Generic;
using System.Diagnostics;
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
	public class ObjDump : IDisassembler, IDisposable
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

        private readonly FprintfFtype fprintfDelegate;
        private readonly libopcodes.Delegates.Func_int_ulong_bytePtr_uint_IntPtr bufferReadMemoryDelegate;

        private readonly DisassembleInfo dasmInfo;
        private readonly DisassemblerFtype dasm;
        private BfdEndian endianness;

        private ulong programCounter = 0;

        private void PrintAvailableArchitectures() {
            foreach(var pair in libToArches) {
                Console.WriteLine($"[{pair.Key}]");
                foreach(string arch in pair.Value) {
                    Console.WriteLine($" -- {arch}");
                    Debug.Print($" -- {arch}");
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
                        string? arch = Marshal.PtrToStringAnsi(strPtr);
                        archList.Add(arch ?? "(null)");
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
            try
            {
                NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), ImportResolver);
            }
            catch (InvalidOperationException)
            {
                // resolver already set
            }
        }

        public unsafe ObjDump(string arch) {
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
            this.buf = new StringBuilder();
            this.arch = ai;

            fprintfDelegate = new FprintfFtype(fprintf);
            bufferReadMemoryDelegate = new libopcodes.Delegates.Func_int_ulong_bytePtr_uint_IntPtr(BufferReadMemory);

            (dasmInfo, dasm) = InitDisassembler();
        }

        private int fprintf(IntPtr h, string fmt, IntPtr args) {
            StringBuilder sb;
            
            using (DisposableGCHandle argsH = DisposableGCHandle.Pin(args)) {
            IntPtr pArgs = argsH.AddrOfPinnedObject();
                sb = new StringBuilder(_vscprintf(fmt, pArgs) + 1);
            vsprintf(sb, fmt, pArgs);
            }

            var formattedMessage = sb.ToString().Replace("(null)", "\t");
            buf.Append(formattedMessage);
            return 0;
        }

        private unsafe int BufferReadMemory(ulong memaddr, byte* myaddr, uint length, IntPtr dinfo) {
            DisassembleInfo di = new DisassembleInfo(dinfo.ToPointer());
            return dis_asm.BufferReadMemory(memaddr, myaddr, length, di);
        }

        private unsafe (DisassembleInfo, DisassemblerFtype) InitDisassembler()
        {
            DisassembleInfo info = new DisassembleInfo();
            dis_asm.InitDisassembleInfo(info, IntPtr.Zero, fprintfDelegate);

            info.Endian = endianness;
            info.Arch = arch.Arch;
            info.Mach = arch.Mach;
            info.ReadMemoryFunc = bufferReadMemoryDelegate;
            info.Buffer = null;
            info.BufferVma = 0;
            info.BufferLength = 0;
            
            dis_asm.DisassembleInitForTarget(info);

            // create disassembler, returns a function pointer
            DisassemblerFtype disasm = dis_asm.Disassembler(arch.Arch, 0, arch.Mach, null);
            if (disasm == null) {
                string? archName = Enum.GetName(typeof(BfdArchitecture), arch.Arch);
                throw new NotSupportedException($"This build of binutils doesn't support architecture '{archName}'");
            }

            return (info, disasm);
        }

        public unsafe (string, byte[]?) Disassemble(byte[] bytes)
        {
            buf.Clear();

            byte[]? ibytes = null;
            ulong offset = 0;

            using (DisposableGCHandle hBytes = DisposableGCHandle.Pin(bytes))
            {
                dasmInfo.Buffer = (byte *)hBytes.AddrOfPinnedObject();
                dasmInfo.BufferLength = (ulong)bytes.Length;
                dasmInfo.BufferVma = programCounter;

                while (offset < (ulong)bytes.Length)
                {
                    int insn_size = dasm(programCounter, dasmInfo.__Instance);

                    ibytes = new byte[insn_size];
                    Array.Copy(bytes, (long)offset, ibytes, 0, insn_size);

                    programCounter += (ulong)insn_size;
                    offset += (ulong) insn_size;
                    break; //only first instruction
                }
            }

            string sInstr = SanitizeObjdumpOutput();
            return (sInstr, ibytes);
        }

        private string SanitizeObjdumpOutput()
        {
            var sInstr = buf.ToString();
            if (arch.Arch == BfdArchitecture.BfdArchI386)
            {
                // Trim # only on architectures where # is not used 
                // in the actual disassembled code.
                int iHash = sInstr.IndexOf('#');
                if (iHash >= 0)
                {
                    return sInstr.Remove(iHash);
                }
            }
            return sInstr;
        }

        public bool IsInvalidInstruction(string sInstr)
        {
            if (string.IsNullOrEmpty(sInstr))
                return true;
            if (sInstr.Contains("(bad)"))
                return true;
            if (sInstr.StartsWith("0x"))
                return true;
            if (sInstr.StartsWith(".short"))
                return true;
            if (sInstr.StartsWith(".inst"))
                return true;
            return false;
        }

        public void Dispose() {
            dasmInfo.Dispose();
        }

        public void SetEndianness(char endianness)
        {
            if (endianness == 'b')
                this.endianness = BfdEndian.BFD_ENDIAN_BIG;
            else if (endianness == 'l')
                this.endianness = BfdEndian.BFD_ENDIAN_LITTLE;
            else
                this.endianness = BfdEndian.BFD_ENDIAN_UNKNOWN;
        }

        public bool SetProgramCounter(ulong address)
        {
            this.programCounter = address;
            return true;
        }

        public ulong GetProgramCounter()
        {
            return this.programCounter;
        }
    }
}