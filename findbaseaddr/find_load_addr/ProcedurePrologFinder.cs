using Reko.Core;
using Reko.Core.Lib;
using Reko.Core.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FindLoadAddr
{
    internal class ProcedurePrologFinder
    {
        private readonly ByteTrie<object> trie;

        public ProcedurePrologFinder(MaskedPattern [] patterns, IProcessorArchitecture arch)
        {
            this.trie = BuildTrie(patterns, arch);
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
