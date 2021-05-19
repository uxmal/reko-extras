namespace ParallelScan
{
    public class ImageReader
    {
        private readonly MemoryArea mem;

        public ImageReader(MemoryArea mem, long offset)
        {
            this.mem = mem;
            this.Offset = offset;
        }


        public long Offset { get; set; }
        
        public Address Address => mem.Address + this.Offset;

        public bool TryReadByte(out byte b)
        {
            if (Offset < mem.Bytes.Length)
            {
                b = mem.Bytes[Offset];
                ++Offset;
                return true;
            }
            b = default;
            return false;
        }

        public bool TryReadBeUInt16(out ushort us)
        {
            if (Offset < mem.Bytes.Length - 1)
            {
                us = (ushort)
                   ((mem.Bytes[Offset] << 8) |
                    mem.Bytes[Offset + 1]);
                Offset += 2;
                return true;
            }
            us = default;
            Offset = mem.Bytes.Length;
            return false;   
        }
    }
}