using Reko.Core;
using Reko.Core.Memory;

namespace chunks
{
    public class WorkUnit
    {

        public WorkUnit(IProcessorArchitecture arch, MemoryArea mem, Address addr, int length)
        {
            this.Architecture = arch;
            this.MemoryArea = mem;
            this.Address = addr;
            this.Length = length;
        }

        public IProcessorArchitecture Architecture { get; }
        public MemoryArea MemoryArea { get; }
        public Address Address { get; }
        public int Length { get; }
    }
}