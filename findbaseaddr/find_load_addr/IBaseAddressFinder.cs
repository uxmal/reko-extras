using Reko.Core;

namespace FindLoadAddr
{
    public interface IBaseAddressFinder
    {
        EndianServices Endianness { get; set; }

        void Run();
    }
}