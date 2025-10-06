using Reko.Core;
using Reko.Core.Graphs;
using System.Collections.Generic;

namespace Reko.Extras.Interactive;

public class ScanResults
{
    public ScanResults()
    {
        this.CFG = new DiGraph<Address>();
        this.CalledAddresses = new HashSet<Address>();
    }

    public DiGraph<Address> CFG { get; }
    public HashSet<Address> CalledAddresses { get; }

}
