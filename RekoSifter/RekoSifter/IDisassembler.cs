using System;
using System.Collections.Generic;
using System.Text;

namespace RekoSifter
{
	public interface IDisassembler
	{
		(string, byte[]) Disassemble(byte[] instruction);
	}
}
