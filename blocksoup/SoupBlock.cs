using Reko.Core;

namespace Reko.Extras.blocksoup;

public class SoupBlock<T>
    where T: IAddressable
{
    public SoupBlock(Address addr)
    {
        this.Begin = addr;
        this.End = addr;
        this.Instrs = [];
    }

    public Address End { get; internal set; }
    public Address Begin { get; }
    public List<T> Instrs { get; }
}