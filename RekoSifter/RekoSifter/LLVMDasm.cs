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

	public class LLVMDasm
	{
		private LLVMDisasmContextRef hDasm;

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
			GCHandle hTriple = GCHandle.Alloc(tripleBytes, GCHandleType.Pinned);

			LLVMDisasmContextRef hDasm = new LLVMDisasmContextRef(
				new IntPtr(LLVM.CreateDisasm(
					(sbyte *)hTriple.AddrOfPinnedObject(),
					null, 0, IntPtr.Zero, IntPtr.Zero
				))
			);

			if(hDasm == null) {
				throw new Exception("CreateDisasm failed");
			}

			hTriple.Free();

			LLVM.SetDisasmOptions(
				hDasm.Handle.ToPointer(),
				// use alternate variant (Intel) with hex immediates
				(ulong)(LLVMDisassemblerOption.PrintImmHex | LLVMDisassemblerOption.AsmPrinterVariant)
			);

			return hDasm;
		}

		public unsafe (string, byte[]) Disassemble(byte[] instr) {		
			GCHandle hBytes = GCHandle.Alloc(instr, GCHandleType.Pinned);

			sbyte[] buf = new sbyte[80];

			GCHandle hBuf = GCHandle.Alloc(buf, GCHandleType.Pinned);

			UIntPtr instrSize = LLVM.DisasmInstruction(
				hDasm.Handle.ToPointer(),
				(byte*)hBytes.AddrOfPinnedObject(),
				(ulong)instr.Length,
				0,
				(sbyte*)hBuf.AddrOfPinnedObject(),
				new UIntPtr((uint)buf.Length)
			);

			byte[] ibytes = new byte[instrSize.ToUInt32()];
			Array.Copy(instr, 0, ibytes, 0, instrSize.ToUInt32());

			string disassembled = Marshal.PtrToStringAnsi(hBuf.AddrOfPinnedObject());

			hBytes.Free();
			hBuf.Free();

			disassembled = disassembled.TrimStart(new[] { ' ', '\t' });
			return (disassembled, ibytes);
		}
	}
}
