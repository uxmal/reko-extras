using Reko.Core;
using Reko.Scanning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reko.Extras.blocksoup;

public struct SoupEdge
{
    public SoupEdge(EdgeType fallthrough, Address addrFrom, Address address)
    {
        EdgeType = fallthrough;
        From = addrFrom;
        To = address;
    }

    public EdgeType EdgeType { get; }
    public Address From { get; }
    public Address To { get; }

    public override string ToString()
    {
        return $"{EdgeType} {From:X8} -> {To:X8}";
    }
}
