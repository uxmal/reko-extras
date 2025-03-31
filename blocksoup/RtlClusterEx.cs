using Reko.Core;
using Reko.Core.Rtl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.blocksoup;

public readonly struct RtlClusterEx : IAddressable
{
    public RtlClusterEx(RtlInstructionCluster cluster)
    {
        Cluster = cluster;
    }
    public readonly RtlInstructionCluster Cluster { get; }
    public readonly Address Address => Cluster.Address;
    public readonly override string ToString()
    {
        return $"{Address:X8}: {Cluster}";
    }
}
