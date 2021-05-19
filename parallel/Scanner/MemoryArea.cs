namespace ParallelScan
{
    public class MemoryArea
    {
        public MemoryArea(Address addr, byte[] bytes)
        {
            this.Address = addr;
            this.Bytes = bytes;
        }

        public Address Address { get; }
        public byte[] Bytes { get; }
    }
}