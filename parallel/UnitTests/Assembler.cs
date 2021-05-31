using Reko.Core;
using Reko.Core.Memory;
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
        private readonly Dictionary<string, Symbol> symbols;

        public Assembler(Address addr)
        {
            this.addr = addr;
            this.bytes = new();
            this.symbols = new();
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
            return new ByteMemoryArea(addr, bytes.ToArray());
        }

        internal void Ret()
        {
            Emit(0x60);
        }

        public void Mov()
        {
            Emit(0x10);
        }

        public void Call(string label)
        {
            Emit(0x30);
            EmitShort(ReferToSymbol(label));
        }

        public void Jmp(int uAddr)
        {
            Emit(0x20);
            EmitShort(uAddr);
        }

        public void Jmp(string label)
        {
            Emit(0x20);
            EmitShort(ReferToSymbol(label));
        }

        public void Branch(int condition, int uAddr)
        {
            Emit(0x20 | condition);
            EmitShort(uAddr);
        }

        public void Branch(int condition, string label)
        {
            Emit(0x20 | condition);
            EmitShort(ReferToSymbol(label));
        }

        private int ReferToSymbol(string label)
        {
            if (!symbols.TryGetValue(label, out var symbol))
            {
                symbol = new Symbol { Name = label };
                symbols.Add(label, symbol);
            }
            if (symbol.Offset.HasValue)
            {
                return symbol.Offset.Value;
            }
            else
            {
                symbol.Patches.Add(bytes.Count);
            }
            return 0;
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

        public void Label(string label)
        {
            var value = bytes.Count;
            if (!symbols.TryGetValue(label, out var symbol))
            {
                symbols.Add(label, new Symbol { Offset = value, Name = label });
            }
            else
            {
                if (!symbol.Offset.HasValue)
                {
                    symbol.Offset = value;
                    foreach (var p in symbol.Patches)
                    {
                        bytes[p] = (byte)(value >> 8);
                        bytes[p + 1] = (byte)value;
                    }
                    symbol.Patches.Clear();
                }
            }
        }

        private class Symbol
        {
            public string Name;
            public int? Offset;
            public List<int> Patches = new();
        }

    }
}
