using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelScan.UnitTests
{
    public class TestArchitecture : IProcessorArchitecture
    {
        public IEnumerable<MachineInstruction> CreateDisassembler(ImageReader rdr)
        {
            return new TestDisassembler(rdr);
        }

        public ImageReader CreateImageReader(MemoryArea mem, Address addr)
        {
            return new ImageReader(mem, addr - mem.Address);
        }
    }
}
