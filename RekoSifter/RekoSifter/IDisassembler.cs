using System;
using System.Collections.Generic;
using System.Text;

namespace RekoSifter
{
	public interface IDisassembler
	{
		(string, byte[]?) Disassemble(byte[] instruction);

		/// <summary>
		/// Select endianness before disassembling
		/// </summary>
		/// <param name="c">'b' for big, 'l' for little; any other value is 'default'.</param>
		void SetEndianness(char c);

		bool IsInvalidInstruction(string sInstr);
        bool SetProgramCounter(ulong address);
        ulong GetProgramCounter();

        // we use the BFD enum as it has them all
        libopcodes.BfdArchitecture GetArchitecture();
	}
}
