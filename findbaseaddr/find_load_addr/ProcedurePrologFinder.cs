using Reko.Core;
using Reko.Core.Lib;
using Reko.Core.Memory;
using System;
using System.Collections.Generic;

namespace FindLoadAddr
{
    public class ProcedurePrologFinder : IBaseAddressFinder
    {
        private readonly ByteMemoryArea mem;
        private readonly ByteTrie<object> trie;

        public ProcedurePrologFinder(ByteMemoryArea mem)
        {
            this.mem = mem;
            this.trie = new ByteTrie<object>();
            trie.Add(new byte[] { 0x55, 0x89, 0xE5, 0x83 }, 4);
            trie.Add(new byte[] { 0x55, 0x89, 0xE5 }, 3);
            //this.trie = BuildTrie(patterns, arch);
        }

        public EndianServices Endianness { get; set; }

        public void Run()
        {
            var prologs = PatternFinder.FindProcedurePrologs(mem, trie);
        }

        private static ByteTrie<object> BuildTrie(IEnumerable<MaskedPattern> patterns, IProcessorArchitecture arch)
        {
            int unitsPerInstr = arch.InstructionBitSize / arch.InstructionBitSize;
            if (arch.Endianness == EndianServices.Big)
            {
                throw new NotImplementedException();
            }
            else
            {
                var trie = new ByteTrie<object>();
                foreach (var pattern in patterns)
                {
                    trie.Add(pattern.Bytes, pattern.Mask, new Object());
                }
                return trie;
            }
        }

        public List<(Address, int)> FindPrologs(MemoryArea mem)
        {
            throw new NotImplementedException();
        }

    }
}
