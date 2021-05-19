using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelScan.UnitTests
{
    public class Assembler
    {
        private readonly Address addr;
        private readonly List<byte> bytes;
        private int iPos;

        public Assembler(Address addr)
        {
            this.addr = addr;
            this.bytes = new();
        }

        public void Org(int addr)
        {
            if (bytes.Count < addr)
            {
                bytes.Add(0);
            }
            iPos = addr;
        }

        public MemoryArea Complete()
        {
            return new MemoryArea(addr, bytes.ToArray());
        }

        internal void Ret()
        {
            Emit(0x60);
        }

        public void Mov()
        {
            Emit(0x10);
        }

        public void Jmp(int uAddr)
        {
            Emit(0x20);
            EmitShort(uAddr);
        }

        public void Branch(int condition, int uAddr)
        {
            Emit(0x20 | condition);
            EmitShort(uAddr);
        }

        private void Emit(int b)
        {
            bytes.Add((byte)b);
        }

        private void EmitShort(int u)
        {
            bytes.Add((byte)(u >> 8));
            bytes.Add((byte)u);
        }
    }
}
