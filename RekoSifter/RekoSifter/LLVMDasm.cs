using libopcodes;
using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace RekoSifter
{
	[Flags]
	enum LLVMDisassemblerOption : uint
	{
		UseMarkup = 1,
		PrintImmHex = 2,
		/// <summary>
		/// use the other assembler printer variant
		/// </summary>
		AsmPrinterVariant = 4,
		SetInstrComments = 8,
		PrintLatency = 16
	}

	public class LLVMDasm : IDisassembler
	{
		private LLVMDisasmContextRef hDasm;

        private ulong programCounter = 0;

        private readonly BfdArchitecture bfdArchitecture;

        private static BfdArchitecture GetBfdArchitecture(string triple)
        {
            if (triple.StartsWith("x86_64"))
            {
                return BfdArchitecture.BfdArchI386;
            }
            if (triple.StartsWith("arm"))
            {
                return BfdArchitecture.BfdArchArm;
            }

            return BfdArchitecture.BfdArchUnknown;
        }

		public LLVMDasm(string triple) {
			hDasm = Initialize(triple);

   
		}

		private static unsafe LLVMDisasmContextRef Initialize(string triple) {
			NativeLibrary.Load(@"C:\msys64\mingw64\bin\libLLVM");

			LLVM.InitializeAllTargetMCs();
			LLVM.InitializeAllTargets();
			LLVM.InitializeAllTargetInfos();
			LLVM.InitializeAllAsmParsers();
			LLVM.InitializeAllAsmPrinters();
			LLVM.InitializeAllDisassemblers();

			byte[] tripleBytes = Encoding.ASCII.GetBytes(triple);

			LLVMDisasmContextRef hDasm;
			using (DisposableGCHandle hTriple = DisposableGCHandle.Pin(tripleBytes)) {
				hDasm = new LLVMDisasmContextRef(
					new IntPtr(LLVM.CreateDisasm(
						(sbyte*)hTriple.AddrOfPinnedObject(),
						null, 0, null, null
					))
				);
			}

			if(hDasm == null) {
				throw new Exception("CreateDisasm failed");
			}

			LLVM.SetDisasmOptions(
				hDasm.Handle.ToPointer(),
				// use alternate variant (Intel) with hex immediates
				(ulong)(LLVMDisassemblerOption.PrintImmHex | LLVMDisassemblerOption.AsmPrinterVariant)
			);

			return hDasm;
		}

		public unsafe (string, byte[]) Disassemble(byte[] instr) {		

			sbyte[] buf = new sbyte[80];
			uint instrSize;
			string? disassembled;

			using (DisposableGCHandle hBytes = DisposableGCHandle.Pin(instr))
			using (DisposableGCHandle hBuf = DisposableGCHandle.Pin(buf)) {
				instrSize = (uint) LLVM.DisasmInstruction(
					hDasm.Handle.ToPointer(),
					(byte*)hBytes.AddrOfPinnedObject(),
					(ulong)instr.Length,
					this.programCounter,
					(sbyte*)hBuf.AddrOfPinnedObject(),
					new UIntPtr((uint)buf.Length)
				);

				disassembled = Marshal.PtrToStringAnsi(hBuf.AddrOfPinnedObject());
			}

			byte[] ibytes = new byte[instrSize];
			Array.Copy(instr, 0, ibytes, 0, instrSize);
            programCounter += instrSize;

			disassembled = (disassembled ?? "").TrimStart(new[] { ' ', '\t' });
			return (disassembled, ibytes);
		}

		public bool IsInvalidInstruction(string sInstr)
		{
			return false;
		}

		public void SetEndianness(char endianness)
        {
			Console.Error.WriteLine("Endianness control for LLVM is not supported yet.");
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

        public BfdArchitecture GetArchitecture()
        {
            return this.bfdArchitecture;
        }
    }
}
