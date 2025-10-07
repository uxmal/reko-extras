using Reko.Core;
using Reko.Core.Graphs;
using Reko.Scanning;
using System.Collections.Generic;

namespace Reko.Extras.Interactive;

public class ScanResults
{
    public ScanResults()
    {
        this.CFG = new DiGraph<Address>();
        this.Blocks = new Dictionary<Address, RtlBlock>();
        this.CalledAddresses = new HashSet<Address>();
    }

    public DiGraph<Address> CFG { get; }
    public HashSet<Address> CalledAddresses { get; }
    public Dictionary<Address, RtlBlock> Blocks { get; }
}
