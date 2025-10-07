using Reko.Core;
using System.Collections.Generic;

namespace Reko.Extras.Interactive
{
    internal class BlockCluster
    {

        public BlockCluster(List<Address> addr, HashSet<Address> clusterBlocks)
        {
            this.Entries = addr;
            this.Blocks = clusterBlocks;
        }

        public List<Address> Entries { get; }
        public HashSet<Address> Blocks { get; }

    }
}