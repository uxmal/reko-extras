namespace ParallelScan
{
    public class ImageSymbol
    {
        public ImageSymbol(IProcessorArchitecture arch, Address addr)
        {
            this.Architecture = arch;
            this.Address = addr;
        }

        public IProcessorArchitecture Architecture { get; }
        public Address Address { get; }
    }
}