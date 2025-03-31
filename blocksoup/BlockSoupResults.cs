using Reko.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.blocksoup;

class BlockSoupResults<T>
    where T : IAddressable
{
    public BlockSoupResults(
        Dictionary<Address, SoupBlock<T>> blocks,
        List<SoupEdge> edges,
    Dictionary<Address, int> callTallies)
    {
        this.Blocks = blocks;
        this.Edges = edges;
        this.CallTallies = callTallies;
    }
    public Dictionary<Address, SoupBlock<T>> Blocks { get; }
    public List<SoupEdge> Edges { get; }
    public Dictionary<Address, int> CallTallies { get; }

    public void Deconstruct(
        out Dictionary<Address, SoupBlock<T>> blocks,
        out List<SoupEdge> edges,
        out Dictionary<Address, int> callTallies)
    {
        blocks = this.Blocks; 
        edges = this.Edges;
        callTallies = this.CallTallies;
    }
}
